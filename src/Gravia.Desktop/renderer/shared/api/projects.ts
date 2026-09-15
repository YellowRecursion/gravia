import { defineComputeService } from '@actuallab/fusion-rpc';
import { fusionHub, fusionPeer } from './fusion';
import type { RpcResult } from './rpc-result';

export interface ProjectDto {
  readonly name: string;
  readonly path: string;
}

export interface ProjectsApi {
  Create(parentDirectory: string, name: string): Promise<RpcResult<ProjectDto>>;
  Open(path: string): Promise<RpcResult<ProjectDto>>;
  GetCurrent(): Promise<RpcResult<ProjectDto | null>>;
  GetRecent(): Promise<RpcResult<readonly ProjectDto[]>>;
}

const definition = defineComputeService('ProjectsApi', {
  Create: { args: ['', ''], callTypeId: 0 },
  Open: { args: [''], callTypeId: 0 },
  GetCurrent: { args: [], callTypeId: 0 },
  GetRecent: { args: [], callTypeId: 0 },
});

export const projectsApi = fusionHub.addClient<ProjectsApi>(fusionPeer, definition);
