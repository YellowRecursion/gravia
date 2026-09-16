import { useRef } from 'react';
import { useMutation } from '@tanstack/react-query';
import { ReloadOutlined, UndoOutlined } from '@ant-design/icons';
import { Button, Flex, Select, Spin, Tooltip, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { getBuiltinEditor, settingsApi, useSetting } from '@/entities/settings';
import { getRpcFieldErrors, RpcError, unwrapRpcResult } from '@/shared/api';
import type { SettingsScope, SettingSummaryDto } from '@/shared/api';
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
  const running = useRef(false);
  const editor = getBuiltinEditor(scope, summary.id);
  const mutation = useMutation({
    mutationFn: async (action: { value: string } | { reset: true }) => {
      const api = settingsApi(scope);
      unwrapRpcResult(
        await ('reset' in action
          ? api.ResetValue(summary.id)
          : api.SetValue(summary.id, action.value)),
      );
    },
  });
  async function change(action: { value: string } | { reset: true }) {
    if (running.current || !editor) return;
    running.current = true;
    try {
      await mutation.mutateAsync(action);
    } catch {
      /* Render the mutation error next to the field. */
    } finally {
      running.current = false;
    }
  }
  const language = i18n.resolvedLanguage ?? 'ru';
  const title = localizedText(summary.title, language);
  const error = mutation.error;
  const code =
    getRpcFieldErrors(error).find((item) => item.field.toLowerCase() === 'value')?.code ??
    (error instanceof RpcError ? error.code : 'saveError');
  const options = editor?.map((option) => ({ value: option.value, label: t(option.labelKey) }));
  const value = query.data?.value;
  // Never coerce or overwrite a value written by a newer Host that this temporary UI does not know.
  if (typeof value === 'string' && options && !options.some((option) => option.value === value)) {
    options.push({ value, label: t('settings.unknownValue', { value }) });
  }
  return (
    <SettingItem
      label={title}
      htmlFor={summary.id}
      extra={summary.description ? localizedText(summary.description, language) : undefined}
      action={
        query.data && editor ? (
          <Tooltip title={t('settings.reset')}>
            <Button
              type="text"
              size="small"
              icon={<UndoOutlined />}
              aria-label={t('settings.resetField', { name: title })}
              disabled={mutation.isPending || value === query.data.defaultValue}
              onClick={() => void change({ reset: true })}
            />
          </Tooltip>
        ) : undefined
      }
      validateStatus={error ? 'error' : undefined}
      help={
        error ? t(`settings.errors.${code}`, { defaultValue: t('settings.saveError') }) : undefined
      }
    >
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
      ) : !query.data ? (
        <Typography.Text type="secondary">{t('settings.missing')}</Typography.Text>
      ) : !editor ? (
        <Typography.Text type="secondary">{t('settings.unsupported')}</Typography.Text>
      ) : (
        <Flex gap="small" align="center">
          <Select
            id={summary.id}
            aria-label={title}
            style={{ width: '100%' }}
            value={typeof value === 'string' ? value : undefined}
            placeholder={t('settings.unsupported')}
            options={options}
            loading={mutation.isPending}
            disabled={mutation.isPending}
            onChange={(next: string) => void change({ value: next })}
          />
        </Flex>
      )}
    </SettingItem>
  );
}
