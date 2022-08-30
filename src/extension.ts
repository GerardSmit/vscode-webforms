import * as vscode from 'vscode';
import * as path from 'path';
import { LanguageClient, TransportKind } from 'vscode-languageclient/node';
import { Trace } from 'vscode-jsonrpc';
import { acquireDotNet } from './acquireDotNet';

export async function activate(context: vscode.ExtensionContext) {
    const output = vscode.window.createOutputChannel('WebForms');

    let command, args;

    if (process.env.NODE_ENV === 'dev') {
        command = process.env.SERVER_PATH;
        args = [];
    } else {
        command = await acquireDotNet('6.0', 'GerardSmit.vscode-webforms');
        args = [path.join(__dirname, 'bin', 'WebForms.LanguageServer.dll')];
    }

    const transport = TransportKind.pipe
    const document = { scheme: 'file', language: 'html' };

    type Range = { start: number, end: number };

    type RangeData = {
        document: string
        ranges: {[key: string]: Range[]}
        versions: {[key: string]: number}
    }

    const fileDocumentMap = new Map<string, { 
        uri: vscode.Uri,
        ranges: { [extension: string]: Range[] }
        contents: { [extension: string]: string }
        versions: { [extension: string]: number }
        diagnostics: vscode.Diagnostic[]
    }>();

    const findRange = (range: Range[], position: number) => {
        return range.find(r => position >= r.start && position <= r.end);
    }

    const getContent = (documentText: string, ranges: Range[]) => {
        let content = documentText
            .split('\n')
            .map(line => {
                return ' '.repeat(line.length);
            }).join('\n')

        ranges.forEach(r => {
            content = content.slice(0, r.start) + documentText.slice(r.start, r.end) + content.slice(r.end);
        });

        return content;
    }

    const virtualDocumentScheme = 'webforms-embedded';
    const updateDocument = new vscode.EventEmitter<vscode.Uri>();

    const getVirtualDocument = (uri: vscode.Uri) => {
        const dot = uri.path.lastIndexOf('.');

        if (dot === -1 || uri.scheme !== virtualDocumentScheme) {
            return {
                virtualDocument: null,
                extension: null
            };
        }

        const path = uri.path.substring(0, dot);
        const extension = uri.path.slice(dot + 1);
        const virtualDocument = fileDocumentMap.get(path);

        return {
            virtualDocument,
            extension
        }
    };

	vscode.workspace.registerTextDocumentContentProvider(virtualDocumentScheme, {
		provideTextDocumentContent: uri => {
            const { virtualDocument, extension } = getVirtualDocument(uri);

            if (!virtualDocument) {
                return '';
            }
            
            const content = virtualDocument.contents[extension]
            return content ?? '';
		},
        onDidChange: updateDocument.event
	});

    const updateVirtualDocument = (document: vscode.TextDocument, extension: string) => {
        const fileDocument = fileDocumentMap.get(document.uri.path);

        if (!fileDocument) {
            return null;
        }

        const ranges = fileDocument.ranges[extension];

        if (!ranges) {
            return null;
        }

        const content = getContent(document.getText(), ranges);
        const uri = vscode.Uri.parse(`${virtualDocumentScheme}:${document.uri.path}.${extension}`);

        fileDocument.contents[extension] = content;
        updateDocument.fire(uri);

        return uri
    }

    const getUri = (document: vscode.TextDocument, position: vscode.Position) => {
        const fileDocument = fileDocumentMap.get(document.uri.path);

        if (fileDocument) {
            const offset = document.offsetAt(position);

            for (const [extension, ranges] of Object.entries(fileDocument.ranges)) {
                if (findRange(ranges, offset)) {
                    return updateVirtualDocument(document, extension);
                }
            }
        }

        return null;
    }

    const getDiagnostics = (uri: vscode.Uri, diagnostics?: vscode.Diagnostic[]) => {
        const virtualDocument = fileDocumentMap.get(uri.path);
                    
        if (virtualDocument) {
            if (diagnostics) {
                virtualDocument.diagnostics = diagnostics;
            } else {
                diagnostics = virtualDocument.diagnostics;
            }

            for (const extension of Object.keys(virtualDocument.contents)) {
                const target = vscode.Uri.parse(`${virtualDocumentScheme}://${uri.path}.${extension}`);
                const targetDiagnostics = vscode.languages.getDiagnostics(target);
                
                if (targetDiagnostics.length > 0) {
                    diagnostics = [...diagnostics, ...targetDiagnostics];
                }

                output.appendLine(`Got ${targetDiagnostics.length} diagnostics for ${target}`);
            }
        }

        return diagnostics
    }

    vscode.languages.onDidChangeDiagnostics((e) => {
        for (const virtualUri of e.uris) {
            const { virtualDocument } = getVirtualDocument(virtualUri);

            if (!virtualDocument) {
                continue;
            }

            const uri = virtualDocument.uri
            client.diagnostics.set(uri, getDiagnostics(uri));
        }
    });
 
    const client = new LanguageClient(
        'webformsLanguageServer', 
        'WebForms Server',
        {
            run : { command, transport, args },
            debug: { command, transport, args }
        },
        {
            documentSelector: [ document ],
            synchronize: {
                configurationSection: 'webformsLanguageServer',
                fileEvents: vscode.workspace.createFileSystemWatcher('**/*.ascx')
            },
            middleware: {
                provideCompletionItem: async (document, position, context, token, next) => {
                    const uri = getUri(document, position);

                    if (uri) {
                        return await vscode.commands.executeCommand<vscode.CompletionList>(
                            'vscode.executeCompletionItemProvider',
                            uri,
                            position,
                            context.triggerCharacter
                        );
                    }

                    return await next(document, position, context, token);
                },
                handleDiagnostics: (uri, diagnostics, next) => {
                    next(uri, getDiagnostics(uri, diagnostics));
                },
            }
        },
        false
    );

    client.registerProposedFeatures();
    client.trace = Trace.Verbose;
    
    let inspectionsEnabled = false;
    let clientReady = false;

    client.onReady().then(() => {
        clientReady = true;

        context.subscriptions.push(client.onNotification('webforms/log', function(data) {
            output.appendLine(data);
        }))

        context.subscriptions.push(client.onNotification('webforms/ranges', function(data: RangeData) {
            const uri = vscode.Uri.parse(data.document);
            let fileDocuments = fileDocumentMap.get(uri.path);

            if (!fileDocuments) {
                fileDocuments = {
                    uri,
                    contents: {},
                    ranges: data.ranges,
                    versions: {},
                    diagnostics: []
                };

                fileDocumentMap.set(uri.path, fileDocuments);
            } else {
                fileDocuments.ranges = data.ranges;
            }

            for (const [extension, version] of Object.entries(data.versions)) {
                const currentVersion = fileDocuments.versions[extension];

                if (currentVersion === version) {
                    continue;
                }

                fileDocuments.versions[extension] = version;

                const document = vscode.workspace.textDocuments.find(d => d.uri.path === uri.path);

                if (document) {
                    const virtualUri = updateVirtualDocument(document, extension);

                    if (currentVersion === undefined) {
                        vscode.workspace.openTextDocument(virtualUri).then(doc => {
                            vscode.window.showTextDocument(doc, { preserveFocus: false }).then(editor => {
                                client.diagnostics.set(uri, getDiagnostics(uri));
                                return vscode.commands.executeCommand('workbench.action.closeActiveEditor');
                            });
                        });
                    }
                }
            }
        }))

        client.sendNotification('webforms/inspections', { enabled: inspectionsEnabled });
    });
    
    context.subscriptions.push(client.start());

    const statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 1000);
    statusBarItem.command = 'webforms.toggleInspections';

    const updateIcon = () => {
        const visible = vscode.window.visibleTextEditors.filter(i => i.document.languageId === 'webforms').length > 0

        if (visible) {
            if (inspectionsEnabled) {
                statusBarItem.tooltip = 'Disable WebForms inspections';
                statusBarItem.text = '$(eye)';
                statusBarItem.backgroundColor = '';
            } else {
                statusBarItem.tooltip = 'Activate WebForms inspections';
                statusBarItem.text = '$(eye-closed)';
                statusBarItem.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
            }

            statusBarItem.show();
        } else {
            statusBarItem.hide();
        }
    };

    const toggleInspections = () => {
        inspectionsEnabled = !inspectionsEnabled;
        updateIcon();

        if (clientReady) {
            client.sendNotification('webforms/inspections', { enabled: inspectionsEnabled });
        }
    };

    context.subscriptions.push(vscode.window.onDidChangeVisibleTextEditors(updateIcon));
    context.subscriptions.push(statusBarItem);
    context.subscriptions.push(vscode.commands.registerCommand('webforms.toggleInspections', toggleInspections));
    updateIcon();
}