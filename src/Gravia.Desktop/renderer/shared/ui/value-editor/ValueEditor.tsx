import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Flex, Input, InputNumber, Select, Switch, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { createValueDraft, equalValue, initialValue, resolveValueTraits } from '@/shared/lib';
import type { ValueDraft, ValueType } from '@/shared/lib';
import { ValueClearButton } from './ValueClearButton';

interface ValueEditorProps {
  type: ValueType;
  draft: ValueDraft;
  traits?: readonly unknown[];
  onChange: (draft: ValueDraft) => void;
  disabled?: boolean;
  label: string;
  id?: string;
  clearInHeader?: boolean;
}

/** Controlled value editor. Persistence and draft lifetime belong to its consumer. */
export function ValueEditor({
  type,
  draft,
  traits = [],
  onChange,
  disabled,
  label,
  id,
  clearInHeader = false,
}: ValueEditorProps) {
  const { t, i18n } = useTranslation();
  const { choices } = resolveValueTraits(type, traits);
  if (choices) {
    const current = draft.kind === 'scalar' ? draft.value : undefined;
    const index = choices.findIndex((option) => equalValue(option.value, current));
    const options = choices.map((option, i) => ({
      value: String(i),
      label:
        option.label.translations[i18n.resolvedLanguage ?? 'ru'] ??
        option.label.translations.en ??
        Object.values(option.label.translations)[0] ??
        JSON.stringify(option.value),
      disabled: false,
    }));
    if (index < 0)
      options.push({
        value: 'current',
        label: t('valueEditor.unknownChoice', { value: JSON.stringify(current) }),
        disabled: true,
      });
    return (
      <Select
        id={id}
        aria-label={label}
        style={{ width: '100%' }}
        disabled={disabled || !choices.length}
        value={index < 0 ? 'current' : String(index)}
        options={options}
        onChange={(key) => {
          const option = choices[Number(key)];
          if (option) onChange({ kind: 'scalar', value: option.value });
        }}
      />
    );
  }
  const nonNullType = { ...type, nullable: false };
  if (type.nullable && draft.kind === 'scalar' && draft.value === null) {
    return (
      <Flex gap="small" align="center">
        <Typography.Text type="secondary">null</Typography.Text>
        <Button
          id={id}
          disabled={disabled}
          onClick={() => onChange(createValueDraft(nonNullType, initialValue(nonNullType)))}
        >
          {t('valueEditor.setValue')}
        </Button>
      </Flex>
    );
  }
  let control;
  if (draft.kind === 'list' && type.kind === 'list') {
    control = (
      <Flex vertical gap="small">
        {draft.items.map((item, index) => (
          <Flex key={index} align="start" gap="small">
            <div style={{ flex: 1, minWidth: 0 }}>
              <ValueEditor
                type={type.element}
                draft={item}
                disabled={disabled}
                label={`${label} [${index + 1}]`}
                onChange={(next) =>
                  onChange({
                    kind: 'list',
                    items: draft.items.map((old, i) => (i === index ? next : old)),
                  })
                }
              />
            </div>
            <Button
              type="text"
              icon={<DeleteOutlined />}
              disabled={disabled}
              aria-label={t('valueEditor.removeAt', { index: index + 1 })}
              onClick={() =>
                onChange({ kind: 'list', items: draft.items.filter((_, i) => i !== index) })
              }
            />
          </Flex>
        ))}
        <Button
          type="dashed"
          icon={<PlusOutlined />}
          disabled={disabled}
          onClick={() =>
            onChange({
              kind: 'list',
              items: [...draft.items, createValueDraft(type.element, initialValue(type.element))],
            })
          }
        >
          {t('valueEditor.add')}
        </Button>
      </Flex>
    );
  } else if (draft.kind === 'dictionary' && type.kind === 'dictionary') {
    control = (
      <Flex vertical gap="small">
        {draft.entries.map((entry, index) => (
          <Flex key={index} vertical gap="small">
            <Flex gap="small">
              <Input
                aria-label={t('valueEditor.keyAt', { index: index + 1 })}
                placeholder={t('valueEditor.key')}
                value={entry.key}
                disabled={disabled}
                status={
                  draft.entries.some((other, i) => i !== index && other.key === entry.key)
                    ? 'error'
                    : undefined
                }
                onChange={(event) =>
                  onChange({
                    kind: 'dictionary',
                    entries: draft.entries.map((old, i) =>
                      i === index ? { ...old, key: event.target.value } : old,
                    ),
                  })
                }
              />
              <Button
                type="text"
                icon={<DeleteOutlined />}
                disabled={disabled}
                aria-label={t('valueEditor.removeAt', { index: index + 1 })}
                onClick={() =>
                  onChange({
                    kind: 'dictionary',
                    entries: draft.entries.filter((_, i) => i !== index),
                  })
                }
              />
            </Flex>
            <ValueEditor
              type={type.value}
              draft={entry.value}
              disabled={disabled}
              label={`${label} [${entry.key}]`}
              onChange={(next) =>
                onChange({
                  kind: 'dictionary',
                  entries: draft.entries.map((old, i) =>
                    i === index ? { ...old, value: next } : old,
                  ),
                })
              }
            />
          </Flex>
        ))}
        <Button
          type="dashed"
          icon={<PlusOutlined />}
          disabled={disabled}
          onClick={() =>
            onChange({
              kind: 'dictionary',
              entries: [
                ...draft.entries,
                { key: '', value: createValueDraft(type.value, initialValue(type.value)) },
              ],
            })
          }
        >
          {t('valueEditor.add')}
        </Button>
      </Flex>
    );
  } else if (draft.kind === 'scalar' && type.kind === 'string' && typeof draft.value === 'string') {
    control = (
      <Input
        id={id}
        aria-label={label}
        disabled={disabled}
        value={draft.value}
        onChange={(event) => onChange({ kind: 'scalar', value: event.target.value })}
      />
    );
  } else if (draft.kind === 'scalar' && type.kind === 'bool' && typeof draft.value === 'boolean') {
    control = (
      <Switch
        id={id}
        aria-label={label}
        disabled={disabled}
        checked={draft.value}
        onChange={(value) => onChange({ kind: 'scalar', value })}
      />
    );
  } else if (
    draft.kind === 'scalar' &&
    (type.kind === 'int' || type.kind === 'double') &&
    (typeof draft.value === 'number' || draft.value === null)
  ) {
    control = (
      <InputNumber
        id={id}
        aria-label={label}
        disabled={disabled}
        style={{ width: '100%' }}
        value={draft.value}
        onChange={(value) => onChange({ kind: 'scalar', value })}
      />
    );
  } else {
    control = (
      <Flex vertical gap="small">
        <Typography.Text type="danger">{t('valueEditor.invalidValue')}</Typography.Text>
        <Typography.Text code>{JSON.stringify(draft)}</Typography.Text>
        <Button
          disabled={disabled}
          onClick={() => onChange(createValueDraft(nonNullType, initialValue(nonNullType)))}
        >
          {t('valueEditor.setValue')}
        </Button>
      </Flex>
    );
  }
  return (
    <Flex gap="small" align="start" style={{ minWidth: 0 }}>
      <div style={{ flex: 1, minWidth: 0 }}>{control}</div>
      {!clearInHeader && (
        <ValueClearButton
          type={type}
          draft={draft}
          traits={traits}
          disabled={disabled}
          label={label}
          onChange={onChange}
        />
      )}
    </Flex>
  );
}
