import { DockviewDefaultTab } from 'dockview-react';
import type { IDockviewPanelHeaderProps } from 'dockview-react';
import { useTranslation } from 'react-i18next';
import { useEffect } from 'react';

export function WorkspaceTab(props: IDockviewPanelHeaderProps) {
  const { t } = useTranslation();
  useEffect(() => {
    props.api.setTitle(t(`workspace.${props.api.id}`));
  }, [props.api, t]);
  // No close action until there is a way to reopen tool panels.
  return <DockviewDefaultTab {...props} hideClose />;
}
