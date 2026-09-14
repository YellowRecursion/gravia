import { fusionHub, fusionPeer } from './fusion';
import { createLogJournalApi } from './log-journal-client';

export const logJournalApi = createLogJournalApi(fusionHub, fusionPeer);
