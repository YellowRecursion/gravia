import { useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { ReloadOutlined, UndoOutlined } from '@ant-design/icons';
import { Button, Flex, Spin, Tooltip, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { settingsApi, useSetting } from '@/entities/settings';
import { getRpcFieldErrors, RpcError, unwrapRpcResult } from '@/shared/api';
import type { SettingsScope, SettingDto, SettingSummaryDto } from '@/shared/api';
import {
  createValueDraft,
  equalValue,
  parseValueType,
  readValueDraft,
  resolveValueTraits,
} from '@/shared/lib';
import type { ValueDraft } from '@/shared/lib';
import { ValueClearButton, ValueEditor } from '@/shared/ui';
import { SettingItem } from './SettingItem';
import { localizedText } from '../model/categories';

export function SettingField({
  scope,
  summary,
}: {
  scope: SettingsScope;
  summary: SettingSummaryDto;
}) {
  const { t, i18n } = useTranslation();
  const query = useSetting(scope, summary.id);
  const title = localizedText(summary.title, i18n.resolvedLanguage ?? 'ru');
  if (query.data)
    return (
      <LoadedSettingField
        scope={scope}
        data={query.data}
        refresh={async () => {
          const result = await query.refetch();
          return !result.isError;
        }}
      />
    );
  return (
    <SettingItem label={title}>
      {query.isPending ? (
        <Spin size="small" />
      ) : query.isError ? (
        <Flex gap="small" align="center">
          <Typography.Text type="danger">{t('settings.loadError')}</Typography.Text>
          <Button
            icon={<ReloadOutlined />}
            aria-label={t('projects.retry')}
            onClick={() => void query.refetch()}
          />
        </Flex>
      ) : (
        <Typography.Text type="secondary">{t('settings.missing')}</Typography.Text>
      )}
    </SettingItem>
  );
}

type Edit = { base: SettingDto; draft: ValueDraft };
type Change = { value: unknown } | { reset: true };

function LoadedSettingField({
  scope,
  data,
  refresh,
}: {
  scope: SettingsScope;
  data: SettingDto;
  refresh: () => Promise<boolean>;
}) {
  const { t, i18n } = useTranslation();
  // Only unfinished input lives locally. Clean fields use the latest Query value.
  const [edit, setEdit] = useState<Edit>();
  const [saving, setSaving] = useState(false);
  const [refreshFailed, setRefreshFailed] = useState(false);
  const running = useRef(false);
  const type = parseValueType(data.valueType);
  const traits = Array.isArray(data.traits) ? data.traits : [];
  const draft = edit?.draft ?? (type ? createValueDraft(type, data.value, traits) : undefined);
  const result = type && draft ? readValueDraft(type, draft, traits) : undefined;
  const dirty = Boolean(edit && (!result?.ok || !equalValue(result.value, data.value)));
  const conflict = Boolean(
    edit &&
    (!equalValue(edit.base.value, data.value) ||
      !equalValue(edit.base.valueType, data.valueType) ||
      !equalValue(edit.base.traits, data.traits)),
  );
  const mutation = useMutation({
    mutationFn: async (action: Change) => {
      const api = settingsApi(scope);
      unwrapRpcResult(
        await ('reset' in action
          ? api.ResetValue(data.summary.id)
          : api.SetValue(data.summary.id, action.value)),
      );
    },
  });
  async function save(action: Change) {
    if (running.current) return;
    running.current = true;
    setSaving(true);
    try {
      await mutation.mutateAsync(action);
      const confirmed = await refresh();
      setRefreshFailed(!confirmed);
      if (confirmed) setEdit(undefined);
    } catch {
      // Preserve rejected input; render the RPC error next to the editor.
    } finally {
      running.current = false;
      setSaving(false);
    }
  }
  const title = localizedText(data.summary.title, i18n.resolvedLanguage ?? 'ru');
  const error = mutation.error;
  const code =
    getRpcFieldErrors(error).find((item) => item.field.toLowerCase() === 'value')?.code ??
    (error instanceof RpcError ? error.code : 'saveError');
  const message = error
    ? t(`settings.errors.${code}`, { defaultValue: t('settings.saveError') })
    : refreshFailed
      ? t('valueEditor.refreshFailed')
      : conflict && !saving
        ? t('valueEditor.conflict')
        : edit && result && !result.ok
          ? t(`valueEditor.${result.error}`)
          : undefined;
  const choices = type ? resolveValueTraits(type, traits).choices : undefined;
  function changeDraft(next: ValueDraft) {
    if (!type || running.current) return;
    mutation.reset();
    setEdit({ base: edit?.base ?? data, draft: next });
    const parsed = readValueDraft(type, next, traits);
    if (!conflict && parsed.ok && (choices || type.kind === 'bool'))
      void save({ value: parsed.value });
  }
  return (
    <SettingItem
      label={title}
      htmlFor={data.summary.id}
      multiline={!choices && (type?.kind === 'list' || type?.kind === 'dictionary')}
      extra={
        data.summary.description
          ? localizedText(data.summary.description, i18n.resolvedLanguage ?? 'ru')
          : undefined
      }
      action={
        <Flex align="center" gap={4}>
          <Tooltip title={t('settings.reset')}>
            <Button
              type="text"
              size="small"
              icon={<UndoOutlined />}
              aria-label={t('settings.resetField', { name: title })}
              disabled={
                saving || refreshFailed || (!edit && equalValue(data.value, data.defaultValue))
              }
              onClick={() => void save({ reset: true })}
            />
          </Tooltip>
          {type && draft && (
            <ValueClearButton
              type={type}
              draft={draft}
              traits={traits}
              label={title}
              disabled={saving || refreshFailed}
              onChange={changeDraft}
            />
          )}
        </Flex>
      }
      validateStatus={message ? 'error' : undefined}
      help={message}
    >
      {type && draft ? (
        <Flex vertical gap="small">
          <ValueEditor
            clearInHeader
            id={data.summary.id}
            label={title}
            type={type}
            traits={traits}
            draft={draft}
            disabled={saving || refreshFailed}
            onChange={changeDraft}
          />
          {edit && (
            <Flex gap="small">
              <Button
                size="small"
                type="primary"
                loading={saving}
                disabled={!dirty || conflict || !result?.ok || refreshFailed}
                onClick={() => {
                  if (result?.ok) void save({ value: result.value });
                }}
              >
                {t('valueEditor.apply')}
              </Button>
              <Button
                size="small"
                disabled={saving || refreshFailed}
                onClick={() => {
                  setEdit(undefined);
                  mutation.reset();
                }}
              >
                {t('valueEditor.cancel')}
              </Button>
            </Flex>
          )}
        </Flex>
      ) : (
        <Flex vertical>
          <Typography.Text type="secondary">{t('settings.unsupported')}</Typography.Text>
          <Typography.Text code>{JSON.stringify(data.value)}</Typography.Text>
        </Flex>
      )}
      {refreshFailed && (
        <Button
          size="small"
          loading={saving}
          onClick={() => {
            setSaving(true);
            void refresh()
              .then((confirmed) => {
                setRefreshFailed(!confirmed);
                if (confirmed) setEdit(undefined);
              })
              .finally(() => setSaving(false));
          }}
        >
          {t('projects.retry')}
        </Button>
      )}
    </SettingItem>
  );
}
