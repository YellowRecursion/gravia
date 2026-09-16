import { useState } from 'react';
import { AppstoreOutlined, FolderOutlined, SettingOutlined } from '@ant-design/icons';
import { Button, Dropdown, message } from 'antd';
import { useTranslation } from 'react-i18next';
import { DesktopUnavailableError, openSettings } from '@/shared/api';
import type { SettingsScope } from '@/shared/api';

export function SettingsMenu({ projectAvailable }: { readonly projectAvailable: boolean }) {
  const { t } = useTranslation();
  const [pending, setPending] = useState(false);
  const [messages, contextHolder] = message.useMessage();

  async function open(scope: SettingsScope) {
    if (pending || (scope === 'project' && !projectAvailable)) return;
    setPending(true);
    try {
      await openSettings(scope);
    } catch (error) {
      void messages.error(
        t(
          error instanceof DesktopUnavailableError
            ? 'settings.desktopRequired'
            : 'settings.openError',
        ),
      );
    } finally {
      setPending(false);
    }
  }

  return (
    <>
      {contextHolder}
      <Dropdown
        trigger={['click']}
        placement="bottomRight"
        disabled={pending}
        menu={{
          items: [
            { key: 'application', label: t('settings.application'), icon: <AppstoreOutlined /> },
            {
              key: 'project',
              label: t('settings.project'),
              icon: <FolderOutlined />,
              disabled: !projectAvailable,
            },
          ],
          onClick: ({ key }) => {
            if (key === 'application' || key === 'project') void open(key);
          },
        }}
      >
        <Button
          type="text"
          size="small"
          icon={<SettingOutlined />}
          loading={pending}
          aria-label={t('settings.menu')}
          title={t('settings.menu')}
          aria-haspopup="menu"
        />
      </Dropdown>
    </>
  );
}
