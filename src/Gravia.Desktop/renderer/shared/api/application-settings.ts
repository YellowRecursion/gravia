import { defineComputeService } from '@actuallab/fusion-rpc';
import { fusionHub, fusionPeer } from './fusion';
import type { RpcResult } from './rpc-result';

export interface LocalizedTextDto {
  readonly translations: Readonly<Record<string, string>>;
}

export interface SettingSummaryDto {
  readonly id: string;
  readonly title: LocalizedTextDto;
  readonly description: LocalizedTextDto | null;
  readonly uiPath: readonly LocalizedTextDto[];
}

export interface SettingDto {
  readonly summary: SettingSummaryDto;
  readonly value: unknown;
  readonly defaultValue: unknown;
}

export interface ApplicationSettingsApi {
  GetAll(): Promise<RpcResult<readonly SettingSummaryDto[]>>;
  Get(id: string): Promise<RpcResult<SettingDto | null>>;
  SetValue(id: string, value: unknown): Promise<RpcResult>;
  ResetValue(id: string): Promise<RpcResult>;
}

const applicationSettingsApiDefinition = defineComputeService('ApplicationSettingsApi', {
  GetAll: { args: [] },
  Get: { args: [''] },
  SetValue: { args: ['', null], callTypeId: 0 },
  ResetValue: { args: [''], callTypeId: 0 },
});

export const applicationSettingsApi = fusionHub.addClient<ApplicationSettingsApi>(
  fusionPeer,
  applicationSettingsApiDefinition,
);
