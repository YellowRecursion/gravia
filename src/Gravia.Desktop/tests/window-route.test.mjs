import assert from 'node:assert/strict';
import { test } from 'node:test';
import { getSettingsWindowScope } from '../renderer/app/config/window-route.ts';

test('only explicit known settings routes replace the main application', () => {
  assert.equal(getSettingsWindowScope('?window=settings&scope=application'), 'application');
  assert.equal(getSettingsWindowScope('?window=settings&scope=project'), 'project');
  for (const search of [
    '',
    '?scope=application',
    '?window=settings',
    '?window=settings&scope=unknown',
    '?window=other&scope=project',
  ]) {
    assert.equal(getSettingsWindowScope(search), null);
  }
});
