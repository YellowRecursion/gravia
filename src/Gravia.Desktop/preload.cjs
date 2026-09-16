const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('graviaDesktop', {
  chooseDirectory: () => ipcRenderer.invoke('gravia:choose-directory'),
  openSettings: (scope) => ipcRenderer.invoke('gravia:open-settings', scope),
  setWindowAppearance: (appearance) =>
    ipcRenderer.invoke('gravia:set-window-appearance', appearance),
});
