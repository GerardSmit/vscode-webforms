import * as vscode from 'vscode';

export function useDocumentCache<TValue, TKey extends vscode.Uri | { uri: vscode.Uri } = vscode.Uri>(load: (key: TKey, document: vscode.TextDocument) => Promise<TValue>) {
	const cache = new Map<vscode.Uri, { version: number, value: TValue }>()

	return async(key: TKey): Promise<TValue> => {
		let uri = ('uri' in key ? key.uri : key) as vscode.Uri;
		const document = await vscode.workspace.openTextDocument(uri)
		const version = document.version
		const cacheValue = cache.get(uri)

		if (cacheValue && cacheValue.version === version) {
			return cacheValue.value
		}

		const value = await load(key, document)
		cache.set(uri, { value, version })
		return value
	}
}

export function useCache<TValue, TKey = string>(load: (key: TKey) => Promise<TValue>) {
	const cache = new Map<TKey, TValue>()

	return async(key: TKey): Promise<TValue> => {
		const cacheValue = cache.get(key)

		if (cacheValue) {
			return cacheValue
		}

		const value = await load(key)
		cache.set(key, value)
		return value
	}
}