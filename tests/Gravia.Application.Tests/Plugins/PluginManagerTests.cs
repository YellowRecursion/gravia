using System.Text;
using System.Text.Json;
using Gravia.Application.Plugins.DTOs;
using Gravia.Application.Plugins.Interfaces;
using Gravia.Application.Plugins.Services;
using Gravia.Application.Tests.Projects;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Plugins.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Application.Tests.Plugins;

public sealed class PluginManagerTests
{
    private static readonly string ProjectPath = Path.Combine(Path.GetTempPath(), "gravia-plugin-manager-tests");

    [Fact]
    public async Task AddPersistsOnlyRelativePathAndEnabledAndDoesNotActivate()
    {
        var data = new MemoryDataStore();
        var factory = new Factory();
        await Manifest(data, "plugins/base", "org.gravia.base");
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        await manager.AddAsync("plugins\\base/");
        var plugin = Assert.Single(manager.Plugins);
        Assert.Equal("plugins/base", plugin.Path);
        Assert.False(plugin.Enabled);
        Assert.Equal("org.gravia.base", plugin.Manifest!.Id);
        Assert.Empty(factory.Events);
        using var json = JsonDocument.Parse((await data.ReadBytesAsync(PluginManager.FileName))!);
        var registration = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(2, registration.EnumerateObject().Count());
        Assert.Equal("plugins/base", registration.GetProperty("path").GetString());
        Assert.False(registration.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task InitializeStartsDependenciesFirstAndDisposeStopsDependentsFirst()
    {
        var data = await DependencyProject();
        var factory = new Factory();
        var manager = Create(data, factory);
        await manager.InitializeAsync();
        Assert.Equal(["start:org.gravia.base", "start:org.gravia.consumer"], factory.Events);
        Assert.All(manager.Plugins, item => Assert.True(item.IsActive));
        await manager.InitializeAsync();
        Assert.Equal(2, factory.Events.Count);
        await manager.DisposeAsync();
        await manager.DisposeAsync();
        Assert.Equal(["start:org.gravia.base", "start:org.gravia.consumer", "stop:org.gravia.consumer", "stop:org.gravia.base"], factory.Events);
        Assert.All(manager.Plugins, item => Assert.False(item.IsActive));
        Assert.Equal(0, data.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.AddAsync("plugins/other"));
        // Closing does not overwrite the desired enabled flags.
        Assert.All((await data.ReadAsync<PluginRegistration[]>(PluginManager.FileName))!, item => Assert.True(item.Enabled));
    }

    [Fact]
    public async Task EnablingDependencyRecoversPreviouslyBlockedConsumer()
    {
        var data = await DependencyProject(enabled: false);
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        await manager.SetEnabledAsync("plugins/consumer", true);
        Assert.NotNull(manager.Plugins.Single(item => item.Path == "plugins/consumer").Error);
        Assert.Empty(factory.Events);
        await manager.SetEnabledAsync("plugins/base", true);
        Assert.All(manager.Plugins, item => Assert.True(item.IsActive));
        Assert.Equal(["start:org.gravia.base", "start:org.gravia.consumer"], factory.Events);
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.SetEnabledAsync("plugins/base", false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RemoveAsync("plugins/base"));
        await manager.SetEnabledAsync("plugins/consumer", false);
        await manager.SetEnabledAsync("plugins/base", false);
        await manager.RemoveAsync("plugins/base");
        Assert.Single(manager.Plugins);
    }

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("1.3.0-beta.1")]
    public async Task IncompatibleDependencyPreventsConsumerActivation(string version)
    {
        var data = await DependencyProject();
        await Manifest(data, "plugins/base", "org.gravia.base", version);
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        Assert.False(manager.Plugins[0].IsActive);
        Assert.Contains("does not satisfy", manager.Plugins[0].Error);
        Assert.DoesNotContain("start:org.gravia.consumer", factory.Events);
    }

    [Fact]
    public async Task MissingAndCyclicDependenciesDoNotBlockIndependentPlugins()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/first", "org.gravia.first", dependency: "org.gravia.second");
        await Manifest(data, "plugins/second", "org.gravia.second", dependency: "org.gravia.first");
        await Manifest(data, "plugins/missing", "org.gravia.missing", dependency: "org.gravia.absent");
        await Manifest(data, "plugins/independent", "org.gravia.independent");
        await data.SaveAsync(PluginManager.FileName, new[]
        {
            new PluginRegistration("plugins/first", true), new("plugins/second", true),
            new("plugins/missing", true), new("plugins/independent", true)
        });
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        Assert.All(manager.Plugins.Take(3), item => { Assert.False(item.IsActive); Assert.NotNull(item.Error); });
        Assert.True(manager.Plugins[3].IsActive);
        Assert.Equal(["start:org.gravia.independent"], factory.Events);
    }

