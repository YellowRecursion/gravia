const CHANNEL = 'gravia:set-window-appearance';
const HEX_COLOR = /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i;

function registerWindowAppearance({
  ipcMain,
  nativeTheme,
  window,
  backendUrl,
  platform = process.platform,
}) {
  const windows = new Map();
  function trustWindow(target, url) {
    const contents = target.webContents;
    windows.set(contents, { window: target, url });
    target.once('closed', () => windows.delete(contents));
  }
  trustWindow(window, backendUrl);
  ipcMain.handle(CHANNEL, (event, appearance) => {
    const target = windows.get(event.sender);
    if (
      !target ||
      target.window.isDestroyed() ||
      event.senderFrame !== target.window.webContents.mainFrame ||
      event.senderFrame.url !== target.url
    )
      throw new Error('Untrusted window appearance request.');
    if (
      !appearance ||
      !['dark', 'light'].includes(appearance.colorScheme) ||
      !['system', 'dark', 'light'].includes(appearance.themeSource) ||
      typeof appearance.backgroundColor !== 'string' ||
      !HEX_COLOR.test(appearance.backgroundColor) ||
      typeof appearance.symbolColor !== 'string' ||
      !HEX_COLOR.test(appearance.symbolColor)
    )
      throw new Error('Invalid window appearance.');

    // Keep system preference observable: a resolved color would otherwise force matchMedia.
    nativeTheme.themeSource = appearance.themeSource;
    target.window.setBackgroundColor(appearance.backgroundColor);
    if (platform !== 'darwin') {
      target.window.setTitleBarOverlay({ color: '#00000000', symbolColor: appearance.symbolColor });
    }
  });
  window.once('closed', () => ipcMain.removeHandler(CHANNEL));
  return trustWindow;
}

module.exports = { registerWindowAppearance };
