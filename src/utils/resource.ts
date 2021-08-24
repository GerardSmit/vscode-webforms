import * as path from 'path';
import * as vscode from 'vscode';

function escapeRegExp(string) {
	return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

export type ResourceFilePath = {
	path: string
	culture: string
	uri: vscode.Uri
}

export async function getResourceFiles(uri: vscode.Uri): Promise<ResourceFilePath[]> {
	const info = path.parse(uri.path)

	if (info.ext !== ".ascx") {
		return []
	}

	const regex = new RegExp(`${escapeRegExp(info.name)}\\${info.ext}(?:\\.([a-z]{2}-[a-z]{2}))?\\.resx`, 'i')
	const resourceDirectory = uri.with({
		path: `${info.dir}${path.sep}App_LocalResources`
	})

	try {
		const files = await vscode.workspace.fs.readDirectory(resourceDirectory)

		return files
			.map(([name]) => ({name, match: name.match(regex) }))
			.map(({ name, match }) => ({
				path: `App_LocalResources${path.sep}${name}`,
				culture: match && match[1] ? match[1] : 'en-US',
				uri: uri.with({ path: `${info.dir}${path.sep}App_LocalResources${path.sep}${name}` })
			}))
			.sort()
	} catch {
		return []
	}
}