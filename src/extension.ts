import * as vscode from 'vscode';
import * as resourceOpen from "./features/resources/open"
import * as resourceValidation from "./features/resources/validation"
import * as controlsDefinitions from "./features/controls/definition"

export function activate(context: vscode.ExtensionContext) {
  resourceOpen.activate(context)
  resourceValidation.activate(context)
  controlsDefinitions.activate(context)
}