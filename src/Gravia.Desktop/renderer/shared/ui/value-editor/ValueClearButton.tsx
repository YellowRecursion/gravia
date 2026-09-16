import { CloseOutlined } from '@ant-design/icons';
import { Button, Tooltip } from 'antd';
import { useTranslation } from 'react-i18next';
import { resolveValueTraits } from '@/shared/lib';
import type { ValueDraft, ValueType } from '@/shared/lib';

export function ValueClearButton({
  type,
  draft,
  traits = [],
  disabled,
  label,
  onChange,
}: {
  type: ValueType;
  draft: ValueDraft;
  traits?: readonly unknown[];
  disabled?: boolean;
  label: string;
  onChange: (draft: ValueDraft) => void;
}) {
  const { t } = useTranslation();
  const { choices } = resolveValueTraits(type, traits);
  if (!type.nullable || (choices && !choices.some((option) => option.value === null))) return null;
  return (
    <Tooltip title={t('valueEditor.setNull')}>
      <Button
        type="text"
        size="small"
        icon={<CloseOutlined />}
        aria-label={`${t('valueEditor.setNull')}: ${label}`}
        disabled={disabled || (draft.kind === 'scalar' && draft.value === null)}
        onClick={() => onChange({ kind: 'scalar', value: null })}
      />
    </Tooltip>
  );
}
