using Gravia.Host.Desktop;
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
        return await HostProcess.RunAsync(token => DesktopHost.RunAsync(args, token),
            bootstrapLogging.CreateLogger(nameof(HostProcess)), Console.Error);
    }
}
