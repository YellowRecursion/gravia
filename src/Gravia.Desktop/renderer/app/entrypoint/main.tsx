import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '../ui/App';
import { SettingsWindow } from '../ui/SettingsWindow';
import { getSettingsWindowScope } from '../config/window-route';
import { AppErrorBoundary } from '../ui/AppErrorBoundary';
import { AppProviders } from '../providers/AppProviders';
import { fusionPeer } from '@/shared/api';
import { initializeI18n } from '@/shared/i18n';
import 'antd/dist/reset.css';
import '@xyflow/react/dist/style.css';
import 'dockview-react/dist/styles/dockview.css';
import '../styles/global.css';

async function bootstrap() {
  fusionPeer.start();
  await initializeI18n();
  const root = document.getElementById('root');
  if (!root) throw new Error('Renderer root element is missing.');
  const settingsScope = getSettingsWindowScope(window.location.search);

  createRoot(root).render(
    <StrictMode>
      <AppProviders>
        <AppErrorBoundary>
          {settingsScope ? <SettingsWindow scope={settingsScope} /> : <App />}
        </AppErrorBoundary>
      </AppProviders>
    </StrictMode>,
  );
}

bootstrap().catch((error: unknown) => {
  console.error('Gravia Lab renderer failed to start.', error);
});
