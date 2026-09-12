# Тесты Gravia

Для каждого проекта приложения создан отдельный проект xUnit на .NET 10.
Каждый тестовый проект ссылается на соответствующий проект из `src`.

Проверка из корня репозитория:

```powershell
dotnet build Gravia.slnx
dotnet test Gravia.slnx
```

Application.Tests проверяет регистрацию MediatR и pipeline FluentValidation:
валидные и невалидные запросы, сбор ошибок, async-валидацию, отмену,
запросы без валидатора и передачу исключения обработчика.
Host.Tests проверяет сборку, запуск и остановку Host с регистрациями Application и журнала,
переопределение размера буфера и отказ запуска при неправильной настройке.

Infrastructure.Tests проверяет журнал в памяти: размер в байтах, кольцевой переход,
Unicode и усечение, снимки, повторные сообщения, счётчики, курсоры и фильтры,
Clear, 100 000 параллельных записей, ILogger/async scopes, замену через DI.
Отдельный LoggingLoadTests подаёт 2 млн сообщений от 20 источников в буфер 16 MiB.
Изолированный запуск с выводом измерений:

```powershell
dotnet test tests/Gravia.Infrastructure.Tests --configuration Release --filter "FullyQualifiedName~LoggingLoadTests" --logger "console;verbosity=detailed"
```

Domain.Tests пока не содержит сценариев: предметная модель ещё не реализована.
