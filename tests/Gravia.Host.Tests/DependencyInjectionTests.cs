using ActualLab.Fusion;
using Gravia.Application.Common.Behaviors;
using Gravia.Application.Logging;
using Gravia.Application.Storage;
using Gravia.Infrastructure.Logging;
using Gravia.Infrastructure.Storage;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gravia.Host.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AllHostFusionServicesAreRegistered()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddHostServices();
        using var host = builder.Build();

        var serviceTypes = typeof(DependencyInjection).Assembly.DefinedTypes
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
            .Where(type => type.GetInterfaces()
                .Except(type.BaseType?.GetInterfaces() ?? [])
                .Contains(typeof(IComputeService)))
            .ToArray();

        Assert.NotEmpty(serviceTypes);
        foreach (var serviceType in serviceTypes)
            Assert.NotNull(host.Services.GetRequiredService(serviceType));
    }

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
    public async Task ApplicationDataStoreIsSharedAcrossScopesAndDisposedWithHost()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddHostServices();
        builder.Services.AddTransient<ApplicationStorageConsumer>();

        IDataStore storage;
        KeyValueStore values;
        using (var host = builder.Build())
        {
            using var first = host.Services.CreateScope();
            using var second = host.Services.CreateScope();
            storage = host.Services.GetRequiredKeyedService<IDataStore>(DataStoreKey.Application);
            values = host.Services.GetRequiredKeyedService<KeyValueStore>(DataStoreKey.Application);
            Assert.Same(values, first.ServiceProvider.GetRequiredService<ApplicationStorageConsumer>().Values);
            Assert.Same(values, second.ServiceProvider.GetRequiredService<ApplicationStorageConsumer>().Values);
            Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<KeyValueStore>());
            Assert.IsType<FileDataStore>(storage);
            Assert.Same(storage, first.ServiceProvider.GetRequiredKeyedService<IDataStore>(DataStoreKey.Application));
            Assert.Same(storage, second.ServiceProvider.GetRequiredKeyedService<IDataStore>(DataStoreKey.Application));
            Assert.Same(storage, first.ServiceProvider.GetRequiredService<ApplicationStorageConsumer>().Storage);
            Assert.Same(storage, second.ServiceProvider.GetRequiredService<ApplicationStorageConsumer>().Storage);
            Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<IDataStore>());
        }

        // Resolving the store does not create files. A disposed store must fail before any I/O.
        await Assert.ThrowsAsync<ObjectDisposedException>(() => storage.ExistsAsync("unused.bin"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => values.GetAsync<int>("unused"));
    }

    public sealed class ApplicationStorageConsumer(
        [FromKeyedServices(DataStoreKey.Application)] IDataStore storage,
        [FromKeyedServices(DataStoreKey.Application)] KeyValueStore values)
    {
        public IDataStore Storage { get; } = storage;
        public KeyValueStore Values { get; } = values;
    }

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
