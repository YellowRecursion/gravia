import { useTranslation } from 'react-i18next';
import { AppTitleBar } from './AppTitleBar';
import { ProjectsScreen } from '@/pages/projects';

export function App() {
  const { t } = useTranslation();
  return (
    <main className="app-shell" aria-label={t('applicationName')}>
      <AppTitleBar />
      <ProjectsScreen />
    </main>
  );
}
