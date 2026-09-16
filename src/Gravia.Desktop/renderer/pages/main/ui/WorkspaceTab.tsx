import { DockviewDefaultTab } from 'dockview-react';
import type { IDockviewPanelHeaderProps } from 'dockview-react';
import { useTranslation } from 'react-i18next';
import { useEffect } from 'react';
import { panelTypes } from './panel-types';
import type { PanelKind, WorkspacePanelParams } from './panel-types';

export function WorkspaceTab(props: IDockviewPanelHeaderProps<WorkspacePanelParams>) {
  const { t } = useTranslation();
  const kind = props.params?.kind ?? (props.api.id as PanelKind);
  const Icon = panelTypes[kind].icon;
  useEffect(() => {
    props.api.setTitle(t(`workspace.${kind}`));
  }, [props.api, kind, t]);
  return (
    <div className="workspace-tab-title">
      <Icon />
      <DockviewDefaultTab
        {...props}
        onDoubleClick={() => {
          if (props.api.isMaximized()) props.api.exitMaximized();
          else props.api.maximize();
        }}
      />
    </div>
  );
}
