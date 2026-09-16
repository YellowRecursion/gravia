import {
  BugOutlined,
  CloseCircleFilled,
  CodeOutlined,
  ExclamationCircleFilled,
  InfoCircleFilled,
  StopOutlined,
} from '@ant-design/icons';
import { theme } from 'antd';
import { useTranslation } from 'react-i18next';
import { levelNames } from './log-presentation';
const levelIcons = [
  CodeOutlined,
  BugOutlined,
  InfoCircleFilled,
  ExclamationCircleFilled,
  CloseCircleFilled,
  StopOutlined,
];

export function LogLevelIcon({ level }: { level: number }) {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const colors = [
    token.colorTextTertiary,
    token.colorTextSecondary,
    token.colorInfo,
    token.colorWarning,
    token.colorError,
    token.colorError,
  ];
  const Icon = levelIcons[level] ?? InfoCircleFilled;
  return (
    <Icon
      style={{ color: colors[level] }}
      aria-label={t(`logPanel.levels.${levelNames[level] ?? 'information'}`)}
    />
  );
}
