import { useTranslation } from 'react-i18next';
import { Button, Tooltip } from 'antd';
import './AppTitleBar.css';

interface AppTitleBarProps {
  readonly projectName?: string;
  readonly onChooseProject: () => void;
}

export function AppTitleBar({ projectName, onChooseProject }: AppTitleBarProps) {
  const { t } = useTranslation();
  return (
    <header className="app-title-bar">
      <div className="app-title-bar__safe-area">
        <span className="app-title-bar__title">{t('applicationName')}</span>
        {projectName && (
          <Tooltip title={t('projects.switchProject')}>
            <Button
              type="text"
              size="small"
              className="app-title-bar__project"
              aria-label={t('projects.switchToProjects', { name: projectName })}
              onClick={onChooseProject}
            >
              <span className="app-title-bar__project-name">{projectName}</span>
            </Button>
          </Tooltip>
        )}
      </div>
    </header>
  );
}
