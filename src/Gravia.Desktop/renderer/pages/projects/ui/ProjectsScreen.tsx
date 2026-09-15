import { FolderOpenOutlined, FolderOutlined, PlusOutlined } from '@ant-design/icons';
import {
  Button,
  ConfigProvider,
  Flex,
  Form,
  Input,
  Menu,
  Space,
  Splitter,
  Tooltip,
  Typography,
  theme,
} from 'antd';
import { useTranslation } from 'react-i18next';
import './ProjectsScreen.css';

// Presentation fixtures only; this screen does not read or modify Host projects yet.
const examples = [
  { id: 'refinery', path: 'C:\\Gravia\\Refinery' },
  { id: 'avt', path: 'D:\\Projects\\AVT' },
  { id: 'blending', path: 'D:\\Projects\\Blending' },
] as const;

interface ProjectsScreenProps {
  readonly currentProjectPath?: string | null;
}

export function ProjectsScreen({ currentProjectPath = null }: ProjectsScreenProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();

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
                  />
                </Tooltip>
              </Flex>
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
                  selectedKeys={examples
                    .filter((project) => project.path === currentProjectPath)
                    .map((project) => project.id)}
                  items={examples.map((project) => ({
                    key: project.id,
                    icon: <FolderOutlined />,
                    label: (
                      <Flex vertical gap={2} style={{ minWidth: 0, lineHeight: token.lineHeight }}>
                        <Typography.Text strong ellipsis>
                          {t(`projects.examples.${project.id}`)}
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
              <Form layout="vertical" component="div" requiredMark={false}>
                <Form.Item label={t('projects.nameLabel')} htmlFor="project-name">
                  <Input
                    id="project-name"
                    placeholder={t('projects.namePlaceholder')}
                    autoComplete="off"
                  />
                </Form.Item>
                <Form.Item
                  label={t('projects.locationLabel')}
                  htmlFor="project-location"
                  extra={t('projects.locationHint')}
                >
                  <Space.Compact block>
                    <Input id="project-location" placeholder={'C:\\Gravia'} autoComplete="off" />
                    <Button
                      icon={<FolderOpenOutlined />}
                      aria-label={t('projects.browseLocation')}
                    />
                  </Space.Compact>
                </Form.Item>
                <Button type="primary" icon={<PlusOutlined />}>
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
