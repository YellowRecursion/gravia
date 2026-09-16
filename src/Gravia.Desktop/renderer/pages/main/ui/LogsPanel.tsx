import { useDeferredValue, useEffect, useMemo, useState, useSyncExternalStore } from 'react';
import { useMutation } from '@tanstack/react-query';
import {
  ArrowDownOutlined,
  ClearOutlined,
  FileTextOutlined,
  FolderOpenOutlined,
  MoreOutlined,
  SearchOutlined,
  VerticalAlignMiddleOutlined,
} from '@ant-design/icons';
import {
  Button,
  Alert,
  ConfigProvider,
  Descriptions,
  Dropdown,
  Flex,
  Input,
  Space,
  Splitter,
  Tooltip,
  Typography,
  theme,
} from 'antd';
import { useTranslation } from 'react-i18next';
import type { IDockviewPanelProps } from 'dockview-react';
import { logJournalApi, unwrapRpcResult, useClearLogJournal, useLogJournal } from '@/shared/api';
import { createLogViewStore } from './log-projection';
import { VirtualLogList } from './VirtualLogList';
import { selectLog, type LogSelection } from './log-selection';
import { displayMessage, levelNames } from './log-presentation';
import { LogLevelIcon } from './LogLevelIcon';
import type { WorkspacePanelParams } from './panel-types';
import './LogsPanel.css';

