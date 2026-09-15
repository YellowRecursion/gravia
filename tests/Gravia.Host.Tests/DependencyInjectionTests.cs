using ActualLab.Fusion;
using ActualLab.Rpc;
using ActualLab.Rpc.Middlewares;
using Gravia.Application;
using Gravia.Application.Common.Behaviors;
using Gravia.Application.Common.Events;
using Gravia.Application.Logging;
using Gravia.Application.Storage;
using Gravia.Infrastructure.Logging;
using Gravia.Infrastructure.Storage;
using Gravia.Host.Fusion;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gravia.Host.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AllApplicationAndHostNotificationHandlersAreRegisteredExactlyOnce()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddHostServices();
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        Assert.IsType<DomainEventNotificationPublisher>(scope.ServiceProvider.GetRequiredService<INotificationPublisher>());

        var assemblies = new[] { typeof(Gravia.Application.DependencyInjection).Assembly, typeof(DependencyInjection).Assembly };
        var handlers = assemblies.SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                .Select(contract => (Implementation: type.AsType(), Contract: contract)))
            .ToArray();
        Assert.NotEmpty(handlers);
        foreach (var handler in handlers)
        {
            Assert.Single(builder.Services, descriptor =>
                descriptor.ServiceType == handler.Contract && descriptor.ImplementationType == handler.Implementation);
            Assert.Single(scope.ServiceProvider.GetServices(handler.Contract),
                instance => instance?.GetType() == handler.Implementation);
        }
    }

    [Fact]
    public void RepeatedAssemblyArgumentsDoNotDuplicateNotificationHandlers()
    {
        var services = new ServiceCollection();
        var application = typeof(Gravia.Application.DependencyInjection).Assembly;
        var host = typeof(DependencyInjection).Assembly;
        services.AddApplicationServices(application, host, host);
        var handlers = services.Where(descriptor => descriptor.ServiceType.IsGenericType
            && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>)).ToArray();
        Assert.NotEmpty(handlers);
        Assert.All(handlers.GroupBy(descriptor => (descriptor.ServiceType, descriptor.ImplementationType)),
            group => Assert.Single(group));
    }

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
        {
            Assert.NotNull(host.Services.GetRequiredService(serviceType));
            foreach (var method in serviceType.DeclaredMethods.Where(method => method.IsPublic))
            {
                Assert.True(method.ReturnType.IsGenericType, method.ToString());
                Assert.Contains(method.ReturnType.GetGenericTypeDefinition(),
                    new[] { typeof(Task<>), typeof(ValueTask<>) });
                Assert.True(typeof(RpcResult).IsAssignableFrom(method.ReturnType.GenericTypeArguments[0]),
                    method.ToString());
            }
        }

        Assert.Single(host.Services.GetServices<IRpcMiddleware>().OfType<RpcExceptionMiddleware>());
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
        Assert.Collection(scope.ServiceProvider.GetServices<IPipelineBehavior<TestCommand, int>>(),
            behavior => Assert.IsType<ProjectOperationBehavior<TestCommand, int>>(behavior),
            behavior => Assert.IsType<ValidationBehavior<TestCommand, int>>(behavior));
        Assert.Same(host.Services.GetRequiredService<DomainEventDispatcher>(),
            Assert.Single(host.Services.GetServices<IHostedService>().OfType<DomainEventDispatcher>()));

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
