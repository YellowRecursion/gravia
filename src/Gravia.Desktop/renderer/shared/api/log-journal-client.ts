import { defineComputeService } from '@actuallab/fusion-rpc';
import type { FusionHub } from '@actuallab/fusion-rpc';
import type { RpcClientPeer } from '@actuallab/rpc';
import type { LogJournalApi } from './log-journal-contract';

const definition = defineComputeService('LogJournalApi', {
  GetState: { args: [] },
  Read: { args: [null, 0], callTypeId: 0 },
  Clear: { args: [], callTypeId: 0 },
  OpenLocation: { args: [false], callTypeId: 0 },
});

export function createLogJournalApi(hub: FusionHub, peer: RpcClientPeer): LogJournalApi {
  return hub.addClient<LogJournalApi>(peer, definition);
}
