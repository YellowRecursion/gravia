const electron = require('electron');
const { runDesktop } = require('./shell.cjs');

runDesktop(electron).catch((error) => {
  console.error(error);
  electron.app.exit(1);
});
