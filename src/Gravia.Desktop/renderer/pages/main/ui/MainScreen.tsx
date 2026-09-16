import { theme } from 'antd';
import { DockviewReact, themeDark, themeLight } from 'dockview-react';
import type { DockviewReadyEvent } from 'dockview-react';
import { useTranslation } from 'react-i18next';
import { useColorScheme } from '@/shared/ui';
import { FilesPanel, GraphPanel, InspectorPanel } from './WorkspacePanels';
import { LogsPanel } from './LogsPanel';
import { LogStatusBar } from './LogStatusBar';
import { createLogsPreview } from './logs-preview';
import { WorkspaceTab } from './WorkspaceTab';
import { WorkspaceAddAction, WorkspaceEmpty, WorkspaceMaximizeAction } from './WorkspaceActions';
import './MainScreen.css';

const components = {
  files: FilesPanel,
  graph: GraphPanel,
  inspector: InspectorPanel,
  logs: LogsPanel,
};

export function MainScreen() {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const colorScheme = useColorScheme();
  // Preview only; the status bar does not read selection or lifecycle state from dock panels.
  const latestLog = createLogsPreview(t).at(-1);

  function onReady({ api }: DockviewReadyEvent) {
    // Layout is owned by this mounted screen; no persistence or project document state yet.
    api.addPanel({
      id: 'graph',
      component: 'graph',
      params: { kind: 'graph' },
      title: t('workspace.graph'),
      minimumWidth: 200,
    });
    api.addPanel({
      id: 'files',
      component: 'files',
      params: { kind: 'files' },
      title: t('workspace.files'),
      position: { referencePanel: 'graph', direction: 'left' },
      initialWidth: Math.round(api.width * 0.2),
      minimumWidth: 160,
    });
    api.addPanel({
      id: 'inspector',
      component: 'inspector',
      params: { kind: 'inspector' },
      title: t('workspace.inspector'),
      position: { referencePanel: 'graph', direction: 'right' },
      initialWidth: Math.round(api.width * 0.25),
      minimumWidth: 220,
    });
    api.addPanel({
      id: 'logs',
      component: 'logs',
      params: { kind: 'logs' },
      title: t('workspace.logs'),
      position: { referencePanel: 'inspector', direction: 'within' },
      inactive: true,
    });
    api.getPanel('graph')?.api.setActive();
  }

  return (
    <section
      className="main-screen"
      aria-label={t('projects.mainScreen')}
      style={{ fontFamily: token.fontFamily }}
    >
      <div className="main-screen__dock">
        <DockviewReact
          components={components}
          defaultTabComponent={WorkspaceTab}
          leftHeaderActionsComponent={WorkspaceAddAction}
          rightHeaderActionsComponent={WorkspaceMaximizeAction}
          watermarkComponent={WorkspaceEmpty}
          onReady={onReady}
          theme={colorScheme === 'dark' ? themeDark : themeLight}
          disableFloatingGroups
          getTabContextMenuItems={() => []}
        />
      </div>
      <LogStatusBar entry={latestLog} />
    </section>
  );
}
