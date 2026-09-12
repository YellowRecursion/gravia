# Gravia Desktop

Electron-оболочка и веб-каркас Gravia Lab. Стек подключён по запросу владельца;
первый экран, редактор графа и бизнес-функции не реализованы. Принят FSD; слои,
границы, состояние и открытые вопросы описаны в
[архитектуре фронтенда](../../docs/frontend-architecture.md).
Правила для LLM — [AGENTS.md](AGENTS.md). Рабочий документ/undo и UX ещё не выбраны.

## Подключённый стек

- React + TypeScript (strict) + Vite — renderer и сборка.
- Ant Design + @ant-design/icons — элементы интерфейса и иконки.
- Dockview + React Flow — будущие панели и редактор схемы; компоненты пока не создаются.
- i18next + react-i18next — RU/EN; стартовый язык RU.
- TanStack Query — будущие данные API; установлен корневой QueryClientProvider.
- Zustand — доступен для общего состояния UI; хранилища пока не создаются.
- ESLint с типизированными правилами TypeScript, React, FSD Boundaries + Prettier.
- Orval — подготовлена генерация клиента и Query-хуков из OpenAPI через fetch.

Zustand не заменяет серверный кеш Query; бизнес-правила остаются в C#.
Vitest, Testing Library и Playwright не добавлены: владелец проверяет интерфейс
вручную. Существующие технические проверки запуска и завершения оболочки сохранены.
Прямые версии закреплены в package.json, транзитивные — в package-lock.json;
peer dependencies проверяются строго через .npmrc.

## Запуск

Нужны .NET SDK 10 и Node.js >= 22.18.0 с npm в PATH (минимум Orval).
Запустить **Gravia.Host** в Rider или из корня репозитория:

```powershell
dotnet run --project src/Gravia.Host
```

Сборка Host автоматически выполняет `npm ci`, когда зависимости отсутствуют или
package.json/package-lock.json изменились, проверяет наличие Electron runtime и
выполняет lint (включая FSD), проверку типов и production-сборку Vite.
Первый запуск требует интернета и загрузки Electron. Последующие не требуют
отдельного запуска npm или dev-сервера. Ручная подготовка:

```powershell
cd src/Gravia.Desktop
npm ci
npm run setup
npm run build
```

Host выбирает свободный порт на 127.0.0.1, начинает слушать, запускает Electron и
передаёт адрес через отдельный локальный named pipe. Открывается пустое окно Gravia Lab. Закрытие последнего
окна завершает Host. При остановке Host закрывается его служебный канал; Electron
закрывается и при принудительном завершении родителя кнопкой Stop в Rider.

Это npm-проект, не C#-библиотека. Файлы добавлены в папку Gravia.Desktop в solution.
Сборка копирует оболочку в `Gravia.Host/bin/<configuration>/net10.0/Desktop`,
а скомпилированный веб (`dist`) — в его подкаталог `wwwroot`.
Рабочая директория Rider не влияет на поиск этих файлов.

## Настройки для разработки и тестирования

- `--Desktop:Enabled=false` — Host без Electron.
- `--Desktop:AppDirectory=<полный путь>` — другой каталог оболочки.
- `--Desktop:ExecutablePath=<полный путь>` — явно заданный Electron executable.
- `-p:SkipDesktopRestore=true` при dotnet build/test — не запускать npm, сборку веба
  и подготовку runtime. Для HTTP-тестов и запуска нужен заранее собранный `dist`;
  для окна нужен установленный Electron.

`npm run dev` запускает отдельный Vite на loopback с обновлением веба при изменениях.
Это просмотр renderer в браузере, не автоматический dev-сервер для Electron;
встроенный запуск из Host использует собранный веб. Проксирование API не настроено.

## Команды разработки

```powershell
npm run check        # lint, форматирование, типы, сборка, существующие Node-тесты
npm run format       # применить форматирование
npm run lint
npm run typecheck
```

