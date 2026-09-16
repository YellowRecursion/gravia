import { theme } from 'antd';
import { DockviewReact, themeDark, themeLight } from 'dockview-react';
import type { DockviewReadyEvent } from 'dockview-react';
import { useTranslation } from 'react-i18next';
import { useColorScheme } from '@/shared/ui';
import { FilesPanel, GraphPanel, InspectorPanel, LogsPanel } from './WorkspacePanels';
import { WorkspaceTab } from './WorkspaceTab';
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

  function onReady({ api }: DockviewReadyEvent) {
    // Layout is owned by this mounted screen; no persistence or project document state yet.
    api.addPanel({
      id: 'graph',
      component: 'graph',
      title: t('workspace.graph'),
      minimumWidth: 200,
    });
    api.addPanel({
      id: 'files',
      component: 'files',
      title: t('workspace.files'),
      position: { referencePanel: 'graph', direction: 'left' },
      initialWidth: Math.round(api.width * 0.2),
      minimumWidth: 160,
    });
    api.addPanel({
      id: 'inspector',
      component: 'inspector',
      title: t('workspace.inspector'),
      position: { referencePanel: 'graph', direction: 'right' },
      initialWidth: Math.round(api.width * 0.25),
      minimumWidth: 220,
    });
    api.addPanel({
      id: 'logs',
      component: 'logs',
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
          onReady={onReady}
          theme={colorScheme === 'dark' ? themeDark : themeLight}
          disableFloatingGroups
          getTabContextMenuItems={() => []}
        />
      </div>
      <footer
        className="main-screen__status"
        aria-label={t('workspace.lastLog')}
        style={{ background: token.colorBgLayout, borderColor: token.colorBorderSecondary }}
      />
    </section>
  );
}
