const assert = require('node:assert/strict');
const { test } = require('node:test');
const { registerWindowAppearance } = require('../window-appearance.cjs');

function fixture(platform = 'win32') {
  let handler;
  let closed;
  let removed;
  const changes = [];
  const frame = { url: 'http://127.0.0.1:1234/' };
  const nativeTheme = { themeSource: 'system' };
  const window = {
    isDestroyed: () => false,
    webContents: { mainFrame: frame },
    setBackgroundColor: (color) => changes.push({ background: color }),
    setTitleBarOverlay: (value) => changes.push({ overlay: value }),
    once: (event, callback) => {
      assert.equal(event, 'closed');
      closed = callback;
    },
  };
  const event = { sender: window.webContents, senderFrame: frame };
  const trustWindow = registerWindowAppearance({
    window,
    nativeTheme,
    backendUrl: frame.url,
    platform,
    ipcMain: {
      handle: (channel, callback) => {
        assert.equal(channel, 'gravia:set-window-appearance');
        handler = callback;
      },
      removeHandler: (channel) => {
        removed = channel;
      },
    },
  });
  return {
    window,
    event,
    frame,
    changes,
    nativeTheme,
    trustWindow,
    request: (appearance, source = event) => handler(source, appearance),
    close: () => closed(),
    removed: () => removed,
  };
}

const light = {
  colorScheme: 'light',
  themeSource: 'light',
  backgroundColor: '#ffffff',
  symbolColor: '#000',
};

test('system preference stays system even when the resolved window color is light', () => {
  const target = fixture();
  target.request({ ...light, themeSource: 'system' });
  assert.equal(target.nativeTheme.themeSource, 'system');
  assert.deepEqual(target.changes[0], { background: '#ffffff' });
});

test('appearance routes a registered child only to its own native window', () => {
  const target = fixture();
  const changes = [];
  let closed;
  const child = {
    isDestroyed: () => false,
    webContents: { mainFrame: { url: 'http://127.0.0.1:1234/?window=settings&scope=application' } },
    setBackgroundColor: (color) => changes.push(color),
    setTitleBarOverlay: () => {},
    once: (_event, callback) => {
      closed = callback;
    },
  };
  const source = { sender: child.webContents, senderFrame: child.webContents.mainFrame };
  assert.throws(() => target.request(light, source), /Untrusted/);
  target.trustWindow(child, child.webContents.mainFrame.url);
  target.request(light, source);
  assert.deepEqual(changes, ['#ffffff']);
  assert.deepEqual(target.changes, []);
  closed();
  assert.throws(() => target.request(light, source), /Untrusted/);
});

test('window appearance updates native colors and removes its handler on close', () => {
  const target = fixture();
  target.request(light);
  assert.equal(target.nativeTheme.themeSource, 'light');
  assert.deepEqual(target.changes, [
    { background: '#ffffff' },
    { overlay: { color: '#00000000', symbolColor: '#000' } },
  ]);
  target.close();
  assert.equal(target.removed(), 'gravia:set-window-appearance');
});

test('macOS uses native theme without calling the Windows/Linux overlay API', () => {
  const target = fixture('darwin');
  target.request(light);
  assert.equal(target.nativeTheme.themeSource, 'light');
  assert.deepEqual(target.changes, [{ background: '#ffffff' }]);
});

test('window appearance rejects untrusted windows, frames and navigated senders', () => {
  const target = fixture();
  assert.throws(
    () => target.request(light, { sender: {}, senderFrame: target.frame }),
    /Untrusted/,
  );
  assert.throws(
    () =>
      target.request(light, {
        sender: target.window.webContents,
        senderFrame: { ...target.frame },
      }),
    /Untrusted/,
  );
  target.frame.url = 'https://example.com/';
  assert.throws(() => target.request(light), /Untrusted/);
  target.window.isDestroyed = () => true;
  assert.throws(() => target.request(light), /Untrusted/);
  assert.deepEqual(target.changes, []);
});

test('window appearance accepts only explicit modes and hex colors', () => {
  const target = fixture();
  for (const value of [
    null,
    {},
    { ...light, colorScheme: 'system' },
    { ...light, themeSource: 'invalid' },
    { ...light, themeSource: undefined },
    { ...light, backgroundColor: 'red' },
    { ...light, symbolColor: 'url(file:///x)' },
    { ...light, symbolColor: 42 },
  ]) {
    assert.throws(() => target.request(value), /Invalid/);
  }
  assert.deepEqual(target.changes, []);
});
