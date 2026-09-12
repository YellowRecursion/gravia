const { parseBootstrap } = require('./bootstrap.cjs');
const net = require('node:net');

async function runDesktop({ app, BrowserWindow, session, Menu }) {
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
  const stop = () => {
    rejectBootstrap(new Error('Host disconnected before startup completed.'));
    app.quit();
    setTimeout(() => app.exit(0), 3000).unref();
  };
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
  Menu.setApplicationMenu(null);
  session.defaultSession.setPermissionRequestHandler((_contents, _permission, callback) =>
    callback(false),
  );
  session.defaultSession.setPermissionCheckHandler(() => false);
  const window = new BrowserWindow({
    title: 'Gravia Lab',
    width: 1200,
    height: 800,
    minWidth: 640,
    minHeight: 480,
    show: false,
    backgroundColor: '#202124',
    webPreferences: { nodeIntegration: false, contextIsolation: true, sandbox: true },
  });
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  window.webContents.on('will-navigate', (event) => event.preventDefault());
  window.webContents.on('will-redirect', (event) => event.preventDefault());
  window.webContents.on('will-attach-webview', (event) => event.preventDefault());
  window.webContents.on('render-process-gone', (_event, details) => {
    console.error(`Renderer stopped: ${details.reason}`);
    app.exit(1);
  });
  app.on('window-all-closed', () => app.quit());
  await window.loadURL(configuration.backendUrl);
  window.show();
  control.write(`${JSON.stringify({ event: 'ready' })}\n`);
  return window;
}

module.exports = { runDesktop };
