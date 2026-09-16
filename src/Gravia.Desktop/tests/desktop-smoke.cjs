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

async function scenario(action, hotReload = false) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'gravia-desktop-smoke-'));
  let child;
  let pids = [];
  let output = '';
  try {
    for (const file of [
      'shell.cjs',
      'bootstrap.cjs',
      'preload.cjs',
      'directory-dialog.cjs',
      'window-appearance.cjs',
      'app-window.cjs',
      'settings-windows.cjs',
      'dev-server.cjs',
    ])
      fs.copyFileSync(path.join(builtDesktop, file), path.join(directory, file));
    fs.cpSync(path.join(builtDesktop, 'wwwroot'), path.join(directory, 'wwwroot'), {
      recursive: true,
    });
    fs.copyFileSync(path.join(__dirname, 'smoke-main.cjs'), path.join(directory, 'main.cjs'));
    fs.writeFileSync(
      path.join(directory, 'package.json'),
      JSON.stringify({ name: 'gravia-smoke', main: 'main.cjs' }),
    );
    const source = path.join(directory, 'source');
    if (hotReload) {
      fs.mkdirSync(source);
      fs.cpSync(path.join(desktop, 'renderer'), path.join(source, 'renderer'), { recursive: true });
      for (const file of ['vite.config.ts', 'package.json', 'tsconfig.base.json'])
        fs.copyFileSync(path.join(desktop, file), path.join(source, file));
      fs.symlinkSync(
        path.join(desktop, 'node_modules'),
        path.join(source, 'node_modules'),
        process.platform === 'win32' ? 'junction' : 'dir',
      );
    }
    child = spawn(
      'dotnet',
      [
        host,
        `--Desktop:AppDirectory=${directory}`,
        `--Desktop:ExecutablePath=${electron}`,
        '--Logging:LogLevel:Default=Debug',
        ...(hotReload
          ? [
              '--environment=Development',
              '--Desktop:HotReload=true',
              `--Desktop:SourceDirectory=${source}`,
            ]
          : []),
      ],
      {
        cwd: os.tmpdir(),
        windowsHide: true,
        env: {
          ...process.env,
          GRAVIA_SMOKE_ACTION: action,
          GRAVIA_SMOKE_SOURCE: hotReload ? source : '',
        },
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
    try {
      await until(() => output.includes('GRAVIA_SMOKE ') || exited, hotReload ? 90000 : 20000);
    } catch (error) {
      throw new Error(`${error.message}\n${output}`, { cause: error });
    }
    const match = output.match(/GRAVIA_SMOKE (\{[^\r\n]+\})/);
    assert.ok(match, `No shell verification received:\n${output}`);
    const state = JSON.parse(match[1]);
    pids = state.pids;
    assert.equal(state.title, 'Gravia Lab');
    assert.equal(state.windowTitle, 'Gravia Lab');
    assert.equal(state.mounted, true);
    assert.equal(state.content, 'Gravia Lab');
    assert.deepEqual(state.titleBar, { height: 40, dragRegion: 'drag' });
    assert.deepEqual(state.rendererErrors, []);
    assert.equal(state.rpcHandshakeSent, true);
    assert.equal(state.rpcHandshakeReceived, true);
    assert.equal(state.nodeAvailable, false);
    assert.deepEqual(state.desktopApi, ['chooseDirectory', 'openSettings', 'setWindowAppearance']);
    assert.equal(state.visible, true);
    assert.equal(state.sandbox, true);
    assert.equal(state.contextIsolation, true);
    assert.equal(new URL(state.url).hostname, '127.0.0.1');
    assert.equal(state.hotReloadVerified, hotReload);
    if (action === 'kill-host') child.kill('SIGKILL'); // Parent only: verify control-pipe EOF cleanup in Electron.
    await until(() => exited);
    if (action === 'close-window') assert.equal(exitCode, 0, output);
    try {
      await until(() => pids.every((pid) => !alive(pid)), 10000);
    } catch (error) {
      throw new Error(
        `Electron processes still alive: ${pids.filter(alive).join(', ')}\nLifetime: ${fs.existsSync(path.join(directory, 'lifetime.txt')) ? fs.readFileSync(path.join(directory, 'lifetime.txt'), 'utf8') : 'none'}\n${output}`,
        { cause: error },
      );
    }
    if (hotReload) await assert.rejects(fetch(state.url, { signal: AbortSignal.timeout(2000) }));
    console.log(
      `${hotReload ? 'dev ' : ''}${action}: custom title bar visible, isolated renderer, Host stopped, ${pids.length} Electron processes stopped.`,
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
    // Unlink the exact dependency junction before removing the fixture; never traverse node_modules.
    const dependencyLink = path.join(directory, 'source', 'node_modules');
    if (fs.existsSync(dependencyLink)) fs.unlinkSync(dependencyLink);
    await fs.promises.rm(directory, {
      recursive: true,
      force: true,
      maxRetries: 10,
      retryDelay: 300,
    });
  }
}

(async () => {
  assert.ok(fs.existsSync(host), 'Build Gravia.Host in Release before running smoke tests.');
  await scenario('close-window');
  await scenario('kill-host');
  await scenario('close-window', true);
  await scenario('kill-host', true);
  // Real project workflow, with temporary Host storage; never creates user projects.
  execFileSync(
    'dotnet',
    [
      'test',
      path.resolve(desktop, '../../tests/Gravia.Host.Tests/Gravia.Host.Tests.csproj'),
      '--filter',
      'FullyQualifiedName~ProjectsDesktopTests',
      '-p:SkipDesktopRestore=true',
    ],
    {
      cwd: desktop,
      windowsHide: true,
      env: { ...process.env, GRAVIA_PROJECTS_DESKTOP_SMOKE: '1' },
      stdio: 'inherit',
      timeout: 120000,
    },
  );
})().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
