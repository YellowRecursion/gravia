import type { PropsWithChildren, ReactNode } from 'react';
import { useContext } from 'react';
import { QuestionCircleOutlined } from '@ant-design/icons';
import { Flex, Form, Tooltip } from 'antd';
import type { FormItemProps } from 'antd';
import { matchesSettingSearch, SettingsSearchContext } from '../model/settings-search';

export function SettingBlock({
  children,
  multiline = false,
  muted = false,
}: PropsWithChildren<{ multiline?: boolean; muted?: boolean }>) {
  return (
    <div
      className={`settings-field-block${multiline ? ' settings-field-block--multiline' : ''}${muted ? ' settings-field-block--muted' : ''}`}
    >
      <div className="settings-field-block__content">{children}</div>
    </div>
  );
}

export function SettingHeader({
  label,
  description,
  action,
}: {
  label: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <Flex className="settings-field-header" align="center" justify="space-between" gap="small">
      <span className="settings-field-header__title">{label}</span>
      <Flex className="settings-field-header__actions" align="center" gap={4}>
        {description && (
          <Tooltip title={description}>
            <span className="settings-field-header__help" tabIndex={0}>
              <QuestionCircleOutlined />
            </span>
          </Tooltip>
        )}
        {action}
      </Flex>
    </Flex>
  );
}

/** One setting, including its label, editor, description and validation feedback. */
export function SettingItem({
  multiline = false,
  action,
  label,
  extra,
  ...props
}: FormItemProps & { action?: ReactNode; multiline?: boolean }) {
  const search = useContext(SettingsSearchContext);
  const searching = Boolean(search.trim());
  const matched = matchesSettingSearch(
    search,
    typeof label === 'string' ? label : '',
    typeof extra === 'string' ? extra : '',
  );
  return (
    <SettingBlock multiline={multiline} muted={searching && !matched}>
      <Form.Item
        {...props}
        className="settings-field-item"
        label={
          <SettingHeader
            label={
              searching && matched ? <mark className="settings-field-match">{label}</mark> : label
            }
            description={extra}
            action={action}
          />
        }
        labelAlign="left"
        layout="horizontal"
        colon={false}
        style={{ ...props.style, marginBottom: 0 }}
      />
    </SettingBlock>
  );
}
