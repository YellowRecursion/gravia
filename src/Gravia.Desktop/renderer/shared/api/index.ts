export { applicationSettingsApi } from './application-settings';
export { observeSettingsQuery } from './settings-sync';
export { projectSettingsApi } from './project-settings';
export { projectsApi } from './projects';
export { currentProjectQuery, recentProjectsQuery } from './project-queries';
export {
  chooseDirectory,
  DesktopUnavailableError,
  setWindowAppearance,
  openSettings,
} from './desktop';
export type { SettingsScope } from './desktop';
export type { ApplicationSettingsApi } from './application-settings';
export type { ProjectSettingsApi } from './project-settings';
export type {
  LocalizedTextDto,
  SettingDto,
  SettingSummaryDto,
  SettingsApi,
} from './settings-contract';
export type { ProjectDto, ProjectsApi } from './projects';
export { fusionHub, fusionPeer } from './fusion';
export { logJournalApi } from './log-journal';
export { useLogJournal, useClearLogJournal } from './use-log-journal';
export { observeLogJournal, logJournalQueryKey } from './log-journal-sync';
export type { LogJournalSnapshot } from './log-journal-sync';
export type { LogEntry, LogState, LogBatch, LogJournalApi } from './log-journal-contract';
export { getRpcFieldErrors, RpcError, unwrapRpcResult } from './rpc-result';
export type { RpcErrorData, RpcFailure, RpcFieldError, RpcResult, RpcSuccess } from './rpc-result';
