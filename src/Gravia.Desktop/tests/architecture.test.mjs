import assert from 'node:assert/strict';
import path from 'node:path';
import { test } from 'node:test';
import { ESLint } from 'eslint';
import tseslint from 'typescript-eslint';
import { architectureConfig } from '../tooling/architecture.mjs';

const rootPath = path.join(import.meta.dirname, 'fixtures/fsd');
const eslint = new ESLint({
  cwd: rootPath,
  overrideConfigFile: true,
  overrideConfig: [
    { files: ['**/*.{ts,tsx}'], languageOptions: { parser: tseslint.parser } },
    architectureConfig(rootPath),
  ],
});

const allowed = [
  ['app composes a page', 'app/ui/App.tsx', "import { editor } from '@/pages/editor'"],
  ['page composes widget', 'pages/editor/index.ts', "import { canvas } from '@/widgets/canvas'"],
  [
    'widget uses feature',
    'widgets/canvas/index.ts',
    "import { editNode } from '@/features/edit-node'",
  ],
  [
    'feature uses entity types',
    'features/edit-node/index.ts',
    "import type { Project } from '@/entities/project'",
  ],
  ['entity uses shared API', 'entities/project/index.ts', "import { client } from '@/shared/api'"],
  ['shared segments cooperate', 'shared/i18n/index.ts', "import { format } from '@/shared/lib'"],
  [
    'internal relative import',
    'features/edit-node/index.ts',
    "export { editNode } from './model/edit'",
  ],
  [
    'public barrel exports internals',
    'shared/api/index.ts',
    "export { client } from './generated/client'",
  ],
  ['external library', 'features/edit-node/index.ts', "import { useState } from 'react'"],
  ['literal lazy import', 'pages/editor/index.ts', "const panel = import('@/widgets/canvas')"],
  [
    'explicit public index',
    'pages/editor/index.ts',
    "import { canvas } from '@/widgets/canvas/index'",
  ],
];

const forbidden = [
  [
    'upward dependency',
    'entities/project/index.ts',
    "import { editNode } from '@/features/edit-node'",
    'boundaries/dependencies',
  ],
  [
    'sibling features',
    'features/edit-node/index.ts',
    "import { run } from '@/features/run'",
    'boundaries/dependencies',
  ],
  [
    'sibling widgets',
    'widgets/canvas/index.ts',
    "import { inspector } from '@/widgets/inspector'",
    'boundaries/dependencies',
  ],
  [
    'sibling entities',
    'entities/project/index.ts',
    "import { run } from '@/entities/run'",
    'boundaries/dependencies',
  ],
  [
    'deep alias import',
    'widgets/canvas/index.ts',
    "import { editNode } from '@/features/edit-node/model/edit'",
    'boundaries/dependencies',
  ],
  [
    'deep relative import',
    'widgets/canvas/index.ts',
    "import { editNode } from '../../features/edit-node/model/edit'",
    'boundaries/dependencies',
  ],
  [
    'deep type import',
    'features/edit-node/index.ts',
    "import type { Project } from '@/entities/project/model/project'",
    'boundaries/dependencies',
  ],
  [
    'deep re-export',
    'widgets/canvas/index.ts',
    "export { editNode } from '@/features/edit-node/model/edit'",
    'boundaries/dependencies',
  ],
  [
    'deep dynamic import',
    'widgets/canvas/index.ts',
    "const edit = import('@/features/edit-node/model/edit')",
    'boundaries/dependencies',
  ],
  [
    'shared cannot depend on app',
    'shared/i18n/index.ts',
    "import { App } from '@/app/ui/App'",
    'boundaries/dependencies',
  ],
  [
    'shared internals are private',
    'app/ui/App.tsx',
    "import { language } from '@/shared/i18n/initialize'",
    'boundaries/dependencies',
  ],
  [
    'generated client is private',
    'features/edit-node/index.ts',
    "import { client } from '@/shared/api/generated/client'",
    'boundaries/dependencies',
  ],
  ['unknown layer', 'misc/value.ts', 'export const value = 1', 'boundaries/no-unknown-files'],
  [
    'unknown local target',
    'app/ui/App.tsx',
    "import { value } from '@/misc/value'",
    'boundaries/no-unknown-dependencies',
  ],
  [
    'renderer cannot import shell',
    'app/ui/App.tsx',
    "import shell from '../../../shell.cjs'",
    'boundaries/no-unknown-dependencies',
  ],
  [
    'Node is not available',
    'app/ui/App.tsx',
    "import fs from 'node:fs'",
    'boundaries/dependencies',
  ],
  [
    'Electron is not available',
    'app/ui/App.tsx',
    "import { ipcRenderer } from 'electron'",
    'boundaries/dependencies',
  ],
  [
    'computed imports bypass analysis',
    'app/ui/App.tsx',
    'const page = import(name)',
    'no-restricted-syntax',
  ],
  [
    'CommonJS is not renderer code',
    'app/ui/App.tsx',
    "const fs = require('fs')",
    'no-restricted-syntax',
  ],
  [
    'TypeScript CommonJS import',
    'app/ui/App.tsx',
    "import fs = require('fs')",
    'no-restricted-syntax',
  ],
  [
    'glob imports bypass analysis',
    'app/ui/App.tsx',
    "const modules = import.meta.glob('../../features/**/*.ts')",
    'no-restricted-syntax',
  ],
  [
    'normalized path cannot bypass rules',
    'features/edit-node/index.ts',
    "import { run } from '@/features/edit-node/../run'",
    'boundaries/dependencies',
  ],
];

for (const [name, file, source] of allowed) {
  test(`FSD allows ${name}`, async () => {
    const [result] = await eslint.lintText(source, {
      filePath: path.join(rootPath, 'renderer', file),
    });
    assert.equal(result.messages.length, 0, JSON.stringify(result.messages));
  });
}

for (const [name, file, source, ruleId] of forbidden) {
  test(`FSD rejects ${name}`, async () => {
    const [result] = await eslint.lintText(source, {
      filePath: path.join(rootPath, 'renderer', file),
    });
    assert.ok(
      result.messages.some((message) => message.ruleId === ruleId),
      JSON.stringify(result.messages),
    );
  });
}
