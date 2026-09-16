import { FolderOpenOutlined, FolderOutlined, PlusOutlined } from '@ant-design/icons';
import { useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Button,
  ConfigProvider,
  Empty,
  Flex,
  Form,
  Input,
  Menu,
  Space,
  Spin,
  Splitter,
  Tooltip,
  Typography,
  theme,
} from 'antd';
import { useTranslation } from 'react-i18next';
import { NameIcon } from '@/shared/ui';
import {
  chooseDirectory,
  DesktopUnavailableError,
  getRpcFieldErrors,
  recentProjectsQuery,
  RpcError,
} from '@/shared/api';
import './ProjectsScreen.css';

interface ProjectsScreenProps {
  readonly currentProjectPath?: string | null;
  readonly pending: boolean;
  readonly onOpenProject: (path: string) => Promise<void>;
  readonly onCreateProject: (parentDirectory: string, name: string) => Promise<void>;
}

export function ProjectsScreen({
  currentProjectPath = null,
  pending,
  onOpenProject,
  onCreateProject,
}: ProjectsScreenProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const recent = useQuery(recentProjectsQuery);
  // Form and feedback live only while this screen is mounted; the Host owns the project session.
  const [name, setName] = useState('');
  const [parentDirectory, setParentDirectory] = useState('');
  const [action, setAction] = useState<'open' | 'create' | 'browse' | null>(null);
  const [failure, setFailure] = useState<{ area: 'open' | 'create'; error: unknown } | null>(null);
  const busy = pending || action !== null;
  const running = useRef(false);

  async function run(kind: 'open' | 'create' | 'browse', work: () => Promise<void>) {
    if (running.current || pending) return;
    running.current = true;
    setAction(kind);
    setFailure(null);
    try {
      await work();
    } catch (error) {
      setFailure({ area: kind === 'open' ? 'open' : 'create', error });
    } finally {
      running.current = false;
      setAction(null);
    }
  }

  function errorText(error: unknown) {
    const code =
      error instanceof DesktopUnavailableError
        ? 'desktop_required'
        : error instanceof RpcError
          ? error.code
          : 'unavailable';
    return t(`projects.errors.${code}`, { defaultValue: t('projects.errors.unavailable') });
  }

  function fieldError(field: string) {
    if (failure?.area !== 'create') return undefined;
    const error = getRpcFieldErrors(failure.error).find(
      (item) => item.field.toLowerCase() === field.toLowerCase(),
    );
    return error
      ? t(`projects.errors.${error.code}`, { defaultValue: t('projects.errors.invalid_value') })
      : undefined;
  }

  const nameError = fieldError('name');
  const directoryError = fieldError('parentDirectory');

  return (
    <div className="projects-screen">
      <Splitter>
        <Splitter.Panel defaultSize="50%" min={260}>
          <section aria-labelledby="recent-projects-heading" style={{ padding: token.paddingLG }}>
            <Flex vertical gap="large">
              <Flex align="center" justify="space-between" gap="small">
                <Typography.Title id="recent-projects-heading" level={4} style={{ margin: 0 }}>
                  {t('projects.recentTitle')}
                </Typography.Title>
                <Tooltip title={t('projects.openOther')}>
                  <Button
                    type="text"
                    icon={<FolderOpenOutlined />}
                    aria-label={t('projects.openOther')}
                    disabled={busy}
                    loading={action === 'open'}
                    onClick={() =>
                      void run('open', async () => {
                        const path = await chooseDirectory();
                        if (path !== null) await onOpenProject(path);
                      })
                    }
                  />
                </Tooltip>
              </Flex>
              {failure?.area === 'open' && (
                <Alert type="error" showIcon title={errorText(failure.error)} />
              )}
              {recent.isPending && <Spin aria-label={t('projects.loading')} />}
              {recent.isError && (
                <Alert
                  type="error"
                  showIcon
                  title={t('projects.recentError')}
                  action={
                    <Button disabled={busy} onClick={() => void recent.refetch()}>
                      {t('projects.retry')}
                    </Button>
                  }
                />
              )}
              {recent.isSuccess && recent.data.length === 0 && (
                <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('projects.empty')} />
              )}
              <ConfigProvider
                theme={{
                  components: {
                    Menu: {
                      itemHeight: 64,
                      itemMarginInline: 0,
                      itemPaddingInline: token.padding,
                      itemBg: 'transparent',
                      activeBarBorderWidth: 0,
                    },
                  },
                }}
              >
                <Menu
                  mode="inline"
                  aria-labelledby="recent-projects-heading"
                  selectedKeys={currentProjectPath ? [currentProjectPath] : []}
                  onClick={({ key }) => void run('open', () => onOpenProject(key))}
                  items={(recent.data ?? []).map((project) => ({
                    key: project.path,
                    disabled: busy,
                    icon: <NameIcon name={project.name} size={32} />,
                    label: (
                      <Flex vertical gap={2} style={{ minWidth: 0, lineHeight: token.lineHeight }}>
                        <Typography.Text strong ellipsis>
                          {project.name}
                        </Typography.Text>
                        <Typography.Text
                          type="secondary"
                          ellipsis
                          style={{ fontSize: token.fontSizeSM }}
                        >
                          {project.path}
                        </Typography.Text>
                      </Flex>
                    ),
                  }))}
                />
              </ConfigProvider>
            </Flex>
          </section>
        </Splitter.Panel>
        <Splitter.Panel min={260}>
          <section aria-labelledby="create-project-heading" style={{ padding: token.paddingLG }}>
            <Flex vertical gap="large">
              <Typography.Title id="create-project-heading" level={4} style={{ margin: 0 }}>
                {t('projects.createTitle')}
              </Typography.Title>
              {failure?.area === 'create' && (
                <Alert type="error" showIcon title={errorText(failure.error)} />
              )}
              <Form
                layout="vertical"
                requiredMark={false}
                disabled={busy}
                onFinish={() => void run('create', () => onCreateProject(parentDirectory, name))}
              >
                <Form.Item
                  label={t('projects.nameLabel')}
                  htmlFor="project-name"
                  validateStatus={nameError ? 'error' : undefined}
                  help={nameError}
                >
                  <Input
                    id="project-name"
                    prefix={name.trim() ? <NameIcon name={name} size={20} /> : <FolderOutlined />}
                    placeholder={t('projects.namePlaceholder')}
                    autoComplete="off"
                    value={name}
                    onChange={(event) => {
                      setName(event.target.value);
                      setFailure(null);
                    }}
                  />
                </Form.Item>
                <Form.Item
                  label={t('projects.locationLabel')}
                  htmlFor="project-location"
                  extra={t('projects.locationHint')}
                  validateStatus={directoryError ? 'error' : undefined}
                  help={directoryError}
                >
                  <Space.Compact block>
                    <Input
                      id="project-location"
                      placeholder={t('projects.locationPlaceholder')}
                      autoComplete="off"
                      value={parentDirectory}
                      onChange={(event) => {
                        setParentDirectory(event.target.value);
                        setFailure(null);
                      }}
                    />
                    <Button
                      icon={<FolderOpenOutlined />}
                      aria-label={t('projects.browseLocation')}
                      loading={action === 'browse'}
                      onClick={() =>
                        void run('browse', async () => {
                          const path = await chooseDirectory();
                          if (path !== null) setParentDirectory(path);
                        })
                      }
                    />
                  </Space.Compact>
                </Form.Item>
                <Button
                  type="primary"
                  htmlType="submit"
                  icon={<PlusOutlined />}
                  loading={action === 'create'}
                >
                  {t('projects.createAction')}
                </Button>
              </Form>
            </Flex>
          </section>
        </Splitter.Panel>
      </Splitter>
    </div>
  );
}
