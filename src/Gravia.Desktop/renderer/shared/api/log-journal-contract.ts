import type { RpcResult } from './rpc-result';

// Wire contract of Gravia.Application.Logging, served by LogJournalApi.
// Level uses Microsoft.Extensions.Logging.LogLevel numeric values (Trace=0 ... Critical=5).
export interface LogEntry {
  readonly sequence: number;
  readonly timestamp: string;
  readonly level: number;
  readonly category: string;
  readonly message: string;
  readonly eventId: { readonly id: number; readonly name: string | null };
  readonly exception: string | null;
  readonly properties: Readonly<Record<string, string | null>>;
  readonly isTruncated: boolean;
}

export interface LogState {
  readonly journalId: string;
  readonly revision: number;
  readonly firstSequence: number;
  readonly lastSequence: number;
  readonly entryCount: number;
  readonly usedBytes: number;
  readonly capacityBytes: number;
}

export interface LogBatch {
  readonly state: LogState;
  readonly entries: readonly LogEntry[];
  readonly reset: boolean;
}

export interface LogJournalApi {
  GetState(): Promise<RpcResult<LogState>>;
  Read(journalId: string | null, afterSequence: number): Promise<RpcResult<LogBatch>>;
  Clear(): Promise<RpcResult>;
  OpenLocation(currentFile: boolean): Promise<RpcResult>;
}
