using Gravia.Host.Desktop;
using Microsoft.Extensions.Logging;
using Gravia.Infrastructure;
using Gravia.Infrastructure.Logging;
using Microsoft.Extensions.Options;

namespace Gravia.Host;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // These instances outlive Host DI, including failed startup and disposal.
        var journal = new InMemoryLogJournal(Options.Create(new InMemoryLogOptions()), TimeProvider.System);
        await using var fileLog = new SessionFileLog(ApplicationPaths.StorageDirectory,
            reportDiagnostic: message => journal.TryWrite(message));
        using var bootstrapLogging = LoggerFactory.Create(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddProvider(new JournalLoggerProvider(journal, fileLog));
            logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        });
        var logger = bootstrapLogging.CreateLogger(nameof(HostProcess));
        UnhandledExceptionEventHandler onUnhandled = (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception error)
                logger.LogCritical(error, "Unhandled process exception. Terminating: {Terminating}", eventArgs.IsTerminating);
        };
        AppDomain.CurrentDomain.UnhandledException += onUnhandled;
        try
        {
            logger.LogInformation("Gravia Lab starting. ProcessId: {ProcessId}", Environment.ProcessId);
            var code = await HostProcess.RunAsync(token => DesktopHost.RunAsync(args, token, journal, fileLog),
                logger, Console.Error);
            logger.LogInformation("Gravia Lab stopped. ExitCode: {ExitCode}", code);
            return code;
        }
        finally { AppDomain.CurrentDomain.UnhandledException -= onUnhandled; }
    }
}
