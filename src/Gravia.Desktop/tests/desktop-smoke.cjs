// Explicit opt-in integration test: opens and closes real Electron windows.
const assert = require('node:assert/strict');
const { spawn, execFileSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { setTimeout: delay } = require('node:timers/promises');
const electron = require('electron');

const desktop = path.resolve(__dirname, '..');
const host = path.resolve(desktop, '../Gravia.Host/bin/Release/net10.0/Gravia.Host.dll');
const builtDesktop = path.join(path.dirname(host), 'Desktop');

function alive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

async function until(predicate, timeout = 20000) {
  const deadline = Date.now() + timeout;
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error('Smoke test timed out.');
    await delay(100);
  }
}

async function scenario(action) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'gravia-desktop-smoke-'));
  let child;
  let pids = [];
  let output = '';
  try {
    for (const file of ['shell.cjs', 'bootstrap.cjs'])
      fs.copyFileSync(path.join(builtDesktop, file), path.join(directory, file));
    fs.cpSync(path.join(builtDesktop, 'wwwroot'), path.join(directory, 'wwwroot'), {
      recursive: true,
    });
    fs.copyFileSync(path.join(__dirname, 'smoke-main.cjs'), path.join(directory, 'main.cjs'));
    fs.writeFileSync(
      path.join(directory, 'package.json'),
      JSON.stringify({ name: 'gravia-smoke', main: 'main.cjs' }),
    );
    child = spawn(
      'dotnet',
      [
        host,
        `--Desktop:AppDirectory=${directory}`,
        `--Desktop:ExecutablePath=${electron}`,
        '--Logging:LogLevel:Default=Debug',
      ],
      {
        cwd: os.tmpdir(),
        windowsHide: true,
        env: { ...process.env, GRAVIA_SMOKE_ACTION: action },
        stdio: ['ignore', 'pipe', 'pipe'],
      },
    );
    let exited = false;
    let exitCode;
    child.on('exit', (code) => {
      exited = true;
      exitCode = code;
    });
    const capture = (data) => {
      output = (output + data.toString()).slice(-1024 * 1024);
    };
    child.stdout.on('data', capture);
    child.stderr.on('data', capture);
    await until(() => output.includes('GRAVIA_SMOKE ') || exited);
    const match = output.match(/GRAVIA_SMOKE (\{[^\r\n]+\})/);
    assert.ok(match, `No shell verification received:\n${output}`);
    const state = JSON.parse(match[1]);
    pids = state.pids;
    assert.equal(state.title, 'Gravia Lab');
    assert.equal(state.windowTitle, 'Gravia Lab');
    assert.equal(state.mounted, true);
    assert.equal(state.content, '');
    assert.deepEqual(state.rendererErrors, []);
    assert.equal(state.nodeAvailable, false);
    assert.equal(state.visible, true);
    assert.equal(state.sandbox, true);
    assert.equal(state.contextIsolation, true);
    assert.equal(new URL(state.url).hostname, '127.0.0.1');
    if (action === 'kill-host') child.kill('SIGKILL'); // Parent only: verify control-pipe EOF cleanup in Electron.
    await until(() => exited);
    if (action === 'close-window') assert.equal(exitCode, 0, output);
    await until(() => pids.every((pid) => !alive(pid)), 10000);
    console.log(
      `${action}: blank visible window, isolated renderer, Host stopped, ${pids.length} Electron processes stopped.`,
    );
  } finally {
    if (child && child.exitCode === null && child.signalCode === null) child.kill('SIGKILL');
    for (const pid of pids) {
      if (!alive(pid)) continue;
      if (process.platform === 'win32') {
        try {
          execFileSync('taskkill', ['/PID', String(pid), '/T', '/F'], {
            windowsHide: true,
            stdio: 'ignore',
          });
        } catch {}
      } else {
        try {
          process.kill(pid, 'SIGKILL');
        } catch {}
      }
    }
    // Only the exact mkdtemp directory created above is removed.
    assert.equal(path.dirname(path.resolve(directory)), path.resolve(os.tmpdir()));
    assert.ok(path.basename(directory).startsWith('gravia-desktop-smoke-'));
    fs.rmSync(directory, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
  }
}

(async () => {
  assert.ok(fs.existsSync(host), 'Build Gravia.Host in Release before running smoke tests.');
  await scenario('close-window');
  await scenario('kill-host');
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
