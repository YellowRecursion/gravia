import { useTranslation } from 'react-i18next';
import { Alert, Button, Flex, Spin } from 'antd';
import { AppTitleBar } from './AppTitleBar';
import { useProjectSession } from '../model/use-project-session';
import { ProjectsScreen } from '@/pages/projects';

export function App() {
  const { t } = useTranslation();
  const session = useProjectSession();
  return (
    <main className="app-shell" aria-label={t('applicationName')}>
      <AppTitleBar
        projectName={session.current.data?.name}
        onChooseProject={session.chooseProject}
      />
      {session.current.isPending ? (
        <Flex flex={1} align="center" justify="center">
          <Spin aria-label={t('projects.loading')} />
        </Flex>
      ) : session.current.isError ? (
        <Alert
          type="error"
          showIcon
          title={t('projects.sessionError')}
          action={
            <Button onClick={() => void session.current.refetch()}>{t('projects.retry')}</Button>
          }
        />
      ) : session.showProjects ? (
        <ProjectsScreen
          currentProjectPath={session.current.data?.path}
          pending={session.isPending}
          onOpenProject={session.openProject}
          onCreateProject={session.createProject}
        />
      ) : (
        <div style={{ flex: 1, minHeight: 0 }} aria-label={t('projects.mainScreen')} />
      )}
    </main>
  );
}
