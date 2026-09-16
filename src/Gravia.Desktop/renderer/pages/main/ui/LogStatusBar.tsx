import { theme } from 'antd';
import { useTranslation } from 'react-i18next';
import type { LogEntry } from '@/shared/api';
import { displayMessage } from './log-presentation';
import { LogLevelIcon } from './LogLevelIcon';

export function LogStatusBar({ entry }: { entry?: LogEntry }) {
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
      {entry && (
        <>
          <LogLevelIcon level={entry.level} />
          <span className="main-screen__status-message" title={displayMessage(entry)}>
            {displayMessage(entry)}
          </span>
        </>
      )}
    </footer>
  );
}
