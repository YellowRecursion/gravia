import type { TFunction } from 'i18next';
import { matchesSettingSearch } from './settings-search';
import { nullableExamples } from './nullable-examples';

export const mockSettingsKey = 'mock-controls';
export const mockSettingsSections = ['basic', 'modifiers', 'collections', 'nullable'] as const;
export type MockSection = (typeof mockSettingsSections)[number];

// Search metadata for the temporary controls, including the descriptions shown in tooltips.
const mockSearchEntries: Record<MockSection, readonly (readonly [string, string?])[]> = {
  nullable: nullableExamples.map(
    (example) => [`nullableExamples.${example.id}`, 'nullableHint'] as const,
  ),
  basic: [
    ['text', 'StringSetting'],
    ['integer', 'IntSetting'],
    ['decimal', 'DoubleSetting'],
    ['boolean', 'booleanHint'],
    ['checkbox'],
    ['disabled'],
    ['readOnly'],
    ['error'],
  ],
  modifiers: [
    ['slider', 'sliderHint'],
    ['range'],
    ['stepped'],
    ['percent', 'percentHint'],
    ['precision', 'precisionHint'],
    ['Select'],
    ['Radio.Group'],
    ['Segmented'],
    ['multiline'],
    ['password', 'passwordHint'],
    ['color'],
  ],
  collections: [
    ['tags', 'tagsHint'],
    ['multiple'],
    ['strings', 'ListSetting<string>'],
    ['numbers', 'ListSetting<double>'],
    ['textDictionary', 'DictionarySetting<string>'],
    ['numberDictionary', 'DictionarySetting<double>'],
  ],
};

export function mockSectionMatches(section: MockSection, search: string, t: TFunction) {
  const translate = (key: string) => t(`settings.mock.${key}`, { defaultValue: key });
  return mockSearchEntries[section].some(([title, description]) =>
    matchesSettingSearch(search, translate(title), description ? translate(description) : ''),
  );
}
