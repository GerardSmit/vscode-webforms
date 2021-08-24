  import { nodeResolve } from '@rollup/plugin-node-resolve';
  import commonjs from '@rollup/plugin-commonjs';
  import typescript from '@rollup/plugin-typescript';

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
          typescript(),
          nodeResolve(),
          commonjs()
      ]
  }]