import { createSettingsApi } from './settings-client';
import type { SettingsApi } from './settings-contract';

export type ApplicationSettingsApi = SettingsApi;

export const applicationSettingsApi = createSettingsApi('ApplicationSettingsApi');
