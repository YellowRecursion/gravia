import type { LogEntry } from '@/shared/api';

export const levelNames = [
  'trace',
  'debug',
  'information',
  'warning',
  'error',
  'critical',
] as const;

export function displayMessage(entry: LogEntry) {
  return entry.isTruncated
    ? `${entry.message.trimEnd().replace(/(?:…|\.{3})$/, '')}…`
    : entry.message;
}
