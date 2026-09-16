import { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  applicationSettingsApi,
  projectSettingsApi,
  observeSettingsQuery,
  unwrapRpcResult,
} from '@/shared/api';
import type { SettingsScope } from '@/shared/api';

export function settingsApi(scope: SettingsScope) {
  return scope === 'application' ? applicationSettingsApi : projectSettingsApi;
}

export function useSetting(scope: SettingsScope, id: string) {
  const client = useQueryClient();
  const query = useQuery({
    queryKey: ['settings', scope, id],
    queryFn: async () => unwrapRpcResult(await settingsApi(scope).Get(id)),
    staleTime: Infinity,
    retry: false,
  });
  useEffect(
    () => observeSettingsQuery(client, ['settings', scope, id], () => settingsApi(scope).Get(id)),
    [client, scope, id],
  );
  return query;
}

export function useSettingsCatalog(scope: SettingsScope) {
  const client = useQueryClient();
  const query = useQuery({
    queryKey: ['settings', scope],
    queryFn: async () => unwrapRpcResult(await settingsApi(scope).GetAll()),
    staleTime: Infinity,
    retry: false,
  });
  useEffect(
    () => observeSettingsQuery(client, ['settings', scope], () => settingsApi(scope).GetAll()),
    [client, scope],
  );
  return query;
}
