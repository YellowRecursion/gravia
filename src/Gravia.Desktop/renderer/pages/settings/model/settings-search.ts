import { createContext } from 'react';

export const SettingsSearchContext = createContext('');

export function matchesSettingSearch(search: string, title: string, description = '') {
  const term = search.trim().toLocaleLowerCase();
  return !term || `${title}\n${description}`.toLocaleLowerCase().includes(term);
}
