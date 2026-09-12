using Gravia.Host.Desktop;
using Gravia.Host.Errors;
using Microsoft.Extensions.Logging;

namespace Gravia.Host;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Available before application DI is built and after it is disposed.
        // Fatal process diagnostics go to stderr; no file storage is introduced.
        using var bootstrapLogging = LoggerFactory.Create(logging =>
            logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace));
        var errors = new ExceptionHandler(bootstrapLogging.CreateLogger<ExceptionHandler>());
        return await HostProcess.RunAsync(token => DesktopHost.RunAsync(args, token), errors, Console.Error);
    }
}
