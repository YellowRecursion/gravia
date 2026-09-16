import { useEffect } from 'react';
import { skipToken, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { logJournalApi } from './log-journal';
import { logJournalQueryKey, observeLogJournal } from './log-journal-sync';
import type { LogJournalSnapshot } from './log-journal-sync';
import { unwrapRpcResult } from './rpc-result';

export function useLogJournal<T = LogJournalSnapshot>(
  select?: (snapshot: LogJournalSnapshot) => T,
) {
  const client = useQueryClient();
  const query = useQuery<LogJournalSnapshot, Error, T>({
    queryKey: logJournalQueryKey,
    // Fusion owns fetching/invalidation; Query owns the shared immutable snapshot.
    enabled: false,
    queryFn: skipToken,
    staleTime: Infinity,
    gcTime: Infinity,
    structuralSharing: false,
    select,
  });
  useEffect(() => {
    const observation = observeLogJournal(client, logJournalApi);
    return () => observation.stop();
  }, [client]);
  return query.data;
}

export function useClearLogJournal() {
  return useMutation({
    mutationFn: async () => unwrapRpcResult(await logJournalApi.Clear()),
    // No optimistic local clear: every consumer follows the backend's authoritative bounds.
  });
}
