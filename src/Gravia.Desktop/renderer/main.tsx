import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { AppProviders } from './AppProviders';
import { initializeI18n } from './i18n';
import 'antd/dist/reset.css';
import '@xyflow/react/dist/style.css';
import 'dockview-react/dist/styles/dockview.css';
import './styles.css';

async function bootstrap() {
  await initializeI18n();
  const root = document.getElementById('root');
  if (!root) throw new Error('Renderer root element is missing.');

  createRoot(root).render(
    <StrictMode>
      <AppProviders>
        <App />
      </AppProviders>
    </StrictMode>,
  );
}

bootstrap().catch((error: unknown) => {
  console.error('Gravia Lab renderer failed to start.', error);
});
