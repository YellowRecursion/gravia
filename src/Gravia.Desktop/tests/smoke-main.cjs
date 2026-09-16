// Copied into a temporary app by desktop-smoke.cjs. Never shipped with the shell.
const electron = require('electron');
const path = require('node:path');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const http = require('node:http');
const { setTimeout: delay } = require('node:timers/promises');
const { runDesktop } = require('./shell.cjs');
electron.app.setPath('userData', path.join(__dirname, 'profile'));
electron.app.setPath('sessionData', path.join(__dirname, 'profile'));
if (process.env.GRAVIA_SMOKE_SOURCE) {
  for (const event of ['before-quit', 'will-quit', 'quit', 'window-all-closed'])
    electron.app.on(event, () =>
      fs.appendFileSync(path.join(__dirname, 'lifetime.txt'), `${event}\n`),
    );
  process.on('uncaughtExceptionMonitor', (error) =>
    fs.appendFileSync(path.join(__dirname, 'lifetime.txt'), `${error.stack}\n`),
  );
}
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
    const deadline = Date.now() + 60000;
    function check() {
      if (document.querySelector('#root .app-shell')) return resolve();
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
    // Closing/killing the main application must also terminate an open settings renderer.
    await window.webContents.executeJavaScript(`window.graviaDesktop.openSettings('application')`);
    const settings = electron.BrowserWindow.getAllWindows().find(
      (candidate) => candidate !== window,
    );
    if (!settings?.isVisible()) throw new Error('Settings window did not become visible.');
    let hotReloadVerified = false;
    if (process.env.GRAVIA_SMOKE_SOURCE) {
      const source = process.env.GRAVIA_SMOKE_SOURCE;
      async function rpcUpgradeStatus(origin) {
        return new Promise((resolve, reject) => {
          const request = http.request(new URL('/rpc/ws?f=json5np', window.webContents.getURL()), {
            headers: {
              Origin: origin,
              Connection: 'Upgrade',
              Upgrade: 'websocket',
              'Sec-WebSocket-Version': '13',
              'Sec-WebSocket-Key': 'dGhlIHNhbXBsZSBub25jZQ==',
            },
          });
          request.on('upgrade', (response, socket) => {
            socket.destroy();
            resolve(response.statusCode);
          });
          request.on('response', (response) => {
            response.resume();
            resolve(response.statusCode);
          });
          request.on('error', reject);
          request.setTimeout(5000, () => request.destroy(new Error('RPC origin check timed out.')));
          request.end();
        });
      }
      assert.equal(await rpcUpgradeStatus(new URL(window.webContents.getURL()).origin), 101);
      assert.equal(await rpcUpgradeStatus('https://example.com'), 403);
      // These are disposable copies, never files in the user's active checkout.
      async function untilIn(target, expression) {
        const deadline = Date.now() + 30000;
        while (!(await target.webContents.executeJavaScript(expression))) {
          if (Date.now() > deadline)
            throw new Error(`HMR timed out: ${expression}\n${rendererErrors.join('\n')}`);
          await delay(50);
        }
      }
      await untilIn(settings, `!!document.querySelector('.settings-screen')`);
      const rendererPids = [
        window.webContents.getOSProcessId(),
        settings.webContents.getOSProcessId(),
      ];
      for (const target of [window, settings])
        await target.webContents.executeJavaScript(`window.__hmrSmokeMarker = 'same-page'`);
      const css = path.join(source, 'renderer/app/ui/AppTitleBar.css');
      fs.appendFileSync(css, '\n.app-title-bar { --gravia-hmr-smoke: verified; }\n');
      for (const target of [window, settings])
        await untilIn(
          target,
          `getComputedStyle(document.querySelector('.app-title-bar')).getPropertyValue('--gravia-hmr-smoke').trim() === 'verified'`,
        );
      const component = path.join(source, 'renderer/app/ui/AppTitleBar.tsx');
      const original = fs.readFileSync(component, 'utf8');
      assert.ok(original.includes('<header className="app-title-bar">'));
      fs.writeFileSync(
        component,
        original.replace(
          '<header className="app-title-bar">',
          '<header className="app-title-bar" data-hmr-smoke="verified">',
        ),
      );
      for (const target of [window, settings]) {
        await untilIn(
          target,
          `document.querySelector('.app-title-bar')?.dataset.hmrSmoke === 'verified'`,
        );
        assert.equal(
          await target.webContents.executeJavaScript(`window.__hmrSmokeMarker`),
          'same-page',
        );
      }
      assert.deepEqual(
        [window.webContents.getOSProcessId(), settings.webContents.getOSProcessId()],
        rendererPids,
      );
      hotReloadVerified = true;
    }
    const page = await window.webContents.executeJavaScript(`({
    title: document.title,
    mounted: document.querySelector('#root .app-shell')?.getAttribute('aria-label') === 'Gravia Lab',
    content: document.querySelector('.app-title-bar__title')?.textContent,
    desktopApi: Object.keys(window.graviaDesktop ?? {}),
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
          hotReloadVerified,
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
