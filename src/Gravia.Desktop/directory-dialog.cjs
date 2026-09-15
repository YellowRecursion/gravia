const CHANNEL = 'gravia:choose-directory';

function registerDirectoryDialog({ ipcMain, dialog, window, backendUrl }) {
  let pending;
  ipcMain.handle(CHANNEL, (event) => {
    if (
      window.isDestroyed() ||
      event.sender !== window.webContents ||
      event.senderFrame !== window.webContents.mainFrame ||
      event.senderFrame.url !== backendUrl
    ) {
      throw new Error('Untrusted directory dialog request.');
    }
    pending ??= dialog
      .showOpenDialog(window, { properties: ['openDirectory'] })
      .then((result) => (result.canceled ? null : (result.filePaths[0] ?? null)))
      .finally(() => {
        pending = undefined;
      });
    return pending;
  });
  window.once('closed', () => ipcMain.removeHandler(CHANNEL));
}

module.exports = { registerDirectoryDialog };
