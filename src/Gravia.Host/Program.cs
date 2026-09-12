using Gravia.Infrastructure.Logging;
using Gravia.Host.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        return await HostProcess.RunAsync(token => RunHostAsync(args, token), errors, Console.Error);
    }

    private static async Task RunHostAsync(string[] args, CancellationToken cancellationToken)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Services.AddHostServices();
        builder.Services.Configure<InMemoryLogOptions>(
            builder.Configuration.GetSection(InMemoryLogOptions.SectionName));

        var host = builder.Build();
        // RunAsync owns disposal, including failed startup and cancellation.
        await host.RunAsync(cancellationToken);
    }
}
