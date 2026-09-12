using Gravia.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Gravia.Host;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Services.AddHostServices();
        builder.Services.Configure<InMemoryLogOptions>(
            builder.Configuration.GetSection(InMemoryLogOptions.SectionName));

        using var host = builder.Build();
        await host.RunAsync();
    }
}
