using System.Text;
using System.Text.Json;
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
    public async Task AddPersistsOnlyRelativePathAndActivates()
    {
        var data = new MemoryDataStore();
        var factory = new Factory();
        await Manifest(data, "plugins/base", "org.gravia.base");
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        await manager.AddAsync("plugins\\base/");
        var plugin = Assert.Single(manager.Controllers);
        Assert.Equal("plugins/base", plugin.Path);
        Assert.Equal("org.gravia.base", plugin.Manifest!.Id);
        Assert.Equal(["start:org.gravia.base"], factory.Events);
        using var json = JsonDocument.Parse((await data.ReadBytesAsync(PluginManager.FileName))!);
        var registration = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Single(registration.EnumerateObject());
        Assert.Equal("plugins/base", registration.GetProperty("path").GetString());
    }

    [Fact]
    public async Task InitializeStartsDependenciesFirstAndDisposeStopsDependentsFirst()
    {
        var data = await DependencyProject();
        var factory = new Factory();
        var manager = Create(data, factory);
        await manager.InitializeAsync();
        Assert.Equal(["start:org.gravia.base", "start:org.gravia.consumer"], factory.Events);
        Assert.All(manager.Controllers, item => Assert.True(item.Plugin?.IsLoaded == true));
        await manager.InitializeAsync();
        Assert.Equal(2, factory.Events.Count);
        await manager.DisposeAsync();
        await manager.DisposeAsync();
        Assert.Equal(["start:org.gravia.base", "start:org.gravia.consumer", "stop:org.gravia.consumer", "stop:org.gravia.base"], factory.Events);
        Assert.All(manager.Controllers, item => Assert.False(item.Plugin?.IsLoaded == true));
        Assert.Equal(0, data.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.AddAsync("plugins/other"));
        Assert.Equal(2, (await data.ReadAsync<PluginRegistration[]>(PluginManager.FileName))!.Length);
    }

    [Fact]
    public async Task AddingDependencyRecoversBlockedConsumerAndRemovalRequiresDependentsFirst()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/consumer", "org.gravia.consumer", dependency: "org.gravia.base");
        await Manifest(data, "plugins/base", "org.gravia.base");
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        await manager.AddAsync("plugins/consumer");
        Assert.Empty(factory.Events);
        await manager.AddAsync("plugins/base");
        Assert.All(manager.Controllers, item => Assert.True(item.Plugin?.IsLoaded == true));
        Assert.Equal(["start:org.gravia.base", "start:org.gravia.consumer"], factory.Events);
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RemoveAsync("plugins/base"));
        await manager.RemoveAsync("plugins/consumer");
        await manager.RemoveAsync("plugins/base");
        Assert.Empty(manager.Controllers);
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
        Assert.False(manager.Controllers[0].Plugin?.IsLoaded == true);
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
            new PluginRegistration("plugins/first"), new("plugins/second"),
            new("plugins/missing"), new("plugins/independent")
        });
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        Assert.All(manager.Controllers.Take(3), item => { Assert.False(item.Plugin?.IsLoaded == true); });
        Assert.True(manager.Controllers[3].Plugin?.IsLoaded == true);
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
        factory.Events.Clear();
        await Manifest(data, "plugins/base", "org.gravia.base", "1.4.0");
        await manager.ReloadAsync("plugins/base");
        Assert.Equal(["stop:org.gravia.consumer", "stop:org.gravia.base", "start:org.gravia.base", "start:org.gravia.consumer"], factory.Events);
        Assert.All(manager.Controllers, item => Assert.True(item.Plugin?.IsLoaded == true));
        Assert.Equal("1.4.0", manager.Controllers.Single(item => item.Path == "plugins/base").Manifest!.Version);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("duplicate-id")]
    [InlineData("changed-id")]
    [InlineData("incompatible-version")]
    [InlineData("missing-dependency")]
    [InlineData("cycle")]
    public async Task InvalidReloadPreservesRunningPluginsAndTheirManifests(string failure)
    {
        var data = await DependencyProject();
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        await Manifest(data, "plugins/independent", "org.gravia.independent");
        await manager.AddAsync("plugins/independent");
        var before = manager.Controllers.Select(item => (item.Plugin, item.Manifest)).ToArray();
        factory.Events.Clear();
        if (failure == "malformed")
            await data.SaveBytesAsync("plugins/base/plugin.json", Encoding.UTF8.GetBytes("{ invalid json"));
        else
            await Manifest(data, "plugins/base",
                failure == "duplicate-id" ? "org.gravia.independent" : failure == "changed-id" ? "org.gravia.renamed" : "org.gravia.base",
                failure == "incompatible-version" ? "2.0.0" : "1.2.3",
                failure == "missing-dependency" ? "org.gravia.absent" : failure == "cycle" ? "org.gravia.consumer" : null);

        if (failure == "malformed")
            await Assert.ThrowsAsync<JsonException>(() => manager.ReloadAsync("plugins/base"));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ReloadAsync("plugins/base"));

        Assert.Empty(factory.Events);
        for (var i = 0; i < before.Length; i++)
        {
            Assert.Same(before[i].Plugin, manager.Controllers[i].Plugin);
            Assert.Same(before[i].Manifest, manager.Controllers[i].Manifest);
            Assert.True(manager.Controllers[i].Plugin!.IsLoaded);
        }
    }

    [Fact]
    public async Task MissingManifestRemainsVisibleAndCanBeReloadedOrRemoved()
    {
        var data = new MemoryDataStore();
        await data.SaveAsync(PluginManager.FileName, new[] { new PluginRegistration("plugins/base") });
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        Assert.Null(Assert.Single(manager.Controllers).Manifest);
        await Manifest(data, "plugins/base", "org.gravia.base");
        await manager.ReloadAsync("plugins/base");
        Assert.True(manager.Controllers[0].Plugin?.IsLoaded == true);
        await manager.RemoveAsync("plugins/base");
        Assert.Empty(manager.Controllers);
    }

    [Fact]
    public async Task ActivationFailureIsCleanedUpAndCanBeRetried()
    {
        var data = await DependencyProject();
        var factory = new Factory { FailActivation = "org.gravia.consumer" };
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        Assert.Contains("stop:org.gravia.consumer", factory.Events);
        Assert.False(manager.Controllers[0].Plugin?.IsLoaded == true);
        factory.FailActivation = null;
        await manager.ReloadAsync("plugins/consumer");
        Assert.True(manager.Controllers[0].Plugin?.IsLoaded == true);
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
        Assert.False(Assert.IsAssignableFrom<Plugin>(manager.Controllers[0].Plugin).IsLoaded);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.ReloadAsync("plugins/consumer"));
        factory.FailDeactivation = null;
        factory.Events.Clear();
        await manager.DisposeAsync();
        await manager.DisposeAsync();
        Assert.Equal(["stop:org.gravia.consumer"], factory.Events);
        Assert.All(manager.Controllers, item => Assert.Null(item.Plugin));
    }

    [Fact]
    public async Task SaveFailureDoesNotCommitAddedOrRemovedRegistration()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/base", "org.gravia.base");
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        data.WriteFailure = new IOException("Disk unavailable.");
        await Assert.ThrowsAsync<IOException>(() => manager.AddAsync("plugins/base"));
        Assert.Empty(manager.Controllers);
        data.WriteFailure = null;
        await manager.AddAsync("plugins/base");
        factory.Events.Clear();
        var saved = await data.ReadBytesAsync(PluginManager.FileName);
        data.WriteFailure = new IOException("Disk unavailable.");
        await Assert.ThrowsAsync<IOException>(() => manager.RemoveAsync("plugins/base"));
        Assert.Single(manager.Controllers);
        Assert.True(manager.Controllers[0].Plugin?.IsLoaded == true);
        Assert.Equal(saved, await data.ReadBytesAsync(PluginManager.FileName));
        Assert.Equal(["stop:org.gravia.base", "start:org.gravia.base"], factory.Events);
    }

    [Fact]
    public async Task CancelledRemovalRestoresActivationBeforePropagatingCancellation()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/base", "org.gravia.base");
        await using var manager = Create(data, new Factory());
        await manager.InitializeAsync();
        await manager.AddAsync("plugins/base");
        using var cancellation = new CancellationTokenSource();
        data.BeforeSave = (_, _) => { cancellation.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.RemoveAsync("plugins/base", cancellation.Token));
        Assert.True(Assert.Single(manager.Controllers).Plugin!.IsLoaded);
    }

    [Fact]
    public async Task RemovalReportsBothSaveFailureAndFailedRestoration()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/base", "org.gravia.base");
        var factory = new Factory();
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        await manager.AddAsync("plugins/base");
        data.WriteFailure = new IOException("Disk unavailable.");
        factory.FailActivation = "org.gravia.base";
        var error = await Assert.ThrowsAsync<AggregateException>(() => manager.RemoveAsync("plugins/base"));
        Assert.Same(data.WriteFailure, error.InnerExceptions[0]);
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.False(Assert.Single(manager.Controllers).Plugin?.IsLoaded == true);
        Assert.Single((await data.ReadAsync<PluginRegistration[]>(PluginManager.FileName))!);
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
            new PluginRegistration("plugins/first"), new("plugins/second")
        });
        var factory = new Factory();
        await using var restored = Create(data, factory);
        await restored.InitializeAsync();
        Assert.Empty(factory.Events);
    }

    [Fact]
    public async Task UnsupportedExecutionDoesNotActivatePlugin()
    {
        var data = await DependencyProject();
        await using var manager = Create(data);
        await manager.InitializeAsync();
        Assert.All(manager.Controllers, item => { Assert.False(item.Plugin?.IsLoaded == true); });
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
        await data.SaveAsync(PluginManager.FileName, new[] { new PluginRegistration("plugins/base") });
        var factory = new Factory { WaitDuringActivation = true };
        await using var manager = Create(data, factory);
        using var cancellation = new CancellationTokenSource();
        var initialize = manager.InitializeAsync(cancellation.Token);
        await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialize);
        Assert.Contains("stop:org.gravia.base", factory.Events);
        Assert.False(manager.Controllers[0].Plugin?.IsLoaded == true);
        factory.WaitDuringActivation = false;
        await manager.InitializeAsync();
        Assert.True(manager.Controllers[0].Plugin?.IsLoaded == true);
    }

    [Fact]
    public async Task RemoveWaitsForActivationAndStopsTheActivatedInstance()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/base", "org.gravia.base");
        var factory = new Factory { WaitDuringActivation = true };
        await using var manager = Create(data, factory);
        await manager.InitializeAsync();
        var adding = manager.AddAsync("plugins/base");
        await factory.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var removing = manager.RemoveAsync("plugins/base");
        Assert.False(removing.IsCompleted);
        factory.Release.TrySetResult();
        await adding.WaitAsync(TimeSpan.FromSeconds(5));
        await removing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(manager.Controllers);
        Assert.Equal(["start:org.gravia.base", "stop:org.gravia.base"], factory.Events);
    }

    private static PluginManager Create(MemoryDataStore data, params IPluginFactory[] factories)
        => new(ProjectPath, data, NullLogger<PluginManager>.Instance, factories);

    private static async Task<MemoryDataStore> DependencyProject()
    {
        var data = new MemoryDataStore();
        await Manifest(data, "plugins/base", "org.gravia.base");
        await Manifest(data, "plugins/consumer", "org.gravia.consumer", dependency: "org.gravia.base");
        await data.SaveAsync(PluginManager.FileName, new[]
        {
            new PluginRegistration("plugins/consumer"), new("plugins/base")
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
            protected override async Task OnActivateAsync(CancellationToken cancellationToken = default)
            {
                Assert.False(IsLoaded);
                owner.Events.Add($"start:{Manifest.Id}");
                if (owner.FailActivation == Manifest.Id) throw new InvalidOperationException("Activation failed.");
                if (owner.WaitDuringActivation)
                {
                    owner.Entered.TrySetResult();
                    await owner.Release.Task.WaitAsync(cancellationToken);
                }
            }

            protected override Task OnDeactivateAsync(CancellationToken cancellationToken = default)
            {
                Assert.False(IsLoaded);
                owner.Events.Add($"stop:{Manifest.Id}");
                if (owner.FailDeactivation == Manifest.Id) throw new IOException("Deactivation failed.");
                return Task.CompletedTask;
            }
        }
    }
}
