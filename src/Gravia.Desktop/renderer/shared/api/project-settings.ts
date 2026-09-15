import { createSettingsApi } from './settings-client';
import type { SettingsApi } from './settings-contract';

export type ProjectSettingsApi = SettingsApi;

export const projectSettingsApi = createSettingsApi('ProjectSettingsApi');
