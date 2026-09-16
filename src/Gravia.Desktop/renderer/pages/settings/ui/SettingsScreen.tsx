import { useState } from 'react';
import { Alert, Button, Empty, Flex, Form, Spin, Splitter, Tree, Typography, theme } from 'antd';
import { useTranslation } from 'react-i18next';
import { useSettingsCatalog } from '@/entities/settings';
import type { SettingsScope } from '@/shared/api';
import {
  buildCategories,
  filterCategories,
  findCategory,
  localizedText,
} from '../model/categories';
import { matchesSettingSearch, SettingsSearchContext } from '../model/settings-search';
import { SettingField } from './SettingField';
import { MockSettings } from './mock/MockSettings';
import { mockSettingsKey, mockSettingsSections, mockSectionMatches } from '../model/mock-sections';
import './SettingsScreen.css';

export function SettingsScreen({ scope, search = '' }: { scope: SettingsScope; search?: string }) {
  const { t, i18n } = useTranslation();
  const { token } = theme.useToken();
  const query = useSettingsCatalog(scope);
  // Selection belongs to this window only. Host responses stay in Query, not in form state.
  const [selectedKey, setSelectedKey] = useState<string>();
  const language = i18n.resolvedLanguage ?? 'ru';
  const categories = buildCategories(query.data ?? [], language);
  if (scope === 'application') {
    categories.push({
      key: mockSettingsKey,
      title: t('settings.mock.title'),
      settings: [],
      children: mockSettingsSections.map((section) => ({
        key: `${mockSettingsKey}/${section}`,
        title: t(`settings.mock.sections.${section}`),
        settings: [],
        children: [],
      })),
    });
  }
  const visibleCategories = !search.trim()
    ? categories
    : filterCategories(categories, (category) => {
        const section = mockSettingsSections.find(
          (item) => category.key === `${mockSettingsKey}/${item}`,
        );
        if (section) return mockSectionMatches(section, search, t);
        return category.settings.some((setting) =>
          matchesSettingSearch(
            search,
            localizedText(setting.title, language),
            setting.description ? localizedText(setting.description, language) : '',
          ),
        );
      });
  const selected = findCategory(visibleCategories, selectedKey) ?? visibleCategories[0];
  const mockKey = selected?.key === mockSettingsKey ? selected.children[0]?.key : selected?.key;
  const mockSection = selected?.key.startsWith(mockSettingsKey)
    ? (mockSettingsSections.find((section) => mockKey === `${mockSettingsKey}/${section}`) ??
      'basic')
    : null;

  if (query.isPending)
    return (
      <Flex className="settings-screen__state" align="center" justify="center">
        <Spin aria-label={t('settings.loading')} />
      </Flex>
    );
  if (query.isError)
    return (
      <Alert
        type="error"
        showIcon
        title={t('settings.loadError')}
        action={<Button onClick={() => void query.refetch()}>{t('projects.retry')}</Button>}
      />
    );
  if (!categories.length)
    return (
      <Flex className="settings-screen__state" align="center" justify="center">
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('settings.empty')} />
      </Flex>
    );

  return (
    <SettingsSearchContext.Provider value={search}>
      <section className="settings-screen" style={{ background: token.colorBgContainer }}>
        <Splitter>
          <Splitter.Panel defaultSize={280} min={200} max="45%">
            <nav className="settings-screen__categories" aria-label={t('settings.categories')}>
              {visibleCategories.length ? (
                <Tree
                  key={search.trim().toLocaleLowerCase()}
                  blockNode
                  defaultExpandAll
                  treeData={visibleCategories}
                  selectedKeys={selected ? [selected.key] : []}
                  onSelect={(keys) => {
                    if (keys[0] !== undefined) setSelectedKey(String(keys[0]));
                  }}
                />
              ) : (
                <Empty
                  image={Empty.PRESENTED_IMAGE_SIMPLE}
                  description={t('settings.noCategoriesFound')}
                />
              )}
            </nav>
          </Splitter.Panel>
          <Splitter.Panel min={260}>
            <section className="settings-screen__fields" aria-label={selected?.title}>
              <Typography.Title level={4} style={{ marginTop: 0 }}>
                {selected?.title}
              </Typography.Title>
              {!selected ? (
                <Empty
                  image={Empty.PRESENTED_IMAGE_SIMPLE}
                  description={t('settings.noCategoriesFound')}
                />
              ) : mockSection ? (
                <MockSettings section={mockSection} />
              ) : (
                <Form layout="vertical" requiredMark={false}>
                  {selected?.settings.map((summary) => (
                    <SettingField key={`${scope}:${summary.id}`} scope={scope} summary={summary} />
                  ))}
                </Form>
              )}
            </section>
          </Splitter.Panel>
        </Splitter>
      </section>
    </SettingsSearchContext.Provider>
  );
}
