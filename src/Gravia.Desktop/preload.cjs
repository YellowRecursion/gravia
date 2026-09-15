const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('graviaDesktop', {
  chooseDirectory: () => ipcRenderer.invoke('gravia:choose-directory'),
});
