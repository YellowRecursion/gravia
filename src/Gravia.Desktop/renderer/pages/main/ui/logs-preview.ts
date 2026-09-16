import type { TFunction } from 'i18next';
import type { LogEntry } from '@/shared/api';

// UI fixtures only. Nothing is written to or read from the process journal.
export function createLogsPreview(t: TFunction): LogEntry[] {
  return [
    { level: 2, key: 'opened', category: 'Gravia.Projects', event: 'ProjectOpened' },
    { level: 0, key: 'trace', category: 'Gravia.Preview', event: 'Trace' },
    { level: 1, key: 'debug', category: 'Gravia.Preview', event: 'Debug' },
    { level: 2, key: 'loaded', category: 'Gravia.Modules', event: 'ModuleLoaded' },
    { level: 3, key: 'warning', category: 'Gravia.Preview', event: 'MissingValue' },
    { level: 4, key: 'error', category: 'Gravia.Preview', event: 'PreviewFailed' },
    { level: 4, key: 'error', category: 'Gravia.Preview', event: 'PreviewFailed' },
    { level: 3, key: 'truncated', category: 'Gravia.Preview', event: 'LongMessage' },
    { level: 5, key: 'critical', category: 'Gravia.Preview', event: 'CriticalExample' },
  ].map((item, index) => ({
    sequence: index + 1,
    timestamp: `2026-09-16T10:42:${String(20 + index).padStart(2, '0')}.120Z`,
    level: item.level,
    category: item.category,
    message: t(`logPanel.examples.${item.key}`),
    eventId: { id: 100 + item.level, name: item.event },
    exception:
      item.level === 4
        ? 'Gravia.Preview.PreviewException: Required input is missing.\n   at Gravia.Preview.ValidateInputs()\n   at Gravia.Preview.Run()'
        : null,
    properties: {
      Project: 'Demo refinery',
      Module: 'Preview',
      Object: item.level >= 3 ? 'Feed-01' : null,
    },
    isTruncated: item.key === 'truncated',
  }));
}
