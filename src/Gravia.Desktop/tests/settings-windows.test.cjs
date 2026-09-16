const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const { test } = require('node:test');
const { registerSettingsWindows } = require('../settings-windows.cjs');

function fixture() {
  const children = [];
  const trusted = [];
  let handler;
  let removed;
  class Window extends EventEmitter {
    constructor(options) {
      super();
      this.options = options;
      this.destroyed = false;
      this.focusCount = 0;
      this.minimized = false;
      this.webContents = new EventEmitter();
      this.webContents.mainFrame = { url: 'http://127.0.0.1:1234/' };
      this.webContents.setWindowOpenHandler = (callback) => {
        this.openHandler = callback;
      };
      if (options) children.push(this);
    }
    isDestroyed() {
      return this.destroyed;
    }
    isMinimized() {
      return this.minimized;
    }
    restore() {
      this.minimized = false;
    }
    show() {
      this.visible = true;
    }
    focus() {
      this.focusCount++;
    }
    async loadURL(url) {
      this.url = url;
    }
    destroy() {
      this.destroyed = true;
      this.emit('closed');
    }
  }
  const parent = new Window();
  registerSettingsWindows({
    window: parent,
    BrowserWindow: Window,
    backendUrl: parent.webContents.mainFrame.url,
    trustAppearanceWindow: (window, url) => trusted.push({ window, url }),
    ipcMain: {
      handle: (channel, callback) => {
        assert.equal(channel, 'gravia:open-settings');
        handler = callback;
      },
      removeHandler: (channel) => {
        removed = channel;
      },
    },
  });
  const event = { sender: parent.webContents, senderFrame: parent.webContents.mainFrame };
  return {
    parent,
    children,
    trusted,
    request: (scope, source = event) => handler(source, scope),
    removed: () => removed,
    Window,
  };
}

test('known settings scopes open isolated nonmodal owned windows and reuse them', async () => {
  const target = fixture();
  await Promise.all([target.request('application'), target.request('application')]);
  const child = assertSingle(target.children);
  assert.equal(child.url, 'http://127.0.0.1:1234/?window=settings&scope=application');
  assert.equal(child.options.parent, target.parent);
  assert.notEqual(child.options.modal, true);
  assert.equal(child.options.webPreferences.sandbox, true);
  assert.equal(child.options.webPreferences.contextIsolation, true);
  assert.equal(child.options.webPreferences.nodeIntegration, false);
  assert.deepEqual(child.openHandler(), { action: 'deny' });
  assert.equal(child.visible, true);
  assert.equal(target.trusted[0].window, child);
  assert.equal(target.trusted[0].url, child.url);
  child.minimized = true;
  await target.request('application');
  assert.equal(child.isMinimized(), false);
  assert.equal(child.focusCount, 3);
  await target.request('project');
  assert.equal(target.children.length, 2);
  target.parent.destroy();
  assert.ok(target.children.every((window) => window.isDestroyed()));
  assert.equal(target.removed(), 'gravia:open-settings');
});

function assertSingle(items) {
  assert.equal(items.length, 1);
  return items[0];
}

test('closing a settings window allows reopening without closing the main window', async () => {
  const target = fixture();
  await target.request('project');
  target.children[0].destroy();
  assert.equal(target.parent.isDestroyed(), false);
  await target.request('project');
  assert.equal(target.children.length, 2);
  assert.equal(target.children[1].isDestroyed(), false);
});

test('settings rejects unknown scopes, other windows and subframes', async () => {
  const target = fixture();
  for (const scope of [null, {}, 'https://example.com', 'unknown']) {
    await assert.rejects(target.request(scope), /Invalid settings/);
  }
  await assert.rejects(target.request('application', { sender: {} }), /Untrusted/);
  await assert.rejects(
    target.request('application', {
      sender: target.parent.webContents,
      senderFrame: { ...target.parent.webContents.mainFrame },
    }),
    /Untrusted/,
  );
  target.parent.webContents.mainFrame.url = 'https://example.com';
  await assert.rejects(target.request('application'), /Untrusted/);
  assert.equal(target.children.length, 0);
});

test('failed loading destroys the unfinished window and permits a retry', async () => {
  const target = fixture();
  target.Window.prototype.loadURL = async () => {
    throw new Error('Load failed');
  };
  await assert.rejects(target.request('application'), /Load failed/);
  assert.equal(target.children[0].isDestroyed(), true);
  target.Window.prototype.loadURL = async function (url) {
    this.url = url;
  };
  await target.request('application');
  assert.equal(target.children.length, 2);
  assert.equal(target.children[1].visible, true);
});