TypeScript 6.0.3 выбран потому, что typescript-eslint 8.70.0 поддерживает TS <6.1;
TS 7 нельзя подключить вместе с ним без нарушения peer-контракта.

`strict` и типизированный lint включены для кода Gravia. `skipLibCheck: true`
пропускает внутреннюю проверку библиотечных `.d.ts`: проверка 12 сентября повторила
две TS2430 в @rc-component/image и picker из Ant, а также ошибки деклараций
транзитивных зависимостей Orval. Это обход дефектов внешних деклараций, не их
исправление и не отключение проверки нашего использования API библиотек.

## Генерация API

Предметных endpoints/OpenAPI в Host пока нет. Не добавлены фиктивные DTO, запросы
или API ради генерации. После появления настоящего контракта:

```powershell
$env:GRAVIA_OPENAPI = 'C:/path/to/host-openapi.json' # либо URL документа Host
npm run api:generate
npm run typecheck
```

Orval пишет в `renderer/shared/api/generated`; эти файлы нельзя редактировать вручную.
Снаружи доступ к ним — через явные экспорты `shared/api/index.ts`, не глубокими импортами.
Клиент использует относительные адреса текущего Host и стандартный fetch, без Axios.
Вызовы генератора отделены от обычной сборки: ей не нужен работающий Host.
Политику HTTP-ошибок/отмены и согласование ProblemDetails нужно проверить при
реализации первого API; генерация сама по себе не решает эти задачи.

По умолчанию runtime находится в node_modules этого проекта. Есть вариант поиска
node_modules рядом со скопированной оболочкой. Установщик, ASAR, автообновления и
самодостаточная publish-поставка пока не настроены: обычный dotnet publish не
включает Electron runtime. Windows проверяется первым; macOS/Linux требуют
отдельной проверки, несмотря на предусмотренные пути executable.

## Проверки

```powershell
npm test
```

Сквозная проверка **открывает и закрывает настоящие окна**:

```powershell
dotnet build ../../Gravia.slnx --configuration Release
npm run smoke
```

Она запускает реальный Host из чужого рабочего каталога и проверяет пустое окно,
отсутствие Node.js в renderer, sandbox/contextIsolation, закрытие окна и
принудительное завершение только Host. Проверяет исчезновение процессов Electron,
зарегистрированных через app.getAppMetrics. Тестовая оболочка берёт ресурсы из
результата сборки Host, проверяет монтирование React и отсутствие ошибок renderer;
временный каталог удаляется после проверки.

## Границы

- main.cjs / shell.cjs — только жизненный цикл окна; bootstrap.cjs — валидация
  служебного стартового сообщения. Это не будущий контракт фронтенда или плагинов.
- Renderer содержит только пустой React main, без Node.js/preload/IPC. Запрещены
  новые окна, навигация, webview и выдача разрешений. CSP разрешает скрипты только
  своего origin, без unsafe-eval/unsafe-inline для JavaScript. Inline-стили разрешены
  для CSS-in-JS Ant и позиционирования React Flow; это не разрешение inline-скриптов.
- Host раздаёт только compiled wwwroot. Файлы проекта, конфигурация Host и
  исходники Electron не публикуются по HTTP. CORS не включён. Исходные карты не собираются.
- Для будущих изменяющих API необходимо отдельно согласовать аутентификацию
  локальной сессии/защиту от сторонних страниц. Loopback сам по себе не является
  авторизацией. Такие API в этом срезе отсутствуют.

Основания: [Electron first app](https://www.electronjs.org/docs/latest/tutorial/tutorial-first-app),
[Electron security](https://www.electronjs.org/docs/latest/tutorial/security),
[Kestrel endpoints](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0),
[Node local IPC](https://nodejs.org/api/net.html#ipc-support).

В служебном канале используется случайное имя, одно подключение и CurrentUserOnly
со стороны .NET. В Windows это named pipe; для Unix предусмотрен абсолютный путь
локального сокета .NET. Не используется stdin: реальная проверка на Windows выявила
ранний EOF у Electron даже при живом Host.
