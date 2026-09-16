import { useTranslation } from 'react-i18next';
import type { CSSProperties, ReactNode } from 'react';
import { Button, Tooltip } from 'antd';
import { NameIcon } from '@/shared/ui';
import { getNameColors } from '@/shared/lib';
import './AppTitleBar.css';

interface AppTitleBarProps {
  readonly projectName?: string;
  readonly onChooseProject?: () => void;
  readonly title?: string;
  readonly icon?: ReactNode;
  readonly actions?: ReactNode;
}

export function AppTitleBar({
  projectName,
  onChooseProject,
  title,
  icon,
  actions,
}: AppTitleBarProps) {
  const { t } = useTranslation();
  return (
    <header
      className="app-title-bar"
      data-project={projectName ? 'true' : undefined}
      style={
        projectName
          ? ({ '--project-hue': getNameColors(projectName).hue } as CSSProperties)
          : undefined
      }
    >
      <div className="app-title-bar__safe-area">
        <span className="app-title-bar__title">
          {icon && <span aria-hidden="true">{icon}</span>}
          {title ?? t('applicationName')}
        </span>
        {projectName && (
          <Tooltip title={t('projects.switchProject')}>
            <Button
              type="text"
              size="small"
              className="app-title-bar__project"
              icon={<NameIcon name={projectName} size={20} />}
              aria-label={t('projects.switchToProjects', { name: projectName })}
              onClick={onChooseProject}
            >
              <span className="app-title-bar__project-name">{projectName}</span>
            </Button>
          </Tooltip>
        )}
        {actions && <div className="app-title-bar__actions">{actions}</div>}
      </div>
    </header>
  );
}
