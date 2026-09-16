import { useEffect, useState } from 'react';
import { FullscreenExitOutlined, FullscreenOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Dropdown, Flex, Tooltip, Typography } from 'antd';
import type {
  DockviewApi,
  DockviewGroupPanel,
  IDockviewHeaderActionsProps,
  IWatermarkPanelProps,
} from 'dockview-react';
import { useTranslation } from 'react-i18next';
import { addWorkspacePanel, panelKinds, panelTypes } from './panel-types';

function AddPanelButton({
  api,
  group,
  expanded = false,
}: {
  api: DockviewApi;
  group?: DockviewGroupPanel;
  expanded?: boolean;
}) {
  const { t } = useTranslation();
  return (
    <Dropdown
      trigger={['click']}
      menu={{
        items: panelKinds.map((kind) => {
          const Icon = panelTypes[kind].icon;
          return {
            key: kind,
            label: t(`workspace.${kind}`),
            icon: <Icon />,
            onClick: () => addWorkspacePanel(api, kind, t(`workspace.${kind}`), group),
          };
        }),
      }}
    >
      <Button
        type={expanded ? 'primary' : 'text'}
        size={expanded ? 'middle' : 'small'}
        icon={<PlusOutlined />}
        aria-label={t('workspace.addPanel')}
      >
        {expanded ? t('workspace.addPanel') : undefined}
      </Button>
    </Dropdown>
  );
}

export function WorkspaceAddAction({ containerApi, group }: IDockviewHeaderActionsProps) {
  return (
    <div
      className="workspace-header-actions"
      onPointerDown={(event) => event.stopPropagation()}
      onDoubleClick={(event) => event.stopPropagation()}
    >
      <AddPanelButton api={containerApi} group={group} />
    </div>
  );
}

export function WorkspaceMaximizeAction({ api, containerApi }: IDockviewHeaderActionsProps) {
  const { t } = useTranslation();
  const [maximized, setMaximized] = useState(() => api.isMaximized());
  useEffect(() => {
    const subscription = containerApi.onDidMaximizedGroupChange(() =>
      setMaximized(api.isMaximized()),
    );
    return () => subscription.dispose();
  }, [api, containerApi]);
  const title = t(maximized ? 'workspace.restorePanel' : 'workspace.maximizePanel');
  return (
    <div
      className="workspace-header-actions"
      onPointerDown={(event) => event.stopPropagation()}
      onDoubleClick={(event) => event.stopPropagation()}
    >
      <Tooltip title={title}>
        <Button
          type="text"
          size="small"
          aria-label={title}
          icon={maximized ? <FullscreenExitOutlined /> : <FullscreenOutlined />}
          onClick={() => (api.isMaximized() ? api.exitMaximized() : api.maximize())}
        />
      </Tooltip>
    </div>
  );
}

export function WorkspaceEmpty({ containerApi }: IWatermarkPanelProps) {
  const { t } = useTranslation();
  return (
    <Flex vertical gap="middle" align="center" justify="center" style={{ height: '100%' }}>
      <Typography.Text type="secondary">{t('workspace.emptyWorkspace')}</Typography.Text>
      <AddPanelButton api={containerApi} expanded />
    </Flex>
  );
}
