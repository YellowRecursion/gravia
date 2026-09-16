// Interpretation for app-wide appearance only; editor selection comes from DTO metadata.
export function themePreference(value: unknown): 'system' | 'light' | 'dark' {
  return value === 'light' || value === 'dark' ? value : 'system';
}

export function languagePreference(value: unknown): 'ru' | 'en' {
  return value === 'en' ? 'en' : 'ru';
}
