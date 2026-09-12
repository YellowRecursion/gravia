// Copied into a temporary app by desktop-smoke.cjs. Never shipped with the shell.
const electron = require('electron');
const path = require('node:path');
const { runDesktop } = require('./shell.cjs');
electron.app.setPath('userData', path.join(__dirname, 'profile'));
electron.app.setPath('sessionData', path.join(__dirname, 'profile'));

runDesktop(electron).then(async (window) => {
  const page = await window.webContents.executeJavaScript(`({
    title: document.title,
    children: document.body.children.length,
    nodeAvailable: typeof require !== 'undefined' || typeof process !== 'undefined'
  })`);
  const preferences = window.webContents.getLastWebPreferences();
  console.error('GRAVIA_SMOKE ' + JSON.stringify({
    ...page, windowTitle: window.getTitle(), visible: window.isVisible(), url: window.webContents.getURL(),
    sandbox: preferences.sandbox, contextIsolation: preferences.contextIsolation,
    pids: electron.app.getAppMetrics().map((metric) => metric.pid)
  }));
  if (process.env.GRAVIA_SMOKE_ACTION === 'close-window')
    setTimeout(() => window.close(), 500);
}).catch((error) => {
  console.error(error);
  electron.app.exit(1);
});
