import assert from 'node:assert/strict';
import { test } from 'node:test';
import { theme } from 'antd';
import { appThemes, getThemeVariables } from '../renderer/app/config/theme.ts';

for (const [mode, algorithm, container, layout] of [
  ['dark', theme.darkAlgorithm, '#141414', '#000000'],
  ['light', theme.defaultAlgorithm, '#ffffff', '#f5f5f5'],
]) {
  test(`${mode} uses Ant's palette without tinted neutral overrides`, () => {
    const token = theme.getDesignToken(appThemes[mode]);
    assert.deepEqual(token, theme.getDesignToken({ algorithm }));
    const variables = getThemeVariables(token);
    assert.equal(variables['--gravia-bg-container'], container);
    assert.equal(variables['--gravia-bg-layout'], layout);
    assert.equal(variables['--gravia-bg-elevated'], token.colorBgElevated);
    assert.equal(variables['--gravia-text'], token.colorText);
    assert.equal(variables['--gravia-border-secondary'], token.colorBorderSecondary);
    assert.equal(variables['--gravia-primary'], token.colorPrimary);
    assert.equal(variables['--gravia-primary-bg'], token.colorPrimaryBg);
    assert.equal(variables['--gravia-shadow'], token.boxShadowSecondary);
  });
}
