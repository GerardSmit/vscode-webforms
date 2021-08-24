import * as vscode from 'vscode';

export class Throttle {
	private items = new Map<vscode.Uri, NodeJS.Timeout>()

	constructor(
		private timeout: number,
		private callback: (uri: vscode.TextDocument) => void
	) { }

	queue(uri: vscode.Uri) {
		let current = this.items.get(uri)

		if (current) {
			clearTimeout(current)
		}

		let id = setTimeout(() => {
			clearTimeout(id)
			this.items.delete(uri)

			const document = vscode.workspace.textDocuments.find(i => i.uri === uri)
			if (document) {
				this.callback(document)
			}
		}, this.timeout)

		this.items.set(uri, id)
	}
}