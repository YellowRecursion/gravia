// Copied into a temporary app by desktop-smoke.cjs. Never shipped with the shell.
const electron = require('electron');
const path = require('node:path');
const { setTimeout: delay } = require('node:timers/promises');
const { runDesktop } = require('./shell.cjs');
electron.app.setPath('userData', path.join(__dirname, 'profile'));
electron.app.setPath('sessionData', path.join(__dirname, 'profile'));
const rendererErrors = [];
let rpcHandshakeSent = false;
let rpcHandshakeReceived = false;
electron.app.on('web-contents-created', (_event, contents) => {
  // Observe the real renderer connection without exposing test globals to the page.
  contents.debugger.attach('1.3');
  contents.debugger.on('message', (_event, method, params) => {
    if (!params.response?.payloadData?.includes('$sys.Handshake')) return;
    if (method === 'Network.webSocketFrameSent') rpcHandshakeSent = true;
    if (method === 'Network.webSocketFrameReceived') rpcHandshakeReceived = true;
  });
  contents.debugger.sendCommand('Network.enable').catch((error) => {
    console.error(error);
    electron.app.exit(1);
  });
  contents.on('console-message', (details) => {
    if (details.level === 'error') rendererErrors.push(details.message);
  });
});

runDesktop(electron)
  .then(async (window) => {
    await window.webContents.executeJavaScript(`new Promise((resolve, reject) => {
    const deadline = Date.now() + 5000;
    function check() {
      if (document.querySelector('#root > main')) return resolve();
      if (Date.now() > deadline) return reject(new Error('React did not mount.'));
      setTimeout(check, 20);
    }
    check();
  })`);
    const deadline = Date.now() + 10000;
    while (!rpcHandshakeSent || !rpcHandshakeReceived) {
      if (Date.now() >= deadline) throw new Error('Fusion RPC handshake did not complete.');
      await delay(20);
    }
    const page = await window.webContents.executeJavaScript(`({
    title: document.title,
    mounted: document.querySelector('#root > main')?.getAttribute('aria-label') === 'Gravia Lab',
    content: document.querySelector('.app-title-bar')?.textContent,
    titleBar: (() => {
      const element = document.querySelector('.app-title-bar');
      if (!element) return null;
      const style = getComputedStyle(element);
      return {
        height: element.getBoundingClientRect().height,
        dragRegion: style.getPropertyValue('app-region') || style.getPropertyValue('-webkit-app-region')
      };
    })(),
    nodeAvailable: typeof require !== 'undefined' || typeof process !== 'undefined'
  })`);
    const preferences = window.webContents.getLastWebPreferences();
    console.error(
      'GRAVIA_SMOKE ' +
        JSON.stringify({
          ...page,
          rendererErrors,
          rpcHandshakeSent,
          rpcHandshakeReceived,
          windowTitle: window.getTitle(),
          visible: window.isVisible(),
          url: window.webContents.getURL(),
          sandbox: preferences.sandbox,
          contextIsolation: preferences.contextIsolation,
          pids: electron.app.getAppMetrics().map((metric) => metric.pid),
        }),
    );
    if (process.env.GRAVIA_SMOKE_ACTION === 'close-window') setTimeout(() => window.close(), 500);
  })
  .catch((error) => {
    console.error(error);
    electron.app.exit(1);
  });
