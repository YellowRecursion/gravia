using System.Text.Json;
using Gravia.Application.Plugins.Interfaces;
using Gravia.Application.Plugins.Services;
using Gravia.Application.Projects.Services;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Plugins.Entities;
using Gravia.Infrastructure.Projects;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Infrastructure.Tests.Projects;

public sealed class PluginProjectSessionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gravia-plugin-session-" + Guid.NewGuid().ToString("N"));

    public PluginProjectSessionTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task OpenActivatesPluginAndCloseDeactivatesBeforeStoreIsReleased()
    {
        await WritePluginProject();
        ProjectSession? session = null;
        var factory = new Factory(async () => await session!.DataStore.SaveBytesAsync("stopped.bin", [1]));
        var sessionFactory = new ProjectSessionFactory(NullLoggerFactory.Instance, [factory]);
        session = await sessionFactory.OpenAsync(_directory);
        Assert.True(Assert.Single(session.PluginManager.Controllers).Plugin?.IsLoaded == true);
        Assert.Equal(Path.Combine(_directory, "plugins", "example"), factory.Directory);
        await session.DisposeAsync();
        await session.DisposeAsync();
        Assert.Equal(1, factory.Stops);
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(Path.Combine(_directory, "stopped.bin")));

        await using var reopened = await sessionFactory.OpenAsync(_directory);
        session = reopened;
        Assert.True(Assert.Single(reopened.PluginManager.Controllers).Plugin?.IsLoaded == true);
    }

    [Fact]
    public async Task UnimplementedRuntimeDoesNotPreventProjectOpening()
    {
        await WritePluginProject();
        await using var session = await new ProjectSessionFactory(NullLoggerFactory.Instance).OpenAsync(_directory);
        var plugin = Assert.Single(session.PluginManager.Controllers);
        Assert.False(plugin.Plugin?.IsLoaded == true);
    }

    private async Task WritePluginProject()
    {
        var directory = Path.Combine(_directory, "plugins", "example");
        Directory.CreateDirectory(directory);
        var manifest = new PluginManifest
        {
            Id = "org.gravia.example", Version = "1.0.0", Name = new LocalizedText("Example")
        };
        await File.WriteAllTextAsync(Path.Combine(directory, PluginManifest.FileName),
            JsonSerializer.Serialize(manifest, JsonSerializerOptions.Web));
        await File.WriteAllTextAsync(Path.Combine(_directory, PluginManager.FileName),
            """[{"path":"plugins/example"}]""");
    }

    private sealed class Factory(Func<Task> onStop) : IPluginFactory
    {
        public string? Directory { get; private set; }
        public int Stops { get; private set; }
        public bool Supports(PluginExecution execution) => execution is ProcessPluginExecution;
        public Task<Plugin> CreateAsync(string directory, PluginManifest manifest, CancellationToken cancellationToken = default)
        {
            Directory = directory;
            return Task.FromResult<Plugin>(new Instance(manifest, this, onStop));
        }

        private sealed class Instance(PluginManifest manifest, Factory owner, Func<Task> onStop) : Plugin(manifest)
        {
            protected override Task OnActivateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            protected override async Task OnDeactivateAsync(CancellationToken cancellationToken = default)
            {
                owner.Stops++;
                await onStop();
            }
        }
    }
}
