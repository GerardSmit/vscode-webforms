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