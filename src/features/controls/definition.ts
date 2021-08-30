import * as vscode from 'vscode';
import { useCache, useDocumentCache } from '../../utils/document-cache';
import { parseAttributes } from '../../utils/xml';
import * as path from 'path';
import { dynamicSort } from '../../utils/array';

export async function activate(context: vscode.ExtensionContext) {
	context.subscriptions.push(vscode.commands.registerCommand('webforms.action.openReferencedControl', openReferencedControl));

	context.subscriptions.push(vscode.languages.registerDefinitionProvider("html", {
		provideDefinition: findDefinitions
	}));

	// TODO: This doesn't work yet because the HTML language service overrides this.
	context.subscriptions.push(vscode.languages.registerRenameProvider("html", {
		provideRenameEdits: renameEdits
	}));
}

const registerRegex = /(<%@\s*register\s*)(.*?)%>/gi

type DocumentInformation = {
	controls: ControlRegistration[]
}

type ControlRegistration = {
	tagPrefix: string
	tagName: string
	src: string
	uri: vscode.Uri
	range: {
		all: vscode.Range
		tagPrefix: vscode.Range
		tagName: vscode.Range
		src: vscode.Range
	},
	references: ControlReference[]
}

type ControlReference = {
	range: {
		all: vscode.Range
		tagPrefix: vscode.Range
		tagName: vscode.Range
	}
}

type WebConfig = {
	uri: vscode.Uri
	dir: string
	content: string
	isRoot: boolean
}

const rootWebConfigRegex = /compilation|pages/i
const webConfigs = new Map<vscode.Uri, WebConfig | null>()

async function getWebConfig(fileUri: vscode.Uri): Promise<WebConfig[]> {
	let info = path.parse(fileUri.path)
	const items = []

	while (info.dir !== '/' && info.dir !== '\\' && !(info.dir.length === 3 && info.dir[0] === '/' && info.dir[2] === ':')) {
		const uri = fileUri.with({
			path: info.dir + '/web.config'
		})

		try {
			let webConfig = webConfigs.get(uri)

			if (!webConfig) {
				const buffer = await vscode.workspace.fs.readFile(uri)
				const content = buffer.toString()
				const isRoot = content.match(rootWebConfigRegex) !== null
	
				webConfig = { uri, content, isRoot, dir: info.dir }
				webConfigs.set(uri, webConfig)
			}
			
			items.push(webConfig)
		} catch {
			webConfigs.set(uri, null)
		}

		info = path.parse(info.dir)
	}

	return items
}

const getResourceFile = useDocumentCache<DocumentInformation>(async (path, document) => {
	const result: DocumentInformation = {
		controls: []
	}

	try {
		const str = document.getText()
		const root = (await getWebConfig(path)).find(i => i.isRoot)
		const controls: {[key: string]: ControlRegistration} = {}

		if (root) {
			for (const m of str.matchAll(registerRegex)) {
				const [fullMatch, prefix, attrText] = m
				const { src, tagname, tagprefix } = parseAttributes(document, attrText, m.index + prefix.length)

				if (!src || !tagname || !tagprefix) continue;
	
				let srcValue = src.value
	
				if (srcValue[0] === '~') {
					srcValue = srcValue.substr(1)
				}
				
				const uri = root.uri.with({
					path: root.dir + srcValue
				})

				const control: ControlRegistration = {
					uri,
					src: src.value,
					tagName: tagname.value,
					tagPrefix: tagprefix.value,
					references: [],
					range: {
						all: new vscode.Range(
							document.positionAt(m.index),
							document.positionAt(m.index + fullMatch.length),
						),
						src: src.range.value,
						tagName: tagname.range.value,
						tagPrefix: tagprefix.range.value
					}
				}

				const key = (control.tagPrefix + ':' + control.tagName).toLowerCase();

				controls[key] = control
				result.controls.push(control)
			}

			const regex = new RegExp(result.controls.map(i => '<' + i.tagPrefix + ':' + i.tagName).join('|'), 'ig')

			for (const m of str.matchAll(regex)) {
				const [fullMatch] = m
				const index = fullMatch.indexOf(':')
				const tagPrefix = fullMatch.substr(1, index - 1)
				const tagName = fullMatch.substr(index + 1)
				const key = (tagPrefix + ':' + tagName).toLowerCase()
				const control = controls[key]
				
				if (control) {
					control.references.push({
						range: {
							all: new vscode.Range(
								document.positionAt(m.index + 1),
								document.positionAt(m.index + fullMatch.length),
							),
							tagPrefix: new vscode.Range(
								document.positionAt(m.index + 1),
								document.positionAt(m.index + index),
							),
							tagName: new vscode.Range(
								document.positionAt(m.index + index + 1),
								document.positionAt(m.index + fullMatch.length),
							)
						}
					})
				}
			}
			
			result.controls.sort(dynamicSort('tagName'))
		}
	} catch {

	}

	return result
})

const emptyRange = new vscode.Range(new vscode.Position(0, 0), new vscode.Position(0, 0))

async function findDefinitions(document: vscode.TextDocument, position: vscode.Position): Promise<vscode.DefinitionLink[]> {
	const info = await getResourceFile(document.uri);
	const control = info.controls.find(i => i.range.src.contains(position) || i.references.find(i => i.range.tagName.contains(position)))

	if (control) {
		return [
			{
				targetUri: control.uri,
				targetRange: emptyRange
			}
		]
	}

	return []
}

async function renameEdits(document: vscode.TextDocument, position: vscode.Position, value: string): Promise<vscode.WorkspaceEdit> {
	const uri = document.uri
	const info = await getResourceFile(uri);
	const tagPrefixEdit = info.controls.find(i => i.range.tagPrefix.contains(position) || i.references.find(i => i.range.tagPrefix.contains(position)))

	if (tagPrefixEdit) {
		const edit = new vscode.WorkspaceEdit()
		edit.replace(uri, tagPrefixEdit.range.tagPrefix, value)

		for (const ref of tagPrefixEdit.references) {
			edit.replace(uri, ref.range.tagPrefix, value)
		}
		return edit
	}
	
	const tagNameEdit = info.controls.find(i => i.range.tagName.contains(position) || i.references.find(i => i.range.tagName.contains(position)))

	if (tagNameEdit) {
		const edit = new vscode.WorkspaceEdit()
		edit.replace(uri, tagNameEdit.range.tagName, value)

		for (const ref of tagNameEdit.references) {
			edit.replace(uri, ref.range.tagName, value)
		}
		return edit
	}
}

async function openReferencedControl() {
	const editor = vscode.window.activeTextEditor;
	if (!editor) return;
  
	const info = await getResourceFile(editor.document.uri);

	if (!info.controls.length) {
		await vscode.window.showWarningMessage('There are no referenced user controls in this file')
		return;
	}

	const items = Object.fromEntries(info.controls.map(i => [`${i.tagPrefix}:${i.tagName}`, i.uri]))
	const file = await vscode.window.showQuickPick(Object.keys(items), {
		title: "Select the user control to open"
	})

	if (file) {
		await vscode.window.showTextDocument(items[file])
	}
  }