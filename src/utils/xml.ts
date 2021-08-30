import * as vscode from 'vscode';

const attrRegex = /(([a-zA-Z_\xC0-\xD6\xD8-\xF6\xF8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD][a-zA-Z_\-.0-9\xC0-\xD6\xD8-\xF6\xF8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C-\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD\xB7\u0300-\u036F\u203F-\u2040]*)\s*=\s*")([^"]*)"/gi

type Attribute = {
	value: string,
	range: {
		all: vscode.Range,
		name: vscode.Range,
		value: vscode.Range,
	}
}

export function parseAttributes(document: vscode.TextDocument, input: string, offset = 0): {[key: string]: Attribute} {
	const obj = {}

	for (const m of input.matchAll(attrRegex)) {
		const [fullMatch, start, name, value] = m

		obj[name.toLowerCase()] = {
			value,
			range: {
				all: new vscode.Range(
					document.positionAt(offset + m.index),
					document.positionAt(offset + m.index + fullMatch.length),
				),
				name: new vscode.Range(
					document.positionAt(offset + m.index),
					document.positionAt(offset + m.index + name.length),
				),
				value: new vscode.Range(
					document.positionAt(offset + m.index + start.length),
					document.positionAt(offset + m.index + start.length + value.length),
				)
			},
		}	
	}

	return obj
}