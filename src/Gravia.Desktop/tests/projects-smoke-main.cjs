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
    async function settingsItem(scope) {
      await click('[aria-label="Настройки"]');
      const label = scope === 'application' ? 'Настройки приложения' : 'Настройки проекта';
      await until(
        `Array.from(document.querySelectorAll('.ant-dropdown:not(.ant-dropdown-hidden) [role="menuitem"]')).some(item => item.textContent === ${JSON.stringify(label)})`,
      );
      return `Array.from(document.querySelectorAll('.ant-dropdown:not(.ant-dropdown-hidden) [role="menuitem"]')).find(item => item.textContent === ${JSON.stringify(label)})`;
    }
    async function openSettingsWindow(scope) {
      const item = await settingsItem(scope);
      assert.notEqual(await evaluate(`${item}.getAttribute('aria-disabled')`), 'true');
      await evaluate(`${item}.click()`);
      const deadline = Date.now() + 10000;
      while (Date.now() < deadline) {
        const child = electron.BrowserWindow.getAllWindows().find(
          (candidate) =>
            candidate !== window &&
            new URL(candidate.webContents.getURL() || 'about:blank').searchParams.get('scope') ===
              scope,
        );
        if (
          child?.isVisible() &&
          (await child.webContents.executeJavaScript(
            `!!document.querySelector('.settings-window__content')`,
          ))
        )
          return child;
        await delay(30);
      }
      throw new Error(`Settings window did not open: ${scope}`);
    }
    async function verifySettingsWindow(child, scope) {
      const expectedTitle = scope === 'application' ? 'Настройки приложения' : 'Настройки проекта';
      const state = await child.webContents.executeJavaScript(`({
        title: document.querySelector('.app-title-bar__title').textContent,
        content: !!document.querySelector('.settings-screen, .settings-screen__state'),
        noMainScreen: !document.querySelector('.projects-screen, .main-screen'),
        nodeAvailable: typeof require !== 'undefined' || typeof process !== 'undefined'
      })`);
      assert.deepEqual(state, {
        title: expectedTitle,
        content: true,
        noMainScreen: true,
        nodeAvailable: false,
      });
      assert.equal(child.getParentWindow(), window);
      assert.equal(child.isModal(), false);
      assert.equal(child.webContents.getLastWebPreferences().sandbox, true);
      assert.equal(child.webContents.getLastWebPreferences().contextIsolation, true);
    }
    async function untilIn(child, expression) {
      const deadline = Date.now() + 10000;
      while (!(await child.webContents.executeJavaScript(expression))) {
        if (Date.now() >= deadline)
          throw new Error(
            `Settings timed out: ${expression}\n${await child.webContents.executeJavaScript('document.body.innerText')}`,
          );
        await delay(30);
      }
    }
    async function selectSetting(child, id, label) {
      const selector = `[id="${id}"]`;
      await untilIn(
        child,
        `document.querySelector(${JSON.stringify(selector)}) && !document.querySelector(${JSON.stringify(selector)}).disabled`,
      );
      const point = await child.webContents.executeJavaScript(`(() => {
        const rect = document.querySelector(${JSON.stringify(selector)}).closest('.ant-select').getBoundingClientRect();
        return { x: Math.round(rect.x + rect.width / 2), y: Math.round(rect.y + rect.height / 2) };
      })()`);
      child.webContents.sendInputEvent({
        type: 'mouseDown',
        button: 'left',
        clickCount: 1,
        ...point,
      });
      child.webContents.sendInputEvent({
        type: 'mouseUp',
        button: 'left',
        clickCount: 1,
        ...point,
      });
      const option = `Array.from(document.querySelectorAll('.ant-select-dropdown:not(.ant-select-dropdown-hidden) .ant-select-item-option')).find(item => item.textContent === ${JSON.stringify(label)})`;
      await untilIn(child, `!!(${option})`);
      await child.webContents.executeJavaScript(`${option}.click()`);
    }
    async function storedSetting(id, expected) {
      const file = path.resolve(__dirname, '../application/settings.json');
      const deadline = Date.now() + 10000;
      while (Date.now() < deadline) {
        if (fs.existsSync(file) && JSON.parse(fs.readFileSync(file, 'utf8'))[id] === expected)
          return;
        await delay(30);
      }
      throw new Error(`Setting was not persisted: ${id} = ${expected}`);
    }
    async function mockCategory(child, title, section) {
      await child.webContents.executeJavaScript(
        `Array.from(document.querySelectorAll('.ant-tree-title')).find(item => item.textContent === ${JSON.stringify(title)}).click()`,
      );
      await untilIn(child, `!!document.querySelector('[data-mock-section="${section}"]')`);
    }
    async function fillIn(child, selector, value) {
      await child.webContents.executeJavaScript(`(() => {
        const input = document.querySelector(${JSON.stringify(selector)});
        Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(input, ${JSON.stringify(value)});
        input.dispatchEvent(new Event('input', { bubbles: true }));
      })()`);
    }
    async function verifyMock(child) {
      const settingsFile = path.resolve(__dirname, '../application/settings.json');
      const readStored = () =>
        fs.existsSync(settingsFile) ? fs.readFileSync(settingsFile, 'utf8') : null;
      const before = readStored();
      await mockCategory(child, 'Базовые типы', 'basic');
      await fillIn(child, '#settings-mock_text', 'Local preview only');
      await child.webContents.executeJavaScript(
        `document.querySelector('#settings-mock_enabled').click()`,
      );
      await untilIn(
        child,
        `document.querySelector('#settings-mock_enabled').getAttribute('aria-checked') === 'false'`,
      );
      await mockCategory(child, 'Модификаторы', 'modifiers');
      await fillIn(child, '[aria-label="Точное значение слайдера"]', '37');
      await untilIn(
        child,
        `document.querySelector('[role="slider"][aria-label="Число · слайдер"]').getAttribute('aria-valuenow') === '37'`,
      );
      assert.equal(
        await child.webContents.executeJavaScript(
          `document.querySelectorAll('[role="slider"]').length`,
        ),
        4,
      );
      await mockCategory(child, 'Коллекции', 'collections');
      const dictionary = '.settings-mock__collection[aria-label="Словарь строк"]';
      await child.webContents.executeJavaScript(
        `document.querySelector(${JSON.stringify(dictionary + ' .ant-btn-dashed')}).click()`,
      );
      await untilIn(
        child,
        `document.querySelectorAll('[data-mock-row="textDictionary"]').length === 3`,
      );
      await fillIn(child, dictionary + ' [aria-label="Ключ строки 3"]', 'preview');
      await fillIn(child, dictionary + ' [aria-label="Значение строки 3"]', 'local');
      await child.webContents.executeJavaScript(
        `document.querySelector(${JSON.stringify(dictionary + ' [aria-label="Удалить строку 3"]')}).click()`,
      );
      await untilIn(
        child,
        `document.querySelectorAll('[data-mock-row="textDictionary"]').length === 2`,
      );
      const numbers = '.settings-mock__collection[aria-label="Список чисел"]';
      for (let remaining = 3; remaining > 0; remaining--) {
        await child.webContents.executeJavaScript(
          `document.querySelector(${JSON.stringify(numbers + ' [aria-label="Удалить строку 1"]')}).click()`,
        );
        await untilIn(
          child,
          `document.querySelectorAll('[data-mock-row="numbers"]').length === ${remaining - 1}`,
        );
      }
      await untilIn(child, `!!document.querySelector(${JSON.stringify(numbers + ' .ant-empty')})`);
      await child.webContents.executeJavaScript(
        `Array.from(document.querySelectorAll('.settings-mock button')).find(item => item.textContent.includes('Сбросить макет')).click()`,
      );
      await untilIn(child, `document.querySelectorAll('[data-mock-row="numbers"]').length === 3`);
      await mockCategory(child, 'Базовые типы', 'basic');
      assert.equal(
        await child.webContents.executeJavaScript(
          `document.querySelector('#settings-mock_text').value`,
        ),
        'Gravia Lab',
      );
      await fillIn(child, '#settings-mock_text', 'Discard this');
      await child.webContents.executeJavaScript(
        `Array.from(document.querySelectorAll('.ant-tree-title')).find(item => item.textContent === 'Внешний вид').click()`,
      );
      await untilIn(child, `!document.querySelector('.settings-mock')`);
      await mockCategory(child, 'Базовые типы', 'basic');
      assert.equal(
        await child.webContents.executeJavaScript(
          `document.querySelector('#settings-mock_text').value`,
        ),
        'Gravia Lab',
      );
      assert.equal(readStored(), before, 'The control mock must not persist any settings.');
      await child.webContents.executeJavaScript(
        `Array.from(document.querySelectorAll('.ant-tree-title')).find(item => item.textContent === 'Внешний вид').click()`,
      );
      await untilIn(child, `!document.querySelector('.settings-mock')`);
    }
    async function fill(selector, value) {
      await evaluate(`(() => {
      const input = document.querySelector(${JSON.stringify(selector)});
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(input, ${JSON.stringify(value)});
      input.dispatchEvent(new Event('input', { bubbles: true }));
    })()`);
    }
    async function activateDockTab(title) {
      const point = await evaluate(`(() => {
        const tab = Array.from(document.querySelectorAll('.dv-tab')).find(tab => tab.textContent === ${JSON.stringify(title)});
        const rect = tab.getBoundingClientRect();
        return { x: Math.round(rect.x + rect.width / 2), y: Math.round(rect.y + rect.height / 2) };
      })()`);
      // Dockview activates tabs on pointer-down, not a synthetic DOM click.
      window.webContents.sendInputEvent({
        type: 'mouseDown',
        button: 'left',
        clickCount: 1,
        ...point,
      });
      window.webContents.sendInputEvent({
        type: 'mouseUp',
        button: 'left',
        clickCount: 1,
        ...point,
      });
    }
    async function main(name) {
      await until(
        `!document.querySelector('.projects-screen') && document.querySelector('.app-title-bar__project-name')?.textContent === ${JSON.stringify(name)}`,
      );
      await until(`document.querySelectorAll('.main-screen .dv-tab').length === 4`);
      assert.equal(
        await evaluate(`document.querySelector('.main-screen__status').textContent`),
        '',
      );
      assert.equal(
        await evaluate(`document.querySelector('.main-screen__status').childElementCount`),
        0,
      );
      assert.equal(
        await evaluate(
          `!!document.querySelector('.main-screen .dockview-theme-' + document.querySelector('.app-theme').dataset.colorScheme)`,
        ),
        true,
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

    const projectSettingsItem = await settingsItem('project');
    assert.equal(await evaluate(`${projectSettingsItem}.getAttribute('aria-disabled')`), 'true');
    await evaluate(`${projectSettingsItem}.click()`);
    assert.equal(electron.BrowserWindow.getAllWindows().length, 1);
    // Close the menu without opening an item.
    await click('[aria-label="Настройки"]');
    const applicationSettings = await openSettingsWindow('application');
    await verifySettingsWindow(applicationSettings, 'application');
    await untilIn(applicationSettings, `!!document.querySelector('.ant-tree-title')`);
    await verifyMock(applicationSettings);
    await untilIn(
      applicationSettings,
      `document.querySelectorAll('.settings-screen .ant-select').length === 2`,
    );
    assert.equal(
      await applicationSettings.webContents.executeJavaScript(
        `document.querySelector('.ant-tree-title').textContent`,
      ),
      'Внешний вид',
    );
    await selectSetting(applicationSettings, 'gravia.theme', 'Тёмная');
    await untilIn(
      applicationSettings,
      `document.querySelector('.app-theme').dataset.colorScheme === 'dark'`,
    );
    await until(`document.querySelector('.app-theme').dataset.colorScheme === 'dark'`);
    await selectSetting(applicationSettings, 'gravia.theme', 'Светлая');
    await untilIn(
      applicationSettings,
      `document.querySelector('.app-theme').dataset.colorScheme === 'light'`,
    );
    await until(`document.querySelector('.app-theme').dataset.colorScheme === 'light'`);
    await storedSetting('gravia.theme', 'light');
    await new Promise((resolve) => {
      applicationSettings.webContents.once('did-finish-load', resolve);
      applicationSettings.reload();
    });
    await untilIn(
      applicationSettings,
      `document.querySelector('.app-theme')?.dataset.colorScheme === 'light' && document.querySelectorAll('.settings-screen .ant-select').length === 2`,
    );
    await selectSetting(applicationSettings, 'gravia.language', 'English');
    await untilIn(
      applicationSettings,
      `document.querySelector('.app-title-bar__title').textContent === 'Application settings'`,
    );
    await until(`document.body.innerText.includes('Recent projects')`);
    await storedSetting('gravia.language', 'en');
    assert.equal(
      await applicationSettings.webContents.executeJavaScript(
        `document.querySelector('.ant-tree-title').textContent`,
      ),
      'Appearance',
    );
    await applicationSettings.webContents.executeJavaScript(
      `document.querySelector('[aria-label="Reset Language"]').click()`,
    );
    await until(`document.body.innerText.includes('Недавние проекты')`);
    await untilIn(
      applicationSettings,
      `!!document.querySelector('[aria-label="Сбросить «Тема»"]')`,
    );
    await applicationSettings.webContents.executeJavaScript(
      `document.querySelector('[aria-label="Сбросить «Тема»"]').click()`,
    );
    await untilIn(
      applicationSettings,
      `document.querySelector('[id="gravia.theme"]').closest('.ant-select').textContent.includes('Системная')`,
    );
    assert.equal(electron.nativeTheme.themeSource, 'system');
    await storedSetting('gravia.theme', 'system');
    await storedSetting('gravia.language', 'ru');
    applicationSettings.close();
    assert.equal(window.isDestroyed(), false);
    window.focus();

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

    const appearanceSettings = await openSettingsWindow('application');
    await selectSetting(appearanceSettings, 'gravia.language', 'English');
    await until(
      `Array.from(document.querySelectorAll('.dv-tab')).some(tab => tab.textContent === 'Inspector')`,
    );
    await appearanceSettings.webContents.executeJavaScript(
      `document.querySelector('[aria-label="Reset Language"]').click()`,
    );
    await until(
      `Array.from(document.querySelectorAll('.dv-tab')).some(tab => tab.textContent === 'Инспектор')`,
    );
    appearanceSettings.close();
    window.focus();

    const projectSettings = await openSettingsWindow('project');
    await verifySettingsWindow(projectSettings, 'project');
    const reused = await openSettingsWindow('project');
    assert.equal(reused.id, projectSettings.id);
    assert.equal(electron.BrowserWindow.getAllWindows().length, 2);
    projectSettings.close();
    window.focus();

    const appearance = await evaluate(`(() => {
      const scope = document.querySelector('.app-theme');
      const dock = document.querySelector('.dockview-theme-' + scope.dataset.colorScheme);
      const title = document.querySelector('.app-title-bar');
      return {
        mode: scope.dataset.colorScheme,
        container: getComputedStyle(scope).getPropertyValue('--gravia-bg-container').trim(),
        dockBackground: getComputedStyle(dock).getPropertyValue('--dv-group-view-background-color').trim(),
        titleBackground: getComputedStyle(title).backgroundColor,
        graphBackground: getComputedStyle(document.querySelector('.react-flow')).backgroundColor,
        graphMode: document.querySelector('.react-flow').classList.contains(scope.dataset.colorScheme),
        scheme: getComputedStyle(document.documentElement).colorScheme
      };
    })()`);
    const expectedContainer = appearance.mode === 'dark' ? '#141414' : '#ffffff';
    const expectedBackground =
      appearance.mode === 'dark' ? 'rgb(20, 20, 20)' : 'rgb(255, 255, 255)';
    assert.equal(appearance.container, expectedContainer);
    assert.equal(appearance.dockBackground, expectedContainer);
    assert.equal(appearance.titleBackground, expectedBackground);
    assert.equal(appearance.graphBackground, expectedBackground);
    assert.equal(appearance.graphMode, true);
    assert.equal(appearance.scheme, appearance.mode);
    assert.equal(electron.nativeTheme.themeSource, 'system');
    assert.equal(window.getBackgroundColor().toLowerCase(), expectedContainer);

    // The workspace remains a presentation shell: three groups, four tabs, no graph data.
    const layout = await evaluate(`(() => {
      const bounds = (selector) => {
        const { x, y, width, height } = document.querySelector(selector).getBoundingClientRect();
        return { x, y, width, height };
      };
      return {
        files: bounds('.workspace-panel[aria-label="Файлы"]'),
        graph: bounds('.workspace-graph'),
        inspector: bounds('.workspace-panel[aria-label="Инспектор"]'),
        status: bounds('.main-screen__status'),
        height: innerHeight,
        nodes: document.querySelectorAll('.react-flow__node').length
      };
    })()`);
    assert.ok(layout.files.x < layout.graph.x && layout.graph.x < layout.inspector.x);
    assert.ok(
      layout.graph.width > layout.files.width && layout.graph.width > layout.inspector.width,
    );
    // Windows display scaling can place the edge on a fractional CSS pixel.
    assert.ok(Math.abs(layout.status.y + layout.status.height - layout.height) < 1);
    assert.equal(layout.nodes, 0);
    await activateDockTab('Логи');
    await until(
      `document.querySelector('.workspace-panel[aria-label="Логи"]')?.getBoundingClientRect().width > 0`,
    );
    await activateDockTab('Инспектор');
    await until(
      `document.querySelector('.workspace-panel[aria-label="Инспектор"]')?.getBoundingClientRect().width > 0`,
    );
    if (process.env.GRAVIA_WORKSPACE_SCREENSHOT) {
      fs.writeFileSync(
        process.env.GRAVIA_WORKSPACE_SCREENSHOT,
        (await window.webContents.capturePage()).toPNG(),
      );
    }
    window.setSize(640, 480);
    await until(
      `innerWidth < 700 && document.querySelector('.workspace-graph').getBoundingClientRect().width > 0`,
    );
    // Wait for Dockview's ResizeObserver and allow subpixel rounding at Windows DPI scales.
    await until('document.documentElement.scrollWidth <= innerWidth + 1');
    if (process.env.GRAVIA_WORKSPACE_SCREENSHOT) {
      fs.writeFileSync(
        process.env.GRAVIA_WORKSPACE_SCREENSHOT.replace(/\.png$/, '-narrow.png'),
        (await window.webContents.capturePage()).toPNG(),
      );
    }
    window.setSize(1200, 800);
    await until('innerWidth > 1100');

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
    // Dockview panel lifecycle: duplicates, native maximize/restore, close all and reopen.
    const addPanel = '[aria-label="Открыть панель"]';
    await click(addPanel);
    await until(`!!document.querySelector('.ant-dropdown-menu-item')`);
    await evaluate(
      `Array.from(document.querySelectorAll('.ant-dropdown-menu-item')).find(item => item.textContent.includes('Логи')).click()`,
    );
    await until(`document.querySelectorAll('.dv-tab').length === 5`);
    assert.equal(
      await evaluate(
        `Array.from(document.querySelectorAll('.dv-tab')).filter(tab => tab.textContent.includes('Логи')).length`,
      ),
      2,
    );
    assert.equal(
      await evaluate(`document.querySelectorAll('.workspace-tab-title > .anticon').length`),
      5,
    );
    await click('[aria-label="Развернуть на рабочую область"]');
    await until(`!!document.querySelector('[aria-label="Восстановить раскладку"]')`);
    assert.equal(
      await evaluate(`(() => {
      const group = document.querySelector('[aria-label="Восстановить раскладку"]').closest('.dv-groupview');
      return Math.abs(group.getBoundingClientRect().width - document.querySelector('.main-screen__dock').getBoundingClientRect().width) < 4;
    })()`),
      true,
    );
    await click('[aria-label="Восстановить раскладку"]');
    await until(`!document.querySelector('[aria-label="Восстановить раскладку"]')`);
    await evaluate(
      `Array.from(document.querySelectorAll('.dv-default-tab-action')).forEach(button => button.click())`,
    );
    await until(
      `document.querySelectorAll('.dv-tab').length === 0 && document.body.innerText.includes('Все панели закрыты')`,
    );
    await click(addPanel);
    await until(`!!document.querySelector('.ant-dropdown-menu-item')`);
    await evaluate(
      `Array.from(document.querySelectorAll('.ant-dropdown-menu-item')).find(item => item.textContent.includes('Логи')).click()`,
    );
    await until(`document.querySelectorAll('.dv-tab').length === 1`);
    const applicationAtExit = await openSettingsWindow('application');
    const projectAtExit = await openSettingsWindow('project');
    await verifySettingsWindow(applicationAtExit, 'application');
    await verifySettingsWindow(projectAtExit, 'project');
    assert.equal(electron.BrowserWindow.getAllWindows().length, 3);
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
