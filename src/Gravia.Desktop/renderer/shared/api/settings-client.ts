import { defineComputeService } from '@actuallab/fusion-rpc';
import { fusionHub, fusionPeer } from './fusion';
import type { SettingsApi } from './settings-contract';

const definition = {
  GetAll: { args: [] },
  Get: { args: [''] },
  SetValue: { args: ['', null], callTypeId: 0 },
  ResetValue: { args: [''], callTypeId: 0 },
};

export function createSettingsApi(serviceName: string): SettingsApi {
  return fusionHub.addClient<SettingsApi>(
    fusionPeer,
    defineComputeService(serviceName, definition),
  );
}
