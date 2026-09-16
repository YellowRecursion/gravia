import { UndoOutlined } from '@ant-design/icons';
import {
  Button,
  Checkbox,
  ColorPicker,
  Divider,
  Flex,
  Form,
  Input,
  InputNumber,
  Radio,
  Segmented,
  Select,
  Slider,
  Switch,
  Typography,
} from 'antd';
import { useTranslation } from 'react-i18next';
import { SettingItem } from '../SettingItem';
import { MockCollections } from './MockCollections';
import type { MockSection } from '../../model/mock-sections';

// Deliberately not registered with Host. Form owns these disposable examples while the mock is open.
const initialValues = {
  text: 'Gravia Lab',
  integer: 12,
  decimal: 0.125,
  invalidNumber: 120,
  enabled: true,
  checkbox: false,
  multiline: 'Gravia Lab\nUI preview',
  password: 'demo-password',
  choice: 'normal',
  radio: 'normal',
  segmented: 'normal',
  slider: 65,
  range: [20, 80],
  stepped: 50,
  percent: 75,
  precision: 0.001,
  tags: ['alpha', 'beta'],
  multiple: ['files', 'graph'],
  color: '#1677ff',
  strings: [{ value: 'alpha' }, { value: 'beta' }],
  numbers: [{ value: 0.25 }, { value: 0.5 }, { value: 1 }],
  textDictionary: [
    { key: 'author', value: 'Gravia Lab' },
    { key: 'format', value: 'json' },
  ],
  numberDictionary: [
    { key: 'alpha', value: 1 },
    { key: 'beta', value: 0.75 },
  ],
};

export function MockSettings({ section }: { section: MockSection }) {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const sliderValue =
    Form.useWatch<number>('slider', { form, preserve: true }) ?? initialValues.slider;
  const choices = ['compact', 'normal', 'detailed'].map((value) => ({
    value,
    label: t(`settings.mock.choices.${value}`),
  }));
  const panels = ['files', 'graph', 'inspector', 'logs'].map((value) => ({
    value,
    label: t(`workspace.${value}`),
  }));
  return (
    <div className="settings-mock" data-mock-section={section}>
      <Flex vertical gap="small" style={{ marginBottom: 24 }}>
        <Typography.Text type="secondary">{t('settings.mock.notice')}</Typography.Text>
        <Button
          icon={<UndoOutlined />}
          style={{ alignSelf: 'flex-start' }}
          onClick={() => form.resetFields()}
        >
          {t('settings.mock.reset')}
        </Button>
      </Flex>
      <Form
        form={form}
        name="settings-mock"
        component="div"
        layout="vertical"
        requiredMark={false}
        initialValues={initialValues}
      >
        {section === 'basic' && (
          <>
            <SettingItem name="text" label={t('settings.mock.text')} extra="StringSetting">
              <Input allowClear className="settings-mock__short" />
            </SettingItem>
            <SettingItem name="integer" label={t('settings.mock.integer')} extra="IntSetting">
              <InputNumber precision={0} step={1} />
            </SettingItem>
            <SettingItem name="decimal" label={t('settings.mock.decimal')} extra="DoubleSetting">
              <InputNumber step={0.001} />
            </SettingItem>
            <SettingItem
              name="enabled"
              valuePropName="checked"
              label={t('settings.mock.boolean')}
              extra={t('settings.mock.booleanHint')}
            >
              <Switch />
            </SettingItem>
            <SettingItem
              name="checkbox"
              valuePropName="checked"
              label={t('settings.mock.checkbox')}
            >
              <Checkbox>{t('settings.mock.checkboxCaption')}</Checkbox>
            </SettingItem>
            <Divider titlePlacement="start">{t('settings.mock.states')}</Divider>
            <SettingItem label={t('settings.mock.disabled')}>
              <Input value="Gravia Lab" disabled className="settings-mock__short" />
            </SettingItem>
            <SettingItem label={t('settings.mock.readOnly')}>
              <Input value="gravia.preview" readOnly className="settings-mock__short" />
            </SettingItem>
            <SettingItem
              name="invalidNumber"
              label={t('settings.mock.error')}
              validateStatus="error"
              help={t('settings.mock.errorHint')}
            >
              <InputNumber aria-label={t('settings.mock.error')} />
            </SettingItem>
          </>
        )}
        {section === 'modifiers' && (
          <>
            <SettingItem label={t('settings.mock.slider')} extra={t('settings.mock.sliderHint')}>
              <Flex align="center" gap="middle">
                <Form.Item name="slider" noStyle>
                  <Slider
                    style={{ flex: 1 }}
                    min={0}
                    max={100}
                    ariaLabelForHandle={t('settings.mock.slider')}
                  />
                </Form.Item>
                <InputNumber
                  aria-label={t('settings.mock.sliderValue')}
                  min={0}
                  max={100}
                  value={sliderValue}
                  onChange={(value) => {
                    if (value !== null) form.setFieldValue('slider', value);
                  }}
                />
              </Flex>
            </SettingItem>
            <SettingItem name="range" label={t('settings.mock.range')}>
              <Slider
                range
                min={0}
                max={100}
                ariaLabelForHandle={[t('settings.mock.rangeStart'), t('settings.mock.rangeEnd')]}
              />
            </SettingItem>
            <SettingItem name="stepped" label={t('settings.mock.stepped')}>
              <Slider
                step={null}
                marks={{ 0: '0', 25: '25', 50: '50', 75: '75', 100: '100' }}
                ariaLabelForHandle={t('settings.mock.stepped')}
              />
            </SettingItem>
            <SettingItem
              name="percent"
              label={t('settings.mock.percent')}
              extra={t('settings.mock.percentHint')}
            >
              <InputNumber min={0} max={100} suffix="%" />
            </SettingItem>
            <SettingItem
              name="precision"
              label={t('settings.mock.precision')}
              extra={t('settings.mock.precisionHint')}
            >
              <InputNumber min={0} max={1} step={0.001} precision={3} />
            </SettingItem>
            <Divider titlePlacement="start">{t('settings.mock.variants')}</Divider>
            <SettingItem name="choice" label="Select">
              <Select options={choices} className="settings-mock__short" />
            </SettingItem>
            <SettingItem name="radio" label="Radio.Group">
              <Radio.Group options={choices} />
            </SettingItem>
            <SettingItem name="segmented" label="Segmented">
              <Segmented options={choices} />
            </SettingItem>
            <Divider titlePlacement="start">{t('settings.mock.textModifiers')}</Divider>
            <SettingItem multiline name="multiline" label={t('settings.mock.multiline')}>
              <Input.TextArea autoSize={{ minRows: 3, maxRows: 6 }} />
            </SettingItem>
            <SettingItem
              name="password"
              label={t('settings.mock.password')}
              extra={t('settings.mock.passwordHint')}
            >
              <Input.Password className="settings-mock__short" autoComplete="off" />
            </SettingItem>
            <SettingItem name="color" label={t('settings.mock.color')}>
              <ColorPicker showText />
            </SettingItem>
          </>
        )}
        {section === 'collections' && (
          <>
            <SettingItem
              name="tags"
              label={t('settings.mock.tags')}
              extra={t('settings.mock.tagsHint')}
            >
              <Select mode="tags" tokenSeparators={[',']} options={[]} />
            </SettingItem>
            <SettingItem name="multiple" label={t('settings.mock.multiple')}>
              <Select mode="multiple" options={panels} />
            </SettingItem>
            <MockCollections />
          </>
        )}
      </Form>
    </div>
  );
}