export function LogsPanel({ params }: IDockviewPanelProps<WorkspacePanelParams>) {
  const reveal = params.revealLog;
  const [handledReveal, setHandledReveal] = useState<string>();
  const { t, i18n } = useTranslation();
  const { token } = theme.useToken();
  const [search, setSearch] = useState('');
  const deferredSearch = useDeferredValue(search);
  const [levels, setLevels] = useState(63);
  const [collapse, setCollapse] = useState(false);
  const [follow, setFollow] = useState(true);
  const snapshot = useLogJournal();
  const viewStore = useMemo(
    () => createLogViewStore({ search: deferredSearch, levels, collapse }),
    [deferredSearch, levels, collapse],
  );
  const view = useSyncExternalStore(viewStore.subscribe, viewStore.getSnapshot);
  useEffect(() => {
    if (snapshot) viewStore.offer(snapshot);
  }, [viewStore, snapshot]);
  const rows = view?.rows ?? [];
  const clear = useClearLogJournal();
  const openLocation = useMutation({
    mutationFn: async (currentFile: boolean) =>
      unwrapRpcResult(await logJournalApi.OpenLocation(currentFile)),
  });
  const timeOptions = {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  } as const;
  const timeFormatter = useMemo(
    () =>
      new Intl.DateTimeFormat(i18n.language, {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hourCycle: 'h23',
      }),
    [i18n.language],
  );
  const detailTimeFormatter = new Intl.DateTimeFormat(i18n.language, {
    ...timeOptions,
    fractionalSecondDigits: 3,
  });
  const [storedSelection, setSelection] = useState<{ journalId?: string; value: LogSelection }>({
    value: { ids: [], anchor: null },
  });
  // A navigation request belongs to this panel only and is consumed once, including repeat clicks.
  if (reveal && reveal.requestId !== handledReveal) {
    setHandledReveal(reveal.requestId);
    setSearch('');
    setLevels(63);
    setCollapse(false);
    setFollow(false);
    setSelection({
      journalId: reveal.journalId,
      value: { ids: [reveal.sequence], anchor: reveal.sequence },
    });
  }
  const selection =
    storedSelection.journalId === view?.journalId
      ? storedSelection.value
      : { ids: [], anchor: null };
  const wanted = new Set(selection.ids);
  const selectedRows = wanted.size ? rows.filter((row) => wanted.has(row.id)) : [];
  const selectedIds = new Set(selectedRows.map((row) => row.id));
  const selectedEntries = selectedRows.map((row) => row.entry);
  const selected = selectedEntries.length === 1 ? selectedEntries[0] : undefined;
  function selectEntry(
    sequence: number,
    modifiers: { shiftKey: boolean; ctrlKey: boolean; metaKey: boolean },
  ) {
    setSelection({
      journalId: view?.journalId,
      value: selectLog(
        { ids: [...selectedIds], anchor: selection.anchor },
        rows.map((row) => row.id),
        sequence,
        modifiers,
      ),
    });
  }
  const copyText = () =>
    selectedEntries
      .map((entry) =>
        [
          `${t(`logPanel.levels.${levelNames[entry.level]}`)} · ${detailTimeFormatter.format(new Date(entry.timestamp))}`,
          displayMessage(entry),
          '',
          `${t('logPanel.source')}: ${entry.category}`,
          `${t('logPanel.event')}: ${entry.eventId.name ?? ''} (${entry.eventId.id})`,
          ...(entry.exception ? ['', `${t('logPanel.exception')}:`, entry.exception] : []),
          '',
          `${t('logPanel.properties')}:`,
          ...Object.entries(entry.properties).map(([key, value]) => `${key}: ${value ?? 'null'}`),
        ].join('\n'),
      )
      .join('\n\n────────────\n\n');
  return (
    <section
      className="workspace-panel logs-panel"
      aria-label={t('workspace.logs')}
      tabIndex={-1}
      onCopy={(event) => {
        const target = event.target;
        if (
          event.defaultPrevented ||
          selectedEntries.length === 0 ||
          (target instanceof Element && target.closest('input, textarea, [contenteditable]')) ||
          window.getSelection()?.toString()
        ) {
          return;
        }
        event.clipboardData.setData('text/plain', copyText());
        event.preventDefault();
        event.stopPropagation();
      }}
    >
      <div className="logs-panel__toolbar">
        <Space.Compact>
          <Tooltip title={t('logPanel.clear')}>
            <Button
              size="small"
              type="text"
              icon={<ClearOutlined />}
              aria-label={t('logPanel.clear')}
              loading={clear.isPending}
              onClick={() => clear.mutate()}
            />
          </Tooltip>
          <Tooltip title={t('logPanel.collapse')}>
            <Button
              size="small"
              type="text"
              icon={<VerticalAlignMiddleOutlined />}
              aria-label={t('logPanel.collapse')}
              aria-pressed={collapse}
              onClick={() => setCollapse((value) => !value)}
              className={collapse ? 'logs-panel__toggle--active' : undefined}
            />
          </Tooltip>
          <Tooltip title={t('logPanel.follow')}>
            <Button
              size="small"
              type="text"
              icon={<ArrowDownOutlined />}
              aria-label={t('logPanel.follow')}
              aria-pressed={follow}
              onClick={() => setFollow((value) => !value)}
              className={follow ? 'logs-panel__toggle--active' : undefined}
            />
          </Tooltip>
        </Space.Compact>
        <Input
          className="logs-panel__search"
          size="small"
          prefix={<SearchOutlined />}
          placeholder={t('logPanel.search')}
          aria-label={t('logPanel.search')}
          allowClear
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <Space.Compact className="logs-panel__levels">
          {levelNames.map((level, index) => (
            <Tooltip key={level} title={t(`logPanel.levels.${level}`)}>
              <Button
                size="small"
                type="text"
                icon={<LogLevelIcon level={index} />}
                aria-label={t(`logPanel.levels.${level}`)}
                aria-pressed={(levels & (1 << index)) !== 0}
                onClick={() => setLevels((value) => value ^ (1 << index))}
                className={(levels & (1 << index)) === 0 ? 'logs-panel__level--off' : undefined}
              >
                {view?.counts[index] ?? 0}
              </Button>
            </Tooltip>
          ))}
        </Space.Compact>
        <Dropdown
          trigger={['click']}
          placement="bottomRight"
          menu={{
            onClick: ({ key }) => openLocation.mutate(key === 'file'),
            items: [
              { key: 'folder', icon: <FolderOpenOutlined />, label: t('logPanel.openFolder') },
              { key: 'file', icon: <FileTextOutlined />, label: t('logPanel.openCurrentFile') },
            ],
          }}
        >
          <Button
            size="small"
            type="text"
            icon={<MoreOutlined />}
            aria-label={t('logPanel.more')}
            title={t('logPanel.more')}
            loading={openLocation.isPending}
          />
        </Dropdown>
      </div>
      {(view?.error || clear.isError || openLocation.isError) && (
        <Alert
          type="error"
          showIcon
          title={t(
            clear.isError
              ? 'logPanel.clearError'
              : openLocation.isError
                ? 'logPanel.openError'
                : 'logPanel.loadError',
          )}
        />
      )}
      <ConfigProvider theme={{ token: { fontFamily: token.fontFamilyCode } }}>
        <div className="logs-panel__body" style={{ fontFamily: token.fontFamilyCode }}>
          <Splitter orientation="vertical">
            <Splitter.Panel defaultSize="60%" min={60}>
              <VirtualLogList
                rows={rows}
                reveal={
                  reveal?.journalId === view?.journalId &&
                  !collapse &&
                  deferredSearch === '' &&
                  levels === 63
                    ? reveal
                    : undefined
                }
                selected={selectedIds}
                follow={follow}
                onFollow={setFollow}
                onSelect={selectEntry}
                onClearSelection={() =>
                  setSelection({ journalId: view?.journalId, value: { ids: [], anchor: null } })
                }
                timeFormatter={timeFormatter}
                label={t('workspace.logs')}
                emptyText={t(!view ? 'logPanel.loading' : 'logPanel.empty')}
              />
            </Splitter.Panel>
            <Splitter.Panel min={80} collapsible>
              <div className="logs-panel__details" aria-label={t('logPanel.details')}>
                {selectedEntries.length > 0 && (
                  <>
                    <Flex align="center" gap="small" wrap>
                      {selected ? (
                        <>
                          <LogLevelIcon level={selected.level} />
                          <Typography.Text strong>
                            {t(`logPanel.levels.${levelNames[selected.level]}`)}
                          </Typography.Text>
                          <Typography.Text type="secondary">
                            {detailTimeFormatter.format(new Date(selected.timestamp))}
                          </Typography.Text>
                        </>
                      ) : (
                        <Typography.Text strong>
                          {t('logPanel.selectionCount', { count: selectedEntries.length })}
                        </Typography.Text>
                      )}
                      {selectedEntries.length > 0 && (
                        <Typography.Text
                          key={selectedEntries.map((entry) => entry.sequence).join(',')}
                          className="logs-panel__copy"
                          copyable={{
                            text: copyText,
                            tooltips: [
                              t(selected ? 'logPanel.copy' : 'logPanel.copySelected'),
                              t('logPanel.copied'),
                            ],
                          }}
                        />
                      )}
                    </Flex>
                    {selected ? (
                      <>
                        <Typography.Paragraph className="logs-panel__full-message">
                          {displayMessage(selected)}
                        </Typography.Paragraph>
                        <Descriptions
                          size="small"
                          column={1}
                          colon={false}
                          items={[
                            {
                              key: 'source',
                              label: t('logPanel.source'),
                              children: selected.category,
                            },
                            {
                              key: 'event',
                              label: t('logPanel.event'),
                              children: `${selected.eventId.name ?? ''} (${selected.eventId.id})`,
                            },
                          ]}
                        />
                        {selected.exception && (
                          <div className="logs-panel__detail-section">
                            <Typography.Text type="secondary">
                              {t('logPanel.exception')}
                            </Typography.Text>
                            <pre className="logs-panel__exception">{selected.exception}</pre>
                          </div>
                        )}
                        <div className="logs-panel__detail-section">
                          <Typography.Text type="secondary">
                            {t('logPanel.properties')}
                          </Typography.Text>
                          <Descriptions
                            size="small"
                            column={1}
                            colon={false}
                            items={Object.entries(selected.properties).map(([key, value]) => ({
                              key,
                              label: key,
                              children: value ?? 'null',
                            }))}
                          />
                        </div>
                      </>
                    ) : (
                      <Flex gap="middle" wrap>
                        {levelNames.map((level, index) => {
                          const count = selectedEntries.filter(
                            (entry) => entry.level === index,
                          ).length;
                          return count > 0 ? (
                            <Flex key={level} align="center" gap="small">
                              <LogLevelIcon level={index} />
                              <Typography.Text>
                                {t(`logPanel.levels.${level}`)}: {count}
                              </Typography.Text>
                            </Flex>
                          ) : null;
                        })}
                      </Flex>
                    )}
                  </>
                )}
              </div>
            </Splitter.Panel>
          </Splitter>
        </div>
      </ConfigProvider>
    </section>
  );
}
