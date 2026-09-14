import { Computed } from '@actuallab/fusion';
import type { QueryClient } from '@tanstack/react-query';
import type { LogBatch, LogEntry, LogJournalApi, LogState } from './log-journal-contract';
import type { RpcResult } from './rpc-result';

export const logJournalQueryKey = ['log-journal'] as const;

export interface LogJournalSnapshot {
  readonly state: LogState | null;
  readonly entries: readonly LogEntry[];
  readonly error: string | null;
}

export function applyLogBatch(
  previous: LogJournalSnapshot | undefined,
  batch: LogBatch,
): LogJournalSnapshot {
  const sameJournal = previous?.state?.journalId === batch.state.journalId;
  if (sameJournal && previous.state && previous.state.revision > batch.state.revision)
    return previous;
  let retained: readonly LogEntry[] = [];
  if (sameJournal && !batch.reset && previous) {
    // Binary search skips evicted records without scanning unchanged history on every update.
    let start = 0;
    let end = previous.entries.length;
    while (start < end) {
      const middle = (start + end) >>> 1;
      if (previous.entries[middle]!.sequence < batch.state.firstSequence) start = middle + 1;
      else end = middle;
    }
    retained = start === 0 ? previous.entries : previous.entries.slice(start);
  }
  const last = retained.at(-1)?.sequence ?? 0;
  const additions = batch.entries.filter((entry) => entry.sequence > last);
  return { state: batch.state, entries: retained.concat(additions), error: null };
}

function unwrap<T>(result: RpcResult<T>): T {
  if (!result.isSuccess) throw new Error(result.errorMessage);
  return result.data as T;
}

// RPC can remain pending across reconnects. Stopping observation must not wait for it,
// and a late reply must never repopulate a released Query cache.
function abortable<T>(promise: Promise<T>, signal: AbortSignal): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const cancel = () => {
      reject(new Error('Log journal observation stopped.'));
    };
    signal.addEventListener('abort', cancel, { once: true });
    if (signal.aborted) cancel();
    void promise.then(resolve, reject).finally(() => {
      signal.removeEventListener('abort', cancel);
    });
  });
}

const subscriptions = new WeakMap<
  QueryClient,
  { api: LogJournalApi; users: number; controller: AbortController; completion: Promise<void> }
>();

/**
 * Start on Console mount, stop on unmount. Multiple consumers share one reader and Query cache.
 * The journal belongs to the Host process, not the open project. No UI or background subscription
 * is installed merely by importing this module.
 */
export function observeLogJournal(queryClient: QueryClient, api: LogJournalApi) {
  let subscription = subscriptions.get(queryClient);
  if (subscription && subscription.api !== api)
    throw new Error('This QueryClient already observes another log journal API.');
  if (!subscription) {
    queryClient.setQueryDefaults(logJournalQueryKey, {
      structuralSharing: false,
      staleTime: Infinity,
      // Observation owns this cache even without a mounted React Query consumer.
      // The last stop() removes it explicitly.
      gcTime: Infinity,
    });
    const controller = new AbortController();
    const completion = synchronize(queryClient, api, controller.signal);
    subscription = { api, users: 0, controller, completion };
    subscriptions.set(queryClient, subscription);
  }
  subscription.users++;
  const active = subscription;
  let released = false;
  return {
    completion: active.completion,
    stop() {
      if (released) return;
      released = true;
      if (--active.users > 0) return;
      active.controller.abort();
      subscriptions.delete(queryClient);
      queryClient.removeQueries({ queryKey: logJournalQueryKey, exact: true });
    },
  };
}

async function synchronize(queryClient: QueryClient, api: LogJournalApi, signal: AbortSignal) {
  let retryDelay = 100;
  while (!signal.aborted) {
    let computed: Computed<RpcResult<LogState>> | undefined;
    try {
      computed = await abortable(
        Computed.capture(() => api.GetState()),
        signal,
      );
      const state = unwrap(computed.value);
      const current = queryClient.getQueryData<LogJournalSnapshot>(logJournalQueryKey);
      if (
        !current?.state ||
        current.error ||
        current.state.journalId !== state.journalId ||
        current.state.revision !== state.revision
      ) {
        const batch = unwrap(
          await abortable(
            api.Read(current?.state?.journalId ?? null, current?.state?.lastSequence ?? 0),
            signal,
          ),
        );
        if (signal.aborted) return;
        queryClient.setQueryData<LogJournalSnapshot>(logJournalQueryKey, (previous) =>
          applyLogBatch(previous, batch),
        );
      }
      retryDelay = 100;
      // Invalidation during Read is remembered by Computed, so no change notification is lost.
      await computed.whenInvalidated(signal);
    } catch (error) {
      if (signal.aborted) return;
      computed?.invalidate();
      queryClient.setQueryData<LogJournalSnapshot>(logJournalQueryKey, (previous) => ({
        state: previous?.state ?? null,
        entries: previous?.entries ?? [],
        error: error instanceof Error ? error.message : String(error),
      }));
      // Only failures use a retry delay; healthy updates have no polling interval.
      await new Promise<void>((resolve) => {
        const done = () => {
          clearTimeout(timer);
          signal.removeEventListener('abort', done);
          resolve();
        };
        const timer = setTimeout(done, retryDelay);
        signal.addEventListener('abort', done, { once: true });
        if (signal.aborted) done();
      });
      retryDelay = Math.min(retryDelay * 2, 5000);
    }
  }
}
