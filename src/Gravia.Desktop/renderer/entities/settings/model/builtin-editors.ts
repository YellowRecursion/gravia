import type { SettingsScope } from '@/shared/api';

// Temporary UI metadata for the two built-ins. Replace this registry when Host supplies a schema.
const editors: Readonly<Record<string, readonly { value: string; labelKey: string }[]>> = {
  'gravia.language': [
    { value: 'ru', labelKey: 'settings.languages.ru' },
    { value: 'en', labelKey: 'settings.languages.en' },
  ],
  'gravia.theme': [
    { value: 'system', labelKey: 'settings.themes.system' },
    { value: 'light', labelKey: 'settings.themes.light' },
    { value: 'dark', labelKey: 'settings.themes.dark' },
  ],
};

export function getBuiltinEditor(scope: SettingsScope, id: string) {
  return scope === 'application' && Object.hasOwn(editors, id) ? editors[id] : undefined;
}

export function themePreference(value: unknown): 'system' | 'light' | 'dark' {
  return value === 'light' || value === 'dark' ? value : 'system';
}

export function languagePreference(value: unknown): 'ru' | 'en' {
  return value === 'en' ? 'en' : 'ru';
}
