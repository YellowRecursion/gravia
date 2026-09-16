import { theme } from 'antd';
import { useTranslation } from 'react-i18next';
import { useLogJournal } from '@/shared/api';
import type { LogJournalSnapshot } from '@/shared/api';
import { displayMessage } from './log-presentation';
import { LogLevelIcon } from './LogLevelIcon';
import type { LogTarget } from './panel-types';

const selectLatest = (snapshot: LogJournalSnapshot) => ({
  journalId: snapshot.state?.journalId,
  entry: snapshot.entries.at(-1),
  error: snapshot.error,
});

export function LogStatusBar({ onOpenLogs }: { onOpenLogs: (target: LogTarget) => void }) {
  // Independent consumer: closing every dock panel must not stop status updates.
  const latest = useLogJournal(selectLatest);
  const entry = latest?.entry;
  const { t } = useTranslation();
  const { token } = theme.useToken();
  return (
    <footer
      className="main-screen__status"
      aria-label={t('workspace.lastLog')}
      style={{
        background: token.colorBgLayout,
        borderColor: token.colorBorderSecondary,
        fontFamily: token.fontFamily,
        color: token.colorTextSecondary,
      }}
    >
      {latest?.error ? (
        <span role="status">{t('logPanel.loadError')}</span>
      ) : (
        entry && (
          <>
            <LogLevelIcon level={entry.level} />
            <button
              type="button"
              className="main-screen__status-message"
              title={displayMessage(entry)}
              onClick={() => {
                if (latest?.journalId)
                  onOpenLogs({ journalId: latest.journalId, sequence: entry.sequence });
              }}
            >
              {displayMessage(entry)}
            </button>
          </>
        )
      )}
    </footer>
  );
}
