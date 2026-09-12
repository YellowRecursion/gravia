// Copied into a temporary app by desktop-smoke.cjs. Never shipped with the shell.
const electron = require('electron');
const path = require('node:path');
const { runDesktop } = require('./shell.cjs');
electron.app.setPath('userData', path.join(__dirname, 'profile'));
electron.app.setPath('sessionData', path.join(__dirname, 'profile'));
const rendererErrors = [];
electron.app.on('web-contents-created', (_event, contents) => {
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
    const page = await window.webContents.executeJavaScript(`({
    title: document.title,
    mounted: document.querySelector('#root > main')?.getAttribute('aria-label') === 'Gravia Lab',
    content: document.querySelector('#root')?.textContent,
    nodeAvailable: typeof require !== 'undefined' || typeof process !== 'undefined'
  })`);
    const preferences = window.webContents.getLastWebPreferences();
    console.error(
      'GRAVIA_SMOKE ' +
        JSON.stringify({
          ...page,
          rendererErrors,
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
