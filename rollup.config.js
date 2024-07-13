const { nodeResolve } = require('@rollup/plugin-node-resolve');
const commonjs = require('@rollup/plugin-commonjs');
const typescript = require('@rollup/plugin-typescript');
const replace = require('@rollup/plugin-replace');
const { join } = require('path');

const production = !process.env.ROLLUP_WATCH

module.exports = [{
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
            'process.env.SERVER_PATH': JSON.stringify(join(__dirname, 'server', 'src', 'WebForms.LanguageServer', 'bin', 'Debug', 'net8.0', 'WebForms.LanguageServer.exe'))
        }),
        typescript(),
        nodeResolve(),
        commonjs()
    ]
}]