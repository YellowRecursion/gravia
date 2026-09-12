# Проверка совместимости кандидатов UI-стека

Изолированный компиляционный эксперимент от 11 сентября 2026 года. Это исследовательский материал, не начало реализации приложения и не утверждение перечисленных версий.

Контекст, выводы и ссылки: [дневник проверки](../../logbook/2026-09-11%20Проверка%20стека%20и%20границ%20реализации.md).

## Воспроизведение

Среда исходного запуска: Windows, Node.js 24.12.0, npm 11.6.2. Выполнять из этой папки, желательно в отдельной копии:

```powershell
npm.cmd ci --ignore-scripts --strict-peer-deps --no-audit --no-fund
node node_modules/typescript/bin/tsc --project tsconfig.json
node node_modules/typescript/bin/tsc --project tsconfig.json --skipLibCheck
node check.mjs
node node_modules/vite/bin/vite.js build
```

При исходной установке использовался `npm install` с теми же флагами, после чего сохранён lockfile. Повторное `npm ci` здесь не запускалось. `--ignore-scripts` использован намеренно; это не проверка обычной установки всех возможных зависимостей будущего приложения.

## Полученные результаты

| Проверка | Результат |
| --- | --- |
| Установка с `--strict-peer-deps` | Пройдена, 117 пакетов |
| TypeScript `strict: true`, `skipLibCheck: false` | Не пройдена: две TS2430 в зависимостях Ant |
| TypeScript с `--skipLibCheck` | Пройдена |
| API тем и RU/EN, `check.mjs` | Пройдена |
| Vite production build | Пройден, предупреждение о большом chunk |

Диагностики строгой проверки библиотечных деклараций:

```text
node_modules/@rc-component/image/es/PreviewGroup.d.ts(6,18): error TS2430:
Interface 'GroupPreviewConfig' incorrectly extends interface 'InternalPreviewConfig'.
Types of property 'imageRender' are incompatible.
The callback requires current: number where the base allows current?: number.

node_modules/@rc-component/picker/es/PickerPanel/index.d.ts(40,18): error TS2430:
Interface 'SinglePickerPanelProps<DateType>' incorrectly extends interface 'BasePickerPanelProps<DateType>'.
Types of property 'defaultValue' are incompatible.
Type 'DateType | null | undefined' is not assignable to type 'DateType | undefined'.
```

Выше сокращённая запись диагностик: первая длинная вложенная цепочка типов сведена к различию обязательности `current`. Исходные декларации зафиксированы версиями в lockfile. `skipLibCheck` не устраняет эти ошибки.

Результаты API-проверки:

```json
{
  "status": "passed",
  "ant": {
    "light": "#ffffff",
    "dark": "#141414",
    "defaultHeight": 32,
    "compactHeight": 28,
    "ruCancel": "Отмена",
    "enCancel": "Cancel"
  },
  "dockview": {
    "light": "dockview-theme-light",
    "dark": "dockview-theme-dark"
  },
  "i18n": "ru/en switch passed",
  "scope": "API values only; no browser rendering or Electron launch"
}
```

Сборка: 1 675 модулей, 15,26 с; JS 1 533,09 kB (gzip 455,20 kB), CSS 140,76 kB (gzip 12,57 kB), предупреждение о chunk >500 kB. Это не время запуска, объём RAM или размер упакованного приложения.

`registry-evidence.json` содержит полученные метаданные конкретных версий: peer dependencies, engines и заявленные лицензии. Наличие метаданных Electron/Forge не означает их установку или проверку в эксперименте.

## Ограничения

Пример содержит 80 простых нод без связей. Темы библиотек подключены для проверки API, но React Flow намеренно имеет фиксированную тему; переводы неполные, переключателя языка в UI нет. Поэтому код нельзя использовать как свидетельство готового совместного оформления, производительности или завершённой локализации. `check.mjs` проверяет переключение собственных строк отдельно от браузера.

Браузерный UI, Electron, C#, Python, MATLAB, Linux/macOS и IPC в этом эксперименте не запускались. `node_modules` и `dist` в исследовательский материал не включены. Проверка уязвимостей и полный аудит транзитивных лицензий не выполнялись.

## Повторная проверка при подключении стека, 12 сентября

По запросу владельца стек теперь подключён в Gravia.Desktop. Исходный эксперимент
выше не переписан: его версии, ограничения и результаты сохраняются. В приложении
TypeScript 6.0.3 выбран для совместимости с typescript-eslint; ошибки деклараций
Ant повторились, используется явно документированный skipLibCheck. Новые команды,
результаты сборки и проверки настоящего Electron — в разделе от 12 сентября
[дневника проверки](../../logbook/2026-09-11%20Проверка%20стека%20и%20границ%20реализации.md).
