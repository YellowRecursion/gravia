import { Background, BackgroundVariant, ReactFlow } from '@xyflow/react';
import { Empty, Flex, Typography, theme } from 'antd';
import { useTranslation } from 'react-i18next';
import { useColorScheme } from '@/shared/ui';

export function FilesPanel() {
  const { t } = useTranslation();
  return (
    <div className="workspace-panel" aria-label={t('workspace.files')}>
      <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('workspace.filesPlaceholder')} />
    </div>
  );
}

export function GraphPanel() {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const colorScheme = useColorScheme();
  return (
    <div className="workspace-graph" aria-label={t('workspace.graph')}>
      <ReactFlow
        colorMode={colorScheme}
        nodesDraggable={false}
        nodesConnectable={false}
        elementsSelectable={false}
        style={{ background: 'var(--dv-group-view-background-color)' }}
      >
        <Background variant={BackgroundVariant.Dots} gap={20} size={1} color={token.colorBorder} />
      </ReactFlow>
    </div>
  );
}

export function InspectorPanel() {
  const { t } = useTranslation();
  return (
    <Flex
      className="workspace-panel workspace-panel--empty"
      align="center"
      justify="center"
      aria-label={t('workspace.inspector')}
    >
      <Typography.Text type="secondary">{t('workspace.inspectorPlaceholder')}</Typography.Text>
    </Flex>
  );
}
