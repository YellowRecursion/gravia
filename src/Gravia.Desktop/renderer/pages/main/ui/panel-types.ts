import {
  ApartmentOutlined,
  FileSearchOutlined,
  FolderOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons';
import type { DockviewApi, DockviewGroupPanel } from 'dockview-react';

export const panelTypes = {
  files: { icon: FolderOutlined, minimumWidth: 160 },
  graph: { icon: ApartmentOutlined, minimumWidth: 200 },
  inspector: { icon: FileSearchOutlined, minimumWidth: 220 },
  logs: { icon: UnorderedListOutlined, minimumWidth: 200 },
};
export type PanelKind = keyof typeof panelTypes;
export const panelKinds = Object.keys(panelTypes) as PanelKind[];
export interface WorkspacePanelParams {
  kind: PanelKind;
}

export function addWorkspacePanel(
  api: DockviewApi,
  kind: PanelKind,
  title: string,
  group?: DockviewGroupPanel,
) {
  return api.addPanel({
    id: `${kind}-${crypto.randomUUID()}`,
    component: kind,
    params: { kind },
    title,
    minimumWidth: panelTypes[kind].minimumWidth,
    position: group ? { referenceGroup: group, direction: 'within' } : undefined,
  });
}
