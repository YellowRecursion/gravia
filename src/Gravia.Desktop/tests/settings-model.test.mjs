import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  languagePreference,
  themePreference,
} from '../renderer/entities/settings/model/appearance-preferences.ts';
import {
  buildCategories,
  filterCategories,
  findCategory,
  localizedText,
} from '../renderer/pages/settings/model/categories.ts';
import { matchesSettingSearch } from '../renderer/pages/settings/model/settings-search.ts';

const text = (en, ru) => ({ translations: { en, ru } });
const appearance = text('Appearance', 'Внешний вид');
const summary = (id, uiPath) => ({ id, title: text(id, id), description: null, uiPath });

test('section search preserves matching branches and ancestors without changing the catalog', () => {
  const categories = buildCategories(
    [
      summary('editor', [appearance, text('Editor', 'Редактор')]),
      summary('colors', [appearance, text('Colors', 'Цвета')]),
    ],
    'ru',
  );
  const search = (term) =>
    filterCategories(categories, (category) =>
      category.settings.some((setting) =>
        matchesSettingSearch(term, localizedText(setting.title, 'ru')),
      ),
    );
  const found = search(' EDIT ');
  assert.equal(found.length, 1);
  assert.equal(found[0].key, categories[0].key);
  assert.deepEqual(
    found[0].children.map((child) => child.title),
    ['Редактор'],
  );
  assert.equal(categories[0].children.length, 2);
  assert.deepEqual(search('Внешний'), []);
  assert.deepEqual(search('   '), categories);
  assert.deepEqual(search('missing'), []);
  assert.deepEqual(found[0].settings, categories[0].settings);
  assert.equal(matchesSettingSearch(' ТЁМН ', 'Тема', 'Светлая или тёмная палитра'), true);
  assert.equal(matchesSettingSearch('ТЁМН', 'Язык', 'Язык интерфейса'), false);
});

test('unknown persisted values have safe appearance fallbacks without rewriting the Host', () => {
  assert.equal(themePreference('light'), 'light');
  assert.equal(themePreference('dark'), 'dark');
  assert.equal(themePreference('system'), 'system');
  assert.equal(themePreference(null), 'system');
  assert.equal(themePreference('future-theme'), 'system');
  assert.equal(languagePreference('en'), 'en');
  assert.equal(languagePreference('ru'), 'ru');
  assert.equal(languagePreference(null), 'ru');
});

test('categories share prefixes and keep selection keys when language changes', () => {
  const settings = [
    summary('theme', [appearance]),
    summary('nested', [appearance, text('Editor', 'Редактор')]),
  ];
  const ru = buildCategories(settings, 'ru');
  const en = buildCategories(settings, 'en');
  assert.equal(ru.length, 1);
  assert.equal(ru[0].title, 'Внешний вид');
  assert.equal(en[0].title, 'Appearance');
  assert.equal(ru[0].key, en[0].key);
  assert.deepEqual(
    ru[0].settings.map((item) => item.id),
    ['theme', 'nested'],
  );
  assert.equal(findCategory(en, ru[0].children[0].key).title, 'Editor');
  assert.equal(findCategory(en, 'missing'), undefined);
  assert.deepEqual(buildCategories([], 'ru'), []);
});

test('category identity does not depend on dictionary insertion order', () => {
  const reversed = { translations: { ru: 'Внешний вид', en: 'Appearance' } };
  const categories = buildCategories([summary('a', [appearance]), summary('b', [reversed])], 'ru');
  assert.equal(categories.length, 1);
  assert.equal(categories[0].settings.length, 2);
  assert.equal(localizedText({ translations: { en: 'Fallback' } }, 'ru'), 'Fallback');
});
