import { useState } from 'react';
import { UndoOutlined } from '@ant-design/icons';
import { Button, Flex, Form, Tooltip, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { createValueDraft, readValueDraft } from '@/shared/lib';
import { ValueClearButton, ValueEditor } from '@/shared/ui';
import { nullableExamples } from '../../model/nullable-examples';
import type { NullableExample } from '../../model/nullable-examples';
import { SettingItem } from '../SettingItem';

export function MockNullable() {
  const { t } = useTranslation();
  const [revision, setRevision] = useState(0);
  return (
    <div className="settings-mock" data-mock-section="nullable">
      <Flex vertical gap="small" style={{ marginBottom: 24 }}>
        <Typography.Text type="secondary">{t('settings.mock.notice')}</Typography.Text>
        <Button
          icon={<UndoOutlined />}
          style={{ alignSelf: 'flex-start' }}
          onClick={() => setRevision((value) => value + 1)}
        >
          {t('settings.mock.reset')}
        </Button>
      </Flex>
      <Form component="div" layout="vertical" requiredMark={false}>
        {nullableExamples.map((example) => (
          <NullableField key={`${revision}:${example.id}`} example={example} />
        ))}
      </Form>
    </div>
  );
}

function NullableField({ example }: { example: NullableExample }) {
  const { t } = useTranslation();
  const [draft, setDraft] = useState(() =>
    createValueDraft(example.type, example.value, example.traits),
  );
  const result = readValueDraft(example.type, draft, example.traits);
  const title = t(`settings.mock.nullableExamples.${example.id}`);
  return (
    <SettingItem
      label={title}
      htmlFor={`mock-${example.id}`}
      extra={t('settings.mock.nullableHint')}
      multiline={example.type.kind === 'list' || example.type.kind === 'dictionary'}
      validateStatus={result.ok ? undefined : 'error'}
      help={result.ok ? undefined : t(`valueEditor.${result.error}`)}
      action={
        <Flex align="center" gap={4}>
          <Tooltip title={t('settings.reset')}>
            <Button
              type="text"
              size="small"
              icon={<UndoOutlined />}
              aria-label={t('settings.resetField', { name: title })}
              onClick={() =>
                setDraft(createValueDraft(example.type, example.value, example.traits))
              }
            />
          </Tooltip>
          <ValueClearButton
            type={example.type}
            draft={draft}
            traits={example.traits}
            label={title}
            onChange={setDraft}
          />
        </Flex>
      }
    >
      <Flex vertical gap="small">
        <ValueEditor
          clearInHeader
          id={`mock-${example.id}`}
          label={title}
          type={example.type}
          traits={example.traits}
          draft={draft}
          onChange={setDraft}
        />
        {result.ok && (
          <Typography.Text type="secondary" style={{ overflowWrap: 'anywhere' }}>
            {t('settings.mock.previewValue')}{' '}
            <Typography.Text code>{JSON.stringify(result.value)}</Typography.Text>
          </Typography.Text>
        )}
      </Flex>
    </SettingItem>
  );
}
