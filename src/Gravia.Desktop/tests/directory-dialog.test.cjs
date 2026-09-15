const assert = require('node:assert/strict');
const { test } = require('node:test');
const { registerDirectoryDialog } = require('../directory-dialog.cjs');

function fixture(showOpenDialog = async () => ({ canceled: false, filePaths: ['/projects'] })) {
  let handler;
  let closed;
  let removed;
  const frame = { url: 'http://127.0.0.1:1234/' };
  const window = {
    isDestroyed: () => false,
    webContents: { mainFrame: frame },
    once: (name, callback) => {
      assert.equal(name, 'closed');
      closed = callback;
    },
  };
  registerDirectoryDialog({
    window,
    backendUrl: frame.url,
    ipcMain: {
      handle: (name, callback) => {
        assert.equal(name, 'gravia:choose-directory');
        handler = callback;
      },
      removeHandler: (name) => {
        removed = name;
      },
    },
    dialog: { showOpenDialog },
  });
  return {
    window,
    frame,
    request: (event = { sender: window.webContents, senderFrame: frame }) => handler(event),
    close: () => closed(),
    removed: () => removed,
  };
}

test('directory picker is owned by the window and returns only the selected path', async () => {
  const target = fixture(async (parent, options) => {
    assert.equal(parent, target.window);
    assert.deepEqual(options, { properties: ['openDirectory'] });
    return { canceled: false, filePaths: ['/projects', '/ignored'] };
  });
  assert.equal(await target.request(), '/projects');
  target.close();
  assert.equal(target.removed(), 'gravia:choose-directory');
});

test('canceled and empty directory selections return null', async () => {
  assert.equal(
    await fixture(async () => ({ canceled: true, filePaths: ['/ignored'] })).request(),
    null,
  );
  assert.equal(await fixture(async () => ({ canceled: false, filePaths: [] })).request(), null);
});

test('directory picker rejects another window, subframe, changed URL and destroyed window', () => {
  const target = fixture();
  assert.throws(() => target.request({ sender: {}, senderFrame: target.frame }), /Untrusted/);
  assert.throws(
    () => target.request({ sender: target.window.webContents, senderFrame: { ...target.frame } }),
    /Untrusted/,
  );
  target.frame.url = 'https://example.com/';
  assert.throws(() => target.request(), /Untrusted/);
  target.window.isDestroyed = () => true;
  assert.throws(() => target.request(), /Untrusted/);
});

test('repeated directory requests share one pending dialog', async () => {
  let finish;
  let calls = 0;
  const target = fixture(() => {
    calls++;
    return new Promise((resolve) => {
      finish = resolve;
    });
  });
  const first = target.request();
  assert.equal(target.request(), first);
  assert.equal(calls, 1);
  finish({ canceled: true, filePaths: [] });
  await first;
  const next = target.request();
  assert.equal(calls, 2);
  finish({ canceled: true, filePaths: [] });
  await next;
});

test('a failed dialog does not block subsequent requests', async () => {
  let calls = 0;
  const target = fixture(async () => {
    if (++calls === 1) throw new Error('Dialog failed');
    return { canceled: true, filePaths: [] };
  });
  await assert.rejects(target.request(), /Dialog failed/);
  assert.equal(await target.request(), null);
});
