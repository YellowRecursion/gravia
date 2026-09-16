import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  currentProjectQuery,
  recentProjectsQuery,
  projectsApi,
  unwrapRpcResult,
} from '@/shared/api';

type ProjectOperation =
  | { readonly kind: 'open'; readonly path: string }
  | { readonly kind: 'create'; readonly parentDirectory: string; readonly name: string };

export function useProjectSession() {
  const client = useQueryClient();
  const current = useQuery(currentProjectQuery);
  // Navigation belongs to the app and resets after a successful project command.
  const [choosingProject, setChoosingProject] = useState(false);
  const running = useRef(false);
  const operation = useMutation({
    mutationFn: async (request: ProjectOperation) =>
      unwrapRpcResult(
        await (request.kind === 'open'
          ? projectsApi.Open(request.path)
          : projectsApi.Create(request.parentDirectory, request.name)),
      ),
    retry: false,
    onSuccess: (project) => {
      client.setQueryData(currentProjectQuery.queryKey, project);
      void client.invalidateQueries(recentProjectsQuery);
      setChoosingProject(false);
    },
  });

  async function perform(request: ProjectOperation) {
    if (running.current) return;
    if (request.kind === 'open' && request.path === current.data?.path) {
      setChoosingProject(false);
      return;
    }
    running.current = true;
    try {
      await operation.mutateAsync(request);
    } finally {
      running.current = false;
    }
  }

  return {
    current,
    showProjects: choosingProject || current.data === null,
    isPending: operation.isPending,
    chooseProject: () => {
      if (!running.current) setChoosingProject((choosing) => !choosing);
    },
    openProject: (path: string) => perform({ kind: 'open', path }),
    createProject: (parentDirectory: string, name: string) =>
      perform({ kind: 'create', parentDirectory, name }),
  };
}
