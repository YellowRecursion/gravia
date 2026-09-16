import { useState } from 'react';
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
  ConfigProvider,
  Descriptions,
  Dropdown,
  Flex,
  Input,
  Space,
  Splitter,
  Table,
  Tooltip,
  Typography,
  theme,
} from 'antd';
import { useTranslation } from 'react-i18next';
import type { LogEntry } from '@/shared/api';
import { createLogsPreview } from './logs-preview';
import { selectLog, type LogSelection } from './log-selection';
import { displayMessage, levelNames } from './log-presentation';
import { LogLevelIcon } from './LogLevelIcon';
import './LogsPanel.css';

export function LogsPanel() {
  const { t, i18n } = useTranslation();
  const { token } = theme.useToken();
  const timeOptions = {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  } as const;
  const timeFormatter = new Intl.DateTimeFormat(i18n.language, timeOptions);
  const detailTimeFormatter = new Intl.DateTimeFormat(i18n.language, {
    ...timeOptions,
    fractionalSecondDigits: 3,
  });
  const entries = createLogsPreview(t);
  // Only preview selection is interactive; toolbar commands have no journal connection yet.
  const [selection, setSelection] = useState<LogSelection>({ ids: [6], anchor: 6 });
  const selectedEntries = entries.filter((entry) => selection.ids.includes(entry.sequence));
  const selected = selectedEntries.length === 1 ? selectedEntries[0] : undefined;
  function selectEntry(
    sequence: number,
    modifiers: { shiftKey: boolean; ctrlKey: boolean; metaKey: boolean },
  ) {
    setSelection((previous) =>
      selectLog(
        previous,
        entries.map((entry) => entry.sequence),
        sequence,
        modifiers,
      ),
    );
  }
  const copyText = selectedEntries
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
        event.clipboardData.setData('text/plain', copyText);
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
            />
          </Tooltip>
          <Tooltip title={t('logPanel.collapse')}>
            <Button
              size="small"
              type="text"
              icon={<VerticalAlignMiddleOutlined />}
              aria-label={t('logPanel.collapse')}
            />
          </Tooltip>
          <Tooltip title={t('logPanel.follow')}>
            <Button
              size="small"
              type="text"
              icon={<ArrowDownOutlined />}
              aria-label={t('logPanel.follow')}
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
        />
        <Space.Compact className="logs-panel__levels">
          {levelNames.map((level, index) => (
            <Tooltip key={level} title={t(`logPanel.levels.${level}`)}>
              <Button
                size="small"
                type="text"
                icon={<LogLevelIcon level={index} />}
                aria-label={t(`logPanel.levels.${level}`)}
              >
                {entries.filter((entry) => entry.level === index).length}
              </Button>
            </Tooltip>
          ))}
        </Space.Compact>
        <Dropdown
          trigger={['click']}
          placement="bottomRight"
          menu={{
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
          />
        </Dropdown>
      </div>
      <ConfigProvider theme={{ token: { fontFamily: token.fontFamilyCode } }}>
        <div className="logs-panel__body" style={{ fontFamily: token.fontFamilyCode }}>
          <Splitter orientation="vertical">
            <Splitter.Panel defaultSize="60%" min={60}>
              <div
                className="logs-panel__list"
                onClick={(event) => {
                  if (
                    event.target instanceof Element &&
                    !event.target.closest('.logs-panel__row')
                  ) {
                    setSelection({ ids: [], anchor: null });
                  }
                }}
              >
                <Table<LogEntry>
                  size="small"
                  showHeader={false}
                  pagination={false}
                  rowKey="sequence"
                  tableLayout="fixed"
                  dataSource={entries}
                  rowClassName={(entry) =>
                    selection.ids.includes(entry.sequence)
                      ? 'logs-panel__row logs-panel__row--selected'
                      : 'logs-panel__row'
                  }
                  onRow={(entry) => ({
                    tabIndex: 0,
                    'aria-selected': selection.ids.includes(entry.sequence),
                    onClick: (event) => {
                      if (event.currentTarget instanceof HTMLElement) {
                        event.currentTarget.focus({ preventScroll: true });
                      }
                      selectEntry(entry.sequence, event);
                    },
                    onKeyDown: (event) => {
                      if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        selectEntry(entry.sequence, event);
                      }
                    },
                  })}
                  columns={[
                    {
                      key: 'level',
                      width: 24,
                      render: (_, entry) => <LogLevelIcon level={entry.level} />,
                    },
                    {
                      key: 'message',
                      render: (_, entry) => (
                        <div className="logs-panel__line" title={entry.message}>
                          <span className="logs-panel__time">
                            {timeFormatter.format(new Date(entry.timestamp))}
                          </span>
                          <span className="logs-panel__message">{displayMessage(entry)}</span>
                        </div>
                      ),
                    },
                  ]}
                />
              </div>
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
                              children: `${selected.eventId.name} (${selected.eventId.id})`,
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
