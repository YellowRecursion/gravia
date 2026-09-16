import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Empty, Flex, Form, Input, InputNumber, Tooltip } from 'antd';
import { useTranslation } from 'react-i18next';
import { SettingItem } from '../SettingItem';

const examples = [
  { name: 'strings', dictionary: false, numeric: false, type: 'ListSetting<string>' },
  { name: 'numbers', dictionary: false, numeric: true, type: 'ListSetting<double>' },
  { name: 'textDictionary', dictionary: true, numeric: false, type: 'DictionarySetting<string>' },
  { name: 'numberDictionary', dictionary: true, numeric: true, type: 'DictionarySetting<double>' },
] as const;

export function MockCollections() {
  const { t } = useTranslation();
  return examples.map(({ name, dictionary, numeric, type }) => (
    <section
      key={name}
      className="settings-mock__collection"
      aria-label={t(`settings.mock.${name}`)}
    >
      <SettingItem multiline label={t(`settings.mock.${name}`)} extra={type}>
        <Form.List name={name}>
          {(fields, { add, remove }) => (
            <Flex vertical gap="small">
              {!fields.length && (
                <Empty
                  image={Empty.PRESENTED_IMAGE_SIMPLE}
                  description={t('settings.mock.emptyCollection')}
                />
              )}
              {fields.map((field) => (
                <Flex key={field.key} gap="small" align="start" data-mock-row={name}>
                  {dictionary && (
                    <Form.Item
                      name={[field.name, 'key']}
                      className="settings-mock__cell"
                      validateTrigger={['onChange', 'onBlur']}
                      rules={[
                        {
                          required: true,
                          whitespace: true,
                          message: t('settings.mock.keyRequired'),
                        },
                        ({ getFieldValue }) => ({
                          validator: (_, value: unknown) => {
                            const rows =
                              (getFieldValue(name) as { key?: string }[] | undefined) ?? [];
                            if (
                              typeof value === 'string' &&
                              rows.filter((row) => row.key === value).length > 1
                            )
                              return Promise.reject(new Error(t('settings.mock.duplicateKey')));
                            return Promise.resolve();
                          },
                        }),
                      ]}
                    >
                      <Input
                        placeholder={t('settings.mock.key')}
                        aria-label={t('settings.mock.keyAt', { index: field.name + 1 })}
                      />
                    </Form.Item>
                  )}
                  <Form.Item name={[field.name, 'value']} className="settings-mock__cell">
                    {numeric ? (
                      <InputNumber
                        style={{ width: '100%' }}
                        step={0.1}
                        aria-label={t('settings.mock.valueAt', { index: field.name + 1 })}
                      />
                    ) : (
                      <Input
                        placeholder={t('settings.mock.value')}
                        aria-label={t('settings.mock.valueAt', { index: field.name + 1 })}
                      />
                    )}
                  </Form.Item>
                  <Tooltip title={t('settings.mock.remove')}>
                    <Button
                      type="text"
                      icon={<DeleteOutlined />}
                      aria-label={t('settings.mock.removeAt', { index: field.name + 1 })}
                      onClick={() => remove(field.name)}
                    />
                  </Tooltip>
                </Flex>
              ))}
              <Button
                type="dashed"
                icon={<PlusOutlined />}
                onClick={() =>
                  add(
                    dictionary ? { key: '', value: numeric ? 0 : '' } : { value: numeric ? 0 : '' },
                  )
                }
              >
                {t(dictionary ? 'settings.mock.addPair' : 'settings.mock.addItem')}
              </Button>
            </Flex>
          )}
        </Form.List>
      </SettingItem>
    </section>
  ));
}
