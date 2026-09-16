const { createAppWindow } = require('./app-window.cjs');
const CHANNEL = 'gravia:open-settings';

function registerSettingsWindows({
  ipcMain,
  BrowserWindow,
  window,
  backendUrl,
  trustAppearanceWindow,
}) {
  const windows = new Map();
  ipcMain.handle(CHANNEL, async (event, scope) => {
    if (
      window.isDestroyed() ||
      event.sender !== window.webContents ||
      event.senderFrame !== window.webContents.mainFrame ||
      event.senderFrame.url !== backendUrl
    )
      throw new Error('Untrusted settings window request.');
    if (scope !== 'application' && scope !== 'project') throw new Error('Invalid settings scope.');

    const existing = windows.get(scope);
    if (existing) {
      await existing.ready;
      if (!existing.window.isDestroyed()) {
        if (existing.window.isMinimized()) existing.window.restore();
        existing.window.show();
        existing.window.focus();
      }
      return;
    }

    // The renderer chooses a known window kind, never its URL, preload or window options.
    const url = new URL(backendUrl);
    url.searchParams.set('window', 'settings');
    url.searchParams.set('scope', scope);
    const child = createAppWindow(BrowserWindow, { parent: window, width: 1100, height: 760 });
    trustAppearanceWindow(child, url.href);
    const entry = { window: child, ready: undefined };
    windows.set(scope, entry);
    child.once('closed', () => {
      if (windows.get(scope) === entry) windows.delete(scope);
    });
    child.webContents.on('render-process-gone', () => {
      if (!child.isDestroyed()) child.destroy();
    });
    entry.ready = child.loadURL(url.href);
    try {
      await entry.ready;
      if (!child.isDestroyed() && !window.isDestroyed()) {
        child.show();
        child.focus();
      }
    } catch (error) {
      if (!child.isDestroyed()) child.destroy();
      throw error;
    }
  });
  window.once('closed', () => {
    ipcMain.removeHandler(CHANNEL);
    for (const entry of windows.values()) {
      if (!entry.window.isDestroyed()) entry.window.destroy();
    }
    windows.clear();
  });
}

module.exports = { registerSettingsWindows };
