import { nodeResolve } from '@rollup/plugin-node-resolve';
import commonjs from '@rollup/plugin-commonjs';
import typescript from '@rollup/plugin-typescript';
import replace from '@rollup/plugin-replace';
import * as path from 'path';

const production = !process.env.ROLLUP_WATCH

export default [{
    input: 'src/extension.ts',
    output: {
        file: 'extension/index.js',
        format: 'cjs',
        external: [
            'vscode'
        ]
    },
    plugins: [
        replace({
            'process.env.NODE_ENV': JSON.stringify(production ? 'production' : 'dev'),
            'process.env.SERVER_PATH': JSON.stringify(path.join(__dirname, 'server', 'src', 'WebForms.LanguageServer', 'bin', 'Debug', 'net6.0', 'WebForms.LanguageServer.exe'))
        }),
        typescript(),
        nodeResolve(),
        commonjs()
    ]
}]