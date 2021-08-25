import * as vscode from 'vscode';
import * as path from 'path';
import { getResourceFiles, ResourceFilePath } from '../../utils/resource';
import { Throttle } from '../../utils/throttle';
import { dynamicSort as sortByKey } from '../../utils/array';

export async function activate(context: vscode.ExtensionContext) {
	let enabled = vscode.workspace.getConfiguration('webforms').get<boolean>("resources.validation")

	vscode.workspace.onDidChangeConfiguration(async (e) => {
		if (e.affectsConfiguration("webforms.resources.validation")) {
			let newValue = vscode.workspace.getConfiguration('webforms').get<boolean>("resources.validation")

			if (enabled === newValue) {
				return
			}

			const reload = await vscode.window.showInformationMessage(`Please reload VSCode to ${enabled ? 'disable' : 'enable'} the validator.`, 'Reload Window');
			if (reload) {
				vscode.commands.executeCommand('workbench.action.reloadWindow');
			}
		}
	})

	if (!enabled) {
		return
	}

	const collection = vscode.languages.createDiagnosticCollection('webforms');
	const throttle = new Throttle(1000, document => updateDiagnostics(document, collection))

	if (vscode.window.activeTextEditor) {
		await updateDiagnostics(vscode.window.activeTextEditor.document, collection);
	}

	context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(async editor => {
		if (editor) {
			await updateDiagnostics(editor.document, collection);
		}
	}));

	context.subscriptions.push(vscode.workspace.onDidChangeTextDocument(async editor => {
		if (editor) {
			throttle.queue(editor.document.uri);
		}
	}));

	context.subscriptions.push(vscode.languages.registerHoverProvider('html', {
		provideHover: hover
	}));

	context.subscriptions.push(vscode.languages.registerDefinitionProvider('html', {
		provideDefinition: findDefinitions
	}));

	context.subscriptions.push(vscode.languages.registerCompletionItemProvider("html", {
		provideCompletionItems: autoComplete
	}, '"'));

	context.subscriptions.push(vscode.languages.registerRenameProvider("html", {
		provideRenameEdits: renameEdits
	}));
}

const resourceKeys = new Map<vscode.Uri, string[]>()

type Resource = {
	name: string
	value: string
	range: {
		all: vscode.Range
		name: vscode.Range
		value: vscode.Range
	}
}

type ResourceFile = {
	endOfFile: vscode.Position
	path: ResourceFilePath
	resources: Resource[]
}

async function getResourceFile(path: ResourceFilePath): Promise<ResourceFile> {
	let resources: Resource[] = []
	let endOfFile = new vscode.Position(0, 0)

	try {
		const document = await vscode.workspace.openTextDocument(path.uri)
		const str = document.getText()

		const dataRegex = /((<data\s*name=")([^"]+)"[^>]+>[\s\S]*?<value>)([\s\S]*?)<\/value>[\s\S]*?<\/data>/g
		for (let m: RegExpExecArray; m = dataRegex.exec(str); m !== null) {
			const [fullMatch, start, nameStart, name, value] = m

			resources.push({
				name,
				value,
				range: {
					all: new vscode.Range(
						document.positionAt(m.index),
						document.positionAt(m.index + fullMatch.length),
					),
					name: new vscode.Range(
						document.positionAt(m.index + nameStart.length),
						document.positionAt(m.index + nameStart.length + name.length),
					),
					value: new vscode.Range(
						document.positionAt(m.index + start.length),
						document.positionAt(m.index + start.length + value.length),
					)
				},
			})
		}

		const endPosition = str.lastIndexOf("</data>")

		if (endPosition !== -1) {
			endOfFile = document.positionAt(endPosition + 7)
		}
	} catch {

	}

	return {
		path,
		resources,
		endOfFile
	}
}

const fileResourceCache = new Map<vscode.Uri, ResourceFilePath[]>()

function* getDocumentStrings(document: vscode.TextDocument) {
	const str = document.getText()
	const getStringRegex = /(GetString\(")([^"]+)"\)/g
	for (let m: RegExpExecArray; m = getStringRegex.exec(str); m !== null) {
		let name = m[2]

		if (name.indexOf('.') === -1) {
			name += '.Text'
		}

		yield {
			name,
			getRange() {
				return new vscode.Range(
					document.positionAt(m.index + m[1].length),
					document.positionAt(m.index + m[1].length + m[2].length),
				)
			}
		}
	}
}

async function getResourceFilePaths(document: vscode.TextDocument): Promise<ResourceFilePath[]> {
	if (!document) {
		return []
	}
	
	let files = fileResourceCache.get(document.uri)

	if (!files) {
		files = await getResourceFiles(document.uri)
		fileResourceCache.set(document.uri, files)
	}

	return files
}

async function updateDiagnostics(document: vscode.TextDocument, collection: vscode.DiagnosticCollection): Promise<void> {
	let files = await getResourceFilePaths(document)

	if (files.length === 0) {
		return
	}
	
	let resourceFiles = await Promise.all(files.map(i => getResourceFile(i)))

	const errors: vscode.Diagnostic[] = [];

	for (const match of getDocumentStrings(document)) {
		let current: vscode.Diagnostic

		for (const {path, resources, endOfFile} of resourceFiles) {
			if (resources.find(i => i.name === match.name)) {
				continue;
			}

			if (!current) {
				current = {
					message: `String '${match.name}' is not found in all resource files`,
					range: match.getRange(),
					severity: vscode.DiagnosticSeverity.Warning,
					relatedInformation: []
				}

				errors.push(current)
			}

			current.relatedInformation.push(new vscode.DiagnosticRelatedInformation(
				new vscode.Location(path.uri, endOfFile),
				`Resource should be added to language '${path.culture}'`
			))
		}
	}

	collection.set(document.uri, errors);
}

function getGetString(document: vscode.TextDocument, position: vscode.Position) {
	const line = document.getText(document.lineAt(position.line).range)
	const match = line.match(/(GetString\(")([^"]+)?("?\)?\s*%?>?)/)

	if (!match) {
		return { success: false }
	}

	const pos = position.character
	const [_, prefix, name, suffix] = match
	const start = match.index + prefix.length
	const end = name ? start + name.length : pos

	if (pos < start || name && pos > end) {
		return { success: false }
	}

	const nameRange = new vscode.Range(new vscode.Position(position.line, start), new vscode.Position(position.line, end))

	return { success: true, line, start, end, pos, suffix, name, nameRange }
}

async function autoComplete(document: vscode.TextDocument, position: vscode.Position): Promise<vscode.CompletionItem[]> {
	const { success, line, start, end, pos, nameRange } = getGetString(document, position)

	if (!success) {
		return []
	}
	
	const input = line.slice(start, pos)
	let files = await getResourceFilePaths(document)
	let resourceFiles = await Promise.all(files.map(i => getResourceFile(i)))

	const items: vscode.CompletionItem[] = []
	const itemsByName: {[key: string]: vscode.CompletionItem} = {}

	for (const file of resourceFiles) {
		for (const resource of file.resources) {
			if (resource.name.startsWith(input)) {
				let item = itemsByName[resource.name]

				if (item) {
					item.detail += ", " + file.path.culture;
					(item.documentation as vscode.MarkdownString).appendMarkdown(`  \n\`${file.path.culture}\``).appendCodeblock(resource.value, 'xml')
				} else {
					let text = resource.name

					if (text.endsWith(".Text")) {
						text = text.substr(0, text.length - 5)
					}

					item = {
						label: resource.name,
						detail: file.path.culture,
						insertText: text,
						documentation: new vscode.MarkdownString(`\`${file.path.culture}\``).appendCodeblock(resource.value, 'xml'),
						range: nameRange
					}

					itemsByName[resource.name] = item
					items.push(item)
				}
			}
		}
	}

	items.sort(sortByKey("insertText"))
	return items
}

async function findDefinitions(document: vscode.TextDocument, position: vscode.Position): Promise<vscode.DefinitionLink[]> {
	const { success, name } = getGetString(document, position)

	if (!success || !name) {
		return []
	}

	let files = await getResourceFilePaths(document)
	let resourceFiles = await Promise.all(files.map(i => getResourceFile(i)))
	const definitions: vscode.DefinitionLink[] = []

	const resourceName = name.indexOf('.') === -1
		? name + '.Text'
		: name

	for (const file of resourceFiles) {
		for (const resource of file.resources) {
			if (resource.name === resourceName) {
				definitions.push({
					targetUri: file.path.uri,
					targetRange: resource.range.value
				})
			}
		}
	}

	return definitions
}

async function hover(document: vscode.TextDocument, position: vscode.Position): Promise<vscode.Hover> {
	const { success, name } = getGetString(document, position)

	if (!success || !name) {
		return null
	}

	let files = await getResourceFilePaths(document)
	let resourceFiles = await Promise.all(files.map(i => getResourceFile(i)))

	const resourceName = name.indexOf('.') === -1
		? name + '.Text'
		: name

	const str = new vscode.MarkdownString()
	str.isTrusted = true

	for (const file of resourceFiles) {
		for (const resource of file.resources) {
			if (resource.name === resourceName) {
				str.appendMarkdown(`[\` ${file.path.culture} \`](${file.path.uri}#L${resource.range.value.start.line + 1})`)
				str.appendCodeblock(resource.value, 'xml')
			}
		}
	}

	return {
		contents: [str]
	}
}

async function renameEdits(document: vscode.TextDocument, position: vscode.Position, newName: string): Promise<vscode.WorkspaceEdit> {
	const { success, name, start, end, nameRange } = getGetString(document, position)

	if (!success || !name) {
		return null
	}

	let files = await getResourceFilePaths(document)
	let resourceFiles = await Promise.all(files.map(i => getResourceFile(i)))

	const resourceName = name.indexOf('.') === -1
		? name + '.Text'
		: name

	const newResourceName = newName.indexOf('.') === -1
		? newName + '.Text'
		: newName

	var edit = new vscode.WorkspaceEdit()

	edit.replace(document.uri, nameRange, newName)

	for (const file of resourceFiles) {
		for (const resource of file.resources) {
			if (resource.name === resourceName) {
				edit.replace(file.path.uri, resource.range.name, newResourceName)
			}
		}
	}

	return edit
}