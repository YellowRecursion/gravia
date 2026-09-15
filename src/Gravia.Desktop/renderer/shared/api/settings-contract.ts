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

export interface SettingsApi {
  GetAll(): Promise<RpcResult<readonly SettingSummaryDto[]>>;
  Get(id: string): Promise<RpcResult<SettingDto | null>>;
  SetValue(id: string, value: unknown): Promise<RpcResult>;
  ResetValue(id: string): Promise<RpcResult>;
}
