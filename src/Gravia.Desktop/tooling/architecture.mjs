import path from 'node:path';
import boundaries from 'eslint-plugin-boundaries';

const slices = ['pages', 'widgets', 'features', 'entities'];
const layers = ['app', ...slices, 'shared'];

// Used by the real lint config and by negative architecture tests. No separate test policy.
export function architectureConfig(rootPath) {
  return {
    files: ['renderer/**/*.{ts,tsx,js,jsx,mjs,cjs}'],
    plugins: { boundaries },
    settings: {
      'boundaries/root-path': rootPath,
      'boundaries/elements': [
        { type: 'app', pattern: 'renderer/app', partialMatch: false },
        ...slices.map((type) => ({
          type,
          pattern: `renderer/${type}/*`,
          partialMatch: false,
          capture: ['slice'],
        })),
        {
          type: 'shared',
          pattern: 'renderer/shared/{api,ui,lib,config,i18n}',
          partialMatch: false,
        },
      ],
      'boundaries/flag-as-external': {
        unresolvableAlias: false,
        inNodeModules: true,
        outsideRootPath: false,
      },
      'import/resolver': {
        typescript: { project: path.join(rootPath, 'renderer/tsconfig.json') },
      },
    },
    rules: {
      'boundaries/no-unknown-files': 'error',
      'boundaries/no-unknown-dependencies': 'error',
      'boundaries/no-ignored-dependencies': 'error',
      'boundaries/dependencies': [
        'error',
        {
          default: 'disallow',
          checkAllOrigins: true,
          policies: [
            { allow: { to: { module: { origin: 'external' } } } },
            ...layers.map((layer, index) => ({
              from: { element: { type: layer } },
              allow: {
                to: {
                  element: {
                    type: layer === 'shared' ? ['shared'] : layers.slice(index + 1),
                    fileInternalPath: 'index.{ts,tsx}',
                  },
                },
              },
            })),
            { disallow: { to: { module: { source: ['electron', 'electron/*'] } } } },
          ],
        },
      ],
      // Statically analyzable imports only; computed loading can bypass module boundaries.
      'no-restricted-syntax': [
        'error',
        {
          selector: 'ImportExpression[source.type!="Literal"]',
          message: 'Use a literal import path so FSD boundaries can be checked.',
        },
        {
          selector: 'CallExpression[callee.name="require"]',
          message: 'Renderer uses ES modules, not require().',
        },
        {
          selector: 'TSImportEqualsDeclaration',
          message: 'Renderer uses standard ES imports, not import = require().',
        },
        {
          selector:
            'CallExpression[callee.object.type="MetaProperty"][callee.property.name="glob"]',
          message: 'Use explicit imports; glob loading bypasses FSD import checks.',
        },
      ],
    },
  };
}
