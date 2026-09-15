import { useTranslation } from 'react-i18next';
import './AppTitleBar.css';

export function AppTitleBar() {
  const { t } = useTranslation();
  return (
    <header className="app-title-bar">
      <div className="app-title-bar__safe-area">
        <span className="app-title-bar__title">{t('applicationName')}</span>
      </div>
    </header>
  );
}
