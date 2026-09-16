const path = require('node:path');

function createAppWindow(BrowserWindow, options = {}) {
  const window = new BrowserWindow({
    title: 'Gravia Lab',
    width: 1200,
    height: 800,
    minWidth: 640,
    minHeight: 480,
    ...options,
    show: false,
    backgroundColor: '#141414',
    titleBarStyle: 'hidden',
    titleBarOverlay: { color: '#00000000', symbolColor: '#ffffff', height: 40 },
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      sandbox: true,
      preload: path.join(__dirname, 'preload.cjs'),
    },
  });
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  window.webContents.on('will-navigate', (event) => event.preventDefault());
  window.webContents.on('will-redirect', (event) => event.preventDefault());
  window.webContents.on('will-attach-webview', (event) => event.preventDefault());
  return window;
}

module.exports = { createAppWindow };
