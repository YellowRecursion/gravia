import { useEffect, useState } from 'react';
import { SearchOutlined, SettingOutlined } from '@ant-design/icons';
import { Input } from 'antd';
import { useTranslation } from 'react-i18next';
import type { SettingsScope } from '@/shared/api';
import { SettingsScreen } from '@/pages/settings';
import { AppTitleBar } from './AppTitleBar';

export function SettingsWindow({ scope }: { readonly scope: SettingsScope }) {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');
  const title = t(`settings.${scope}`);
  useEffect(() => {
    document.title = `${title} — ${t('applicationName')}`;
  }, [title, t]);
  return (
    <main className="app-shell settings-window" aria-label={title}>
      <AppTitleBar
        title={title}
        icon={<SettingOutlined />}
        actions={
          <Input
            className="settings-window__search"
            size="small"
            prefix={<SearchOutlined />}
            placeholder={t('settings.search')}
            aria-label={t('settings.search')}
            allowClear
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        }
      />
      <section
        className="settings-window__content"
        aria-label={title}
        style={{ flex: 1, minHeight: 0 }}
      >
        <SettingsScreen key={scope} scope={scope} search={search} />
      </section>
    </main>
  );
}
