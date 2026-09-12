# Gravia Desktop

Минимальная Electron-оболочка. Нет React, TypeScript, Vite, UI-библиотек, экранов,
редактора графа или бизнес-функций. Архитектура фронтенда ещё не выбрана.

## Запуск

Нужны .NET SDK 10 и Node.js >= 22.12.0 с npm в PATH.
Запустить **Gravia.Host** в Rider или из корня репозитория:

```powershell
dotnet run --project src/Gravia.Host
```

Сборка Host автоматически выполняет `npm ci`, когда зависимости отсутствуют или
package.json/package-lock.json изменились, и проверяет наличие Electron runtime.
Первый запуск требует интернета и загрузки Electron. Последующие не требуют
отдельного запуска npm или dev-сервера. Ручная подготовка:

```powershell
cd src/Gravia.Desktop
npm ci
npm run setup
```

Host выбирает свободный порт на 127.0.0.1, начинает слушать, запускает Electron и
передаёт адрес через отдельный локальный named pipe. Открывается пустое окно Gravia. Закрытие последнего
окна завершает Host. При остановке Host закрывается его служебный канал; Electron
закрывается и при принудительном завершении родителя кнопкой Stop в Rider.

Это npm-проект, не C#-библиотека. Файлы добавлены в папку Gravia.Desktop в solution.
Сборка копирует оболочку в `Gravia.Host/bin/<configuration>/net10.0/Desktop`.
Рабочая директория Rider не влияет на поиск этих файлов.

## Настройки для разработки и тестирования

- `--Desktop:Enabled=false` — Host без Electron.
- `--Desktop:AppDirectory=<полный путь>` — другой каталог оболочки.
- `--Desktop:ExecutablePath=<полный путь>` — явно заданный Electron executable.
- `-p:SkipDesktopRestore=true` при dotnet build/test — не готовить npm/runtime.
  Полезно для backend CI; для запуска окна Electron должен уже быть установлен.

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
зарегистрированных через app.getAppMetrics. Тестовая оболочка собирается из тех же
исходников во временном каталоге и удаляется после проверки.

## Границы

- main.cjs / shell.cjs — только жизненный цикл окна; bootstrap.cjs — валидация
  служебного стартового сообщения. Это не будущий контракт фронтенда или плагинов.
- Renderer пустой, без Node.js/preload/IPC. Запрещены новые окна, навигация,
  webview и выдача разрешений; CSP не разрешает скрипты или внешние ресурсы.
- Host раздаёт только index.html и shell.css. Файлы проекта, конфигурация Host и
  исходники Electron не публикуются по HTTP. CORS не включён.
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
