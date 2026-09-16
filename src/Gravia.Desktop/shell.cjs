const { parseBootstrap } = require('./bootstrap.cjs');
const net = require('node:net');
const { registerDirectoryDialog } = require('./directory-dialog.cjs');
const { registerWindowAppearance } = require('./window-appearance.cjs');
const { createAppWindow } = require('./app-window.cjs');
const { registerSettingsWindows } = require('./settings-windows.cjs');

async function runDesktop({ app, BrowserWindow, session, Menu, ipcMain, dialog, nativeTheme }) {
  app.setName('Gravia');
  // A private local pipe is independent of Windows console/stdio initialization.
  // EOF also arrives if Rider forcibly terminates the Host.
  // Keep this control pipe separate from future frontend and plugin protocols.
  if (!process.env.GRAVIA_CONTROL_PIPE) throw new Error('Start Gravia through Gravia.Host.');
  const control = net.createConnection(process.env.GRAVIA_CONTROL_PIPE);
  let received = false;
  let pending = '';
  let rejectBootstrap;
  const bootstrap = new Promise((resolve, reject) => {
    rejectBootstrap = reject;
    control.setEncoding('utf8');
    control.on('data', (chunk) => {
      if (received) return;
      pending += chunk;
      if (pending.length > 4096) {
        received = true;
        reject(new Error('Host startup message is too large.'));
        return;
      }
      const newline = pending.indexOf('\n');
      if (newline < 0) return;
      received = true;
      try {
        resolve(parseBootstrap(pending.slice(0, newline)));
      } catch (error) {
        reject(error);
      }
      pending = '';
    });
  });
  let stopping = false;
  const stop = () => {
    if (stopping) return;
    stopping = true;
    rejectBootstrap(new Error('Host disconnected before startup completed.'));
    setTimeout(() => app.exit(0), 3000).unref();
    app.quit();
  };
  // Vite may report a proxy disconnect after Rider has already killed the log reader (Host).
  // Treat its broken diagnostic pipe as shutdown, not an unhandled Electron error dialog.
  const diagnosticPipeError = (error) => {
    if (error.code === 'EPIPE' || error.code === 'ECONNRESET') stop();
    else throw error;
  };
  process.stdout.on('error', diagnosticPipeError);
  process.stderr.on('error', diagnosticPipeError);
  control.on('end', stop);
  control.on('error', stop);
  const startupTimeout = setTimeout(
    () => rejectBootstrap(new Error('Host startup timed out.')),
    15000,
  );
  let configuration;
  try {
    configuration = await bootstrap;
  } finally {
    clearTimeout(startupTimeout);
  }

  await app.whenReady();
  let rendererUrl = configuration.backendUrl;
  if (configuration.sourceDirectory) {
    const { startDevServer } = require('./dev-server.cjs');
    const devServer = await startDevServer(configuration.sourceDirectory, configuration.backendUrl);
    rendererUrl = devServer.url;
    app.once('will-quit', (event) => {
      event.preventDefault();
      void devServer.close().then(
        () => app.exit(0),
        (error) => {
          console.error('Could not stop Gravia Dev.', error);
          app.exit(1);
        },
      );
    });
  }
  Menu.setApplicationMenu(null);
  session.defaultSession.setPermissionRequestHandler((_contents, _permission, callback) =>
    callback(false),
  );
  session.defaultSession.setPermissionCheckHandler(() => false);
  const window = createAppWindow(BrowserWindow);
  registerDirectoryDialog({ ipcMain, dialog, window, backendUrl: rendererUrl });
  const trustAppearanceWindow = registerWindowAppearance({
    ipcMain,
    nativeTheme,
    window,
    backendUrl: rendererUrl,
  });
  registerSettingsWindows({
    ipcMain,
    BrowserWindow,
    window,
    backendUrl: rendererUrl,
    trustAppearanceWindow,
  });
  window.webContents.on('render-process-gone', (_event, details) => {
    console.error(`Renderer stopped: ${details.reason}`);
    app.exit(1);
  });
  app.on('window-all-closed', () => app.quit());
  window.once('closed', () => app.quit());
  await window.loadURL(rendererUrl);
  window.show();
  control.write(`${JSON.stringify({ event: 'ready' })}\n`);
  return window;
}

module.exports = { runDesktop };