    [Fact]
    public async Task ReloadRestartsAffectedDependentsAndLeavesIndependentPluginRunning()
    {
        var data = await DependencyProject();
        await Manifest(data, "plugins/independent", "org.gravia.independent");
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        await manager.AddAsync("plugins/independent");
        await manager.SetEnabledAsync("plugins/independent", true);
        factory.Events.Clear();
        await Manifest(data, "plugins/base", "org.gravia.base", "1.4.0");
        await manager.ReloadAsync("plugins/base");
        Assert.Equal(["stop:org.gravia.consumer", "stop:org.gravia.base", "start:org.gravia.base", "start:org.gravia.consumer"], factory.Events);
        Assert.All(manager.Plugins, item => Assert.True(item.IsActive));
        Assert.Equal("1.4.0", manager.Plugins.Single(item => item.Path == "plugins/base").Manifest!.Version);
    }

    [Fact]
    public async Task MissingManifestRemainsVisibleAndCanBeReloadedOrRemoved()
    {
        var data = new MemoryDataStore();
        await data.SaveAsync(PluginManager.FileName, new[] { new PluginRegistration("plugins/base", true) });
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        Assert.Null(Assert.Single(manager.Plugins).Manifest);
        Assert.NotNull(manager.Plugins[0].Error);
        await Manifest(data, "plugins/base", "org.gravia.base");
        await manager.ReloadAsync("plugins/base");
        Assert.True(manager.Plugins[0].IsActive);
        await manager.RemoveAsync("plugins/base");
        Assert.Empty(manager.Plugins);
    }

    [Fact]
    public async Task ActivationFailureIsCleanedUpAndCanBeRetried()
    {
        var data = await DependencyProject();
        var factory = new Factory { FailActivation = "org.gravia.consumer" };
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        Assert.Contains("stop:org.gravia.consumer", factory.Events);
        Assert.False(manager.Plugins[0].IsActive);
        Assert.NotNull(manager.Plugins[0].Error);
        factory.FailActivation = null;
        await manager.SetEnabledAsync("plugins/consumer", true);
        Assert.True(manager.Plugins[0].IsActive);
        Assert.Null(manager.Plugins[0].Error);
    }

    [Fact]
    public async Task DisposalAttemptsAllPluginsEvenWhenOneDeactivationFails()
    {
        var data = await DependencyProject();
        var factory = new Factory();
        var manager = Create(data, factory);
        await manager.InitializeAsync();
        factory.FailDeactivation = "org.gravia.consumer";
        await Assert.ThrowsAsync<AggregateException>(() => manager.DisposeAsync().AsTask());
        Assert.Contains("stop:org.gravia.base", factory.Events);
        Assert.True(manager.Plugins[0].IsActive);
        Assert.NotNull(manager.Plugins[0].Error);
    }

