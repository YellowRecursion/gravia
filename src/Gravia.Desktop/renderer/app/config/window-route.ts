import type { SettingsScope } from '@/shared/api';

export function getSettingsWindowScope(search: string): SettingsScope | null {
  const params = new URLSearchParams(search);
  if (params.get('window') !== 'settings') return null;
  const scope = params.get('scope');
  return scope === 'application' || scope === 'project' ? scope : null;
}
