import { queryOptions } from '@tanstack/react-query';
import { projectsApi } from './projects';
import { unwrapRpcResult } from './rpc-result';

export const currentProjectQuery = queryOptions({
  queryKey: ['projects', 'current'],
  queryFn: async () => unwrapRpcResult(await projectsApi.GetCurrent()),
  // The Host restores the session on startup; successful commands update this cache.
  staleTime: Infinity,
  retry: false,
});

export const recentProjectsQuery = queryOptions({
  queryKey: ['projects', 'recent'],
  queryFn: async () => unwrapRpcResult(await projectsApi.GetRecent()),
  retry: false,
});
