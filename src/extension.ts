import * as vscode from 'vscode';
import * as resourceOpen from "./features/resources/open"
import * as resourceValidation from "./features/resources/validation"

export function activate(context: vscode.ExtensionContext) {
  resourceOpen.activate(context)
  resourceValidation.activate(context)
}