using Gravia.Application.Common.Behaviors;
using Gravia.Application.Logging;
using Gravia.Infrastructure.Logging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gravia.Host.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public async Task HostBuildsStartsAndStopsWithApplicationServices()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddHostServices();

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISender>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPublisher>());
        var behavior = Assert.Single(scope.ServiceProvider
            .GetServices<IPipelineBehavior<TestCommand, int>>());
        Assert.IsType<ValidationBehavior<TestCommand, int>>(behavior);

        var journal = scope.ServiceProvider.GetRequiredService<ILogJournal>();
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Gravia.Host.Tests").LogWarning("Journal is connected");
        Assert.Contains(journal.Read().Entries, entry => entry.Message == "Journal is connected");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(timeout.Token);
        await host.StopAsync(timeout.Token);
    }

    public sealed record TestCommand : IRequest<int>;

    [Fact]
    public void LogCapacityCanBeConfiguredThroughDependencyInjection()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddHostServices();
        builder.Services.Configure<InMemoryLogOptions>(options =>
        {
            options.CapacityBytes = 2048;
            options.MaxEntryBytes = 512;
        });
        using var host = builder.Build();
        Assert.Equal(2048, host.Services.GetRequiredService<ILogJournal>().GetStatistics().CapacityBytes);
    }

    [Fact]
    public void InvalidLogOptionsFailAtStartup()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddHostServices();
        builder.Services.Configure<InMemoryLogOptions>(options => options.CapacityBytes = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());
    }
}
