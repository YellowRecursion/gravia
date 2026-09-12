// Compatibility specimen only: not a product implementation or UI benchmark.
import { createRoot } from 'react-dom/client';
import { useState } from 'react';
import { App, ConfigProvider, Button, Form, InputNumber, Table, theme } from 'antd';
import ruRU from 'antd/locale/ru_RU';
import enUS from 'antd/locale/en_US';
import { ReactFlow, Background, Controls, type AriaLabelConfig, type Node } from '@xyflow/react';
import { DockviewReact, themeDark, themeLight, type IDockviewPanelProps } from 'dockview-react';
import i18n from 'i18next';
import { initReactI18next, useTranslation } from 'react-i18next';
import '@xyflow/react/dist/style.css';
import 'dockview-react/dist/styles/dockview.css';

await i18n.use(initReactI18next).init({ lng: 'ru', fallbackLng: 'en', interpolation: { escapeValue: false }, resources: {
  ru: { translation: { graph: 'Схема', run: 'Рассчитать' } },
  en: { translation: { graph: 'Flowsheet', run: 'Calculate' } },
} });
const nodes: Node[] = Array.from({ length: 80 }, (_, i) => ({ id: String(i), position: { x: (i % 10) * 180, y: Math.floor(i / 10) * 100 }, data: { label: `Unit ${i}` } }));
const labels: Partial<AriaLabelConfig> = { 'controls.zoomIn.ariaLabel': 'Приблизить' };
function GraphPanel(_props: IDockviewPanelProps) {
  return <ReactFlow nodes={nodes} edges={[]} colorMode="dark" ariaLabelConfig={labels} fitView><Background /><Controls /></ReactFlow>;
}
const panels = { graph: GraphPanel };
function Specimen() {
  const [dark, setDark] = useState(false);
  const { t } = useTranslation();
  return <ConfigProvider locale={i18n.language === 'ru' ? ruRU : enUS} theme={{ algorithm: [dark ? theme.darkAlgorithm : theme.defaultAlgorithm, theme.compactAlgorithm] }}>
    <App>
      <Button onClick={() => setDark(!dark)}>{t('run')}</Button>
      <Form><Form.Item name="temperature"><InputNumber decimalSeparator="," /></Form.Item></Form>
      <Table<{ id: string; value: number }> rowKey="id" virtual scroll={{ x: 600, y: 200 }} columns={[{ dataIndex: 'value', title: 'Value' }]} dataSource={[{ id: 'one', value: 1.25 }]} />
      <div style={{ height: 500 }}><DockviewReact components={panels} theme={dark ? themeDark : themeLight}
        messages={{ closeTab: title => `Закрыть ${title}`, closeTabPlain: () => 'Закрыть' }}
        onReady={event => { event.api.addPanel({ id: 'graph', component: 'graph', title: t('graph') }); const state = event.api.toJSON(); event.api.fromJSON(state); }} />
      </div>
    </App>
  </ConfigProvider>;
}
createRoot(document.getElementById('root')!).render(<Specimen />);
