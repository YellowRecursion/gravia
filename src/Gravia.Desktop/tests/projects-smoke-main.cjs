// Loaded only by ProjectsDesktopTests. The Host and both data directories are isolated.
// Only native dialog choices are stubbed; DOM events, preload, IPC and RPC are real.
const electron = require('electron');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { setTimeout: delay } = require('node:timers/promises');
const { runDesktop } = require('./shell.cjs');

electron.app.setPath('userData', path.join(__dirname, 'profile'));
electron.app.setPath('sessionData', path.join(__dirname, 'profile'));
const projects = path.resolve(__dirname, '../projects');
const choices = [];
const errors = [];
electron.app.on('web-contents-created', (_event, contents) => {
  contents.on('console-message', (details) => {
    if (details.level === 'error') errors.push(details.message);
  });
});

runDesktop({
  ...electron,
  dialog: {
    showOpenDialog: async () => {
      assert.ok(choices.length, 'Unexpected directory picker.');
      const choice = choices.shift();
      return { canceled: choice === null, filePaths: choice === null ? [] : [choice] };
    },
  },
})
  .then(async (window) => {
    const evaluate = (script) => window.webContents.executeJavaScript(script);
    async function until(expression) {
      const deadline = Date.now() + 10000;
      while (!(await evaluate(expression))) {
        if (Date.now() >= deadline)
          throw new Error(`Timed out: ${expression}\n${await evaluate('document.body.innerText')}`);
        await delay(30);
      }
    }
    async function click(selector) {
      await until(
        `document.querySelector(${JSON.stringify(selector)}) && !document.querySelector(${JSON.stringify(selector)}).disabled`,
      );
      await evaluate(`document.querySelector(${JSON.stringify(selector)}).click()`);
    }
    async function fill(selector, value) {
      await evaluate(`(() => {
      const input = document.querySelector(${JSON.stringify(selector)});
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(input, ${JSON.stringify(value)});
      input.dispatchEvent(new Event('input', { bubbles: true }));
    })()`);
    }
    async function main(name) {
      await until(
        `!document.querySelector('.projects-screen') && document.querySelector('.app-title-bar__project-name')?.textContent === ${JSON.stringify(name)}`,
      );
      assert.equal(
        await evaluate(`document.querySelector('[aria-label="Основной экран"]').textContent`),
        '',
      );
    }
    async function showProjects(name) {
      await click('.app-title-bar__project');
      await until(
        `document.querySelector('.projects-screen') && document.querySelector('.ant-menu-item-selected')?.textContent.startsWith(${JSON.stringify(name)})`,
      );
    }
    async function selectRecent(name) {
      await evaluate(
        `Array.from(document.querySelectorAll('.ant-menu-item')).find(item => item.textContent.startsWith(${JSON.stringify(name)})).click()`,
      );
      await main(name);
    }
    const browse = '[aria-label="Выбрать каталог"]';
    const open = '[aria-label="Открыть другой проект…"]';
    const submit = 'button[type="submit"]';
    await until(`document.body.innerText.includes('Пока нет недавних проектов')`);
    assert.equal(await evaluate(`document.querySelector('.ant-menu-item-selected')`), null);
    assert.equal(await evaluate(`document.querySelector('.app-title-bar__project')`), null);

    // Cancellation leaves the initial screen untouched, with no accidental Open command.
    choices.push(null);
    await click(open);
    await until(`!document.querySelector(${JSON.stringify(open)}).disabled`);
    assert.equal(await evaluate(`document.querySelector('[role="alert"]')`), null);

    // Validation really comes back from C#, including its PascalCase field names.
    await click(submit);
    await until(`document.querySelectorAll('.ant-form-item-explain-error').length === 2`);
    await fill('#project-name', 'Альфа');
    choices.push(projects);
    await click(browse);
    await until(
      `document.querySelector('#project-location').value === ${JSON.stringify(projects)}`,
    );
    choices.push(null);
    await click(browse);
    await until(`!document.querySelector(${JSON.stringify(browse)}).disabled`);
    assert.equal(await evaluate(`document.querySelector('#project-location').value`), projects);
    await click(submit);
    await main('Альфа');

    const titleButton = await evaluate(`(() => {
    const button = document.querySelector('.app-title-bar__project');
    const logo = document.querySelector('.app-title-bar__title');
    return { drag: getComputedStyle(button).getPropertyValue('app-region'), rightOfLogo: button.getBoundingClientRect().left >= logo.getBoundingClientRect().right };
  })()`);
    assert.deepEqual(titleButton, { drag: 'no-drag', rightOfLogo: true });
    await showProjects('Альфа');

    // Existing-directory error keeps the current session and form contents.
    await fill('#project-name', 'Альфа');
    await fill('#project-location', projects);
    await click(submit);
    await until(
      `document.body.innerText.includes('Папка или файл с таким именем уже существует.')`,
    );
    assert.equal(
      await evaluate(`document.querySelector('.app-title-bar__project-name').textContent`),
      'Альфа',
    );
    assert.equal(await evaluate(`document.querySelector('#project-location').value`), projects);
    await fill('#project-name', 'Бета');
    await click(submit);
    await main('Бета');
    await showProjects('Бета');
    assert.equal(await evaluate(`document.querySelectorAll('.ant-menu-item').length`), 2);
    await selectRecent('Альфа');
    await showProjects('Альфа');

    // Failed opening does not select the failed target or close the current project.
    choices.push(path.join(projects, 'missing'));
    await click(open);
    await until(`document.body.innerText.includes('Каталог не существует или недоступен.')`);
    assert.equal(
      await evaluate(`document.querySelector('.app-title-bar__project-name').textContent`),
      'Альфа',
    );
    assert.equal(await evaluate(`document.querySelectorAll('.ant-menu-item-selected').length`), 1);
    choices.push(path.join(projects, 'Бета'));
    await click(open);
    await main('Бета');

    // A fresh renderer gets the session from Host, rather than local UI state.
    await new Promise((resolve) => {
      window.webContents.once('did-finish-load', resolve);
      window.reload();
    });
    await main('Бета');
    await showProjects('Бета');
    await selectRecent('Бета');
    assert.deepEqual(choices, []);
    assert.deepEqual(errors, []);
    fs.writeFileSync(path.join(__dirname, 'result.json'), JSON.stringify({ success: true }));
    window.close();
  })
  .catch((error) => {
    fs.writeFileSync(
      path.join(__dirname, 'result.json'),
      JSON.stringify({ success: false, error: error.stack, rendererErrors: errors }),
    );
    electron.app.exit(1);
  });