    [Fact]
    public async Task SaveFailureDoesNotCommitAddedOrEnabledRegistration()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/base", "org.gravia.base");
        await using var manager = Create(data, new Factory());
        await manager.InitializeAsync();
        data.WriteFailure = new IOException("Disk unavailable.");
        await Assert.ThrowsAsync<IOException>(() => manager.AddAsync("plugins/base"));
        Assert.Empty(manager.Plugins);
        data.WriteFailure = null;
        await manager.AddAsync("plugins/base");
        data.WriteFailure = new IOException("Disk unavailable.");
        await Assert.ThrowsAsync<IOException>(() => manager.SetEnabledAsync("plugins/base", true));
        Assert.False(manager.Plugins[0].Enabled);
        Assert.False(manager.Plugins[0].IsActive);
    }

    [Fact]
    public async Task PartiallyActivatedPluginIsCleanedBeforeItsDependencyOnClose()
    {
        var data = await DependencyProject();
        var factory = new Factory
        {
            FailActivation = "org.gravia.consumer", FailDeactivation = "org.gravia.consumer"
        };
        var manager = Create(data, factory);
        await manager.InitializeAsync();
        factory.FailDeactivation = null;
        factory.Events.Clear();
        await manager.DisposeAsync();
        Assert.Equal(["stop:org.gravia.consumer", "stop:org.gravia.base"], factory.Events);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("plugins/../outside")]
    [InlineData("/outside")]
    [InlineData("C:\\outside")]
    [InlineData("plugins//base")]
    public async Task InvalidConnectionPathIsRejected(string path)
    {
        await using var manager = Create(new MemoryDataStore(), new Factory());
        await manager.InitializeAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => manager.AddAsync(path));
    }

    [Fact]
    public async Task DuplicateIdsAreRejectedAndPersistedDuplicatesAreNotActivated()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/first", "org.gravia.base");
        await Manifest(data, "plugins/second", "org.gravia.base");
        await using (var manager = Create(data, new Factory()))
        {
            await manager.InitializeAsync();
            await manager.AddAsync("plugins/first");
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.AddAsync("plugins/second"));
        }
        await data.SaveAsync(PluginManager.FileName, new[]
        {
            new PluginRegistration("plugins/first", true), new("plugins/second", true)
        });
        var factory = new Factory();
        await using var restored = Create(data, factory);
        await restored.InitializeAsync();
        Assert.All(restored.Plugins, item => Assert.NotNull(item.Error));
        Assert.Empty(factory.Events);
    }

    [Fact]
    public async Task UnsupportedExecutionIsReportedWithoutPretendingThePluginIsActive()
    {
        var data = await DependencyProject();
        await using var manager = Create(data);
        await manager.InitializeAsync();
        Assert.All(manager.Plugins, item => { Assert.False(item.IsActive); Assert.NotNull(item.Error); });
    }

    [Fact]
    public async Task InvalidProjectListIsNotOverwritten()
    {
        var data = new MemoryDataStore();
        var invalid = Encoding.UTF8.GetBytes("{ invalid json");
        await data.SaveBytesAsync(PluginManager.FileName, invalid);
        await using var manager = Create(data, new Factory());
        await Assert.ThrowsAsync<JsonException>(() => manager.InitializeAsync());
        Assert.Equal(invalid, await data.ReadBytesAsync(PluginManager.FileName));
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.AddAsync("plugins/base"));
    }

    [Fact]
    public async Task CancellationDuringActivationCleansUpAndAllowsRetry()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/base", "org.gravia.base");
        await data.SaveAsync(PluginManager.FileName, new[] { new PluginRegistration("plugins/base", true) });
        var factory = new Factory { WaitDuringActivation = true };
        await using var manager = Create(data, factory);
        using var cancellation = new CancellationTokenSource();
        var initialize = manager.InitializeAsync(cancellation.Token);
        await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialize);
        Assert.Contains("stop:org.gravia.base", factory.Events);
        Assert.False(manager.Plugins[0].IsActive);
        factory.WaitDuringActivation = false;
        await manager.InitializeAsync();
        Assert.True(manager.Plugins[0].IsActive);
    }

    [Fact]
    public async Task DisableWaitsForActivationAndStopsTheActivatedInstance()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/base", "org.gravia.base");
        var factory = new Factory { WaitDuringActivation = true };
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        await manager.AddAsync("plugins/base");
        var enabling = manager.SetEnabledAsync("plugins/base", true);
        await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disabling = manager.SetEnabledAsync("plugins/base", false);
        Assert.False(disabling.IsCompleted);
        factory.Release.TrySetResult();
        await enabling.WaitAsync(TimeSpan.FromSeconds(5));
        await disabling.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(manager.Plugins[0].Enabled);
        Assert.False(manager.Plugins[0].IsActive);
        Assert.Equal(["start:org.gravia.base", "stop:org.gravia.base"], factory.Events);
    }

    private static PluginManager Create(MemoryDataStore data, params IPluginFactory[] factories)
        => new(ProjectPath, data, NullLogger<PluginManager>.Instance, factories);

    private static async Task<MemoryDataStore> DependencyProject(bool enabled = true)
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/base", "org.gravia.base");
        await Manifest(data, "plugins/consumer", "org.gravia.consumer", dependency: "org.gravia.base");
        await data.SaveAsync(PluginManager.FileName, new[]
        {
            new PluginRegistration("plugins/consumer", enabled), new("plugins/base", enabled)
        });
        return data;
    }

    private static Task Manifest(MemoryDataStore data, string path, string id, string version = "1.2.3", string? dependency = null)
        => data.SaveBytesAsync($"{path}/{PluginManifest.FileName}", JsonSerializer.SerializeToUtf8Bytes(new PluginManifest
        {
            Id = id, Version = version, Name = new LocalizedText(id),
            Dependencies = dependency is null ? new Dictionary<string, string>() : new() { [dependency] = "^1.2.3" }
        }, JsonSerializerOptions.Web));

    private sealed class Factory : IPluginFactory
    {
        public List<string> Events { get; } = [];
        public string? FailActivation { get; set; }
        public string? FailDeactivation { get; set; }
        public bool WaitDuringActivation { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Supports(PluginExecution execution) => execution is ProcessPluginExecution;
        public Task<Plugin> CreateAsync(string directory, PluginManifest manifest, CancellationToken cancellationToken = default)
        {
            Assert.True(Path.IsPathFullyQualified(directory));
            return Task.FromResult<Plugin>(new Instance(manifest, this));
        }

        private sealed class Instance(PluginManifest manifest, Factory owner) : Plugin(manifest)
        {
            public override async Task ActivateAsync(CancellationToken cancellationToken = default)
            {
                owner.Events.Add($"start:{Manifest.Id}");
                if (owner.FailActivation == Manifest.Id) throw new InvalidOperationException("Activation failed.");
                if (owner.WaitDuringActivation)
                {
                    owner.Entered.TrySetResult();
                    await owner.Release.Task.WaitAsync(cancellationToken);
                }
            }

            public override Task DeactivateAsync(CancellationToken cancellationToken = default)
            {
                owner.Events.Add($"stop:{Manifest.Id}");
                if (owner.FailDeactivation == Manifest.Id) throw new IOException("Deactivation failed.");
                return Task.CompletedTask;
            }
        }
    }
}
