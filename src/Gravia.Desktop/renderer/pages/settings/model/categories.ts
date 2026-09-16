import type { LocalizedTextDto, SettingSummaryDto } from '@/shared/api';

export interface SettingsCategory {
  key: string;
  title: string;
  settings: SettingSummaryDto[];
  children: SettingsCategory[];
}

export function localizedText(text: LocalizedTextDto, language: string): string {
  return (
    text.translations[language] ?? text.translations.en ?? Object.values(text.translations)[0] ?? ''
  );
}

export function buildCategories(settings: readonly SettingSummaryDto[], language: string) {
  const roots: SettingsCategory[] = [];
  for (const setting of settings) {
    let siblings = roots;
    const path: (readonly [string, string])[][] = [];
    for (const segment of setting.uiPath) {
      // The DTO has no category IDs. Canonical translations keep selection stable on language change.
      path.push(Object.entries(segment.translations).sort(([a], [b]) => a.localeCompare(b)));
      const key = JSON.stringify(path);
      let category = siblings.find((item) => item.key === key);
      if (!category) {
        category = { key, title: localizedText(segment, language), settings: [], children: [] };
        siblings.push(category);
      }
      category.settings.push(setting);
      siblings = category.children;
    }
  }
  return roots;
}

export function findCategory(
  categories: SettingsCategory[],
  key: string | undefined,
): SettingsCategory | undefined {
  for (const category of categories) {
    if (category.key === key) return category;
    const child = findCategory(category.children, key);
    if (child) return child;
  }
  return undefined;
}

export function filterCategories(
  categories: SettingsCategory[],
  matches: (category: SettingsCategory) => boolean,
): SettingsCategory[] {
  return categories.flatMap((category) => {
    const children = filterCategories(category.children, matches);
    return matches(category) || children.length ? [{ ...category, children }] : [];
  });
}
