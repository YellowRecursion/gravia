using System.Text.Json;
using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.EventHandlers;
using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Consts;
using Gravia.Domain.Settings.Entities;
using Gravia.Domain.Settings.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Application.Tests.Projects.Services;

[Collection("Domain events")]
public sealed class ProjectSessionTests
{
    [Fact]
    public async Task IndependentSessionsHaveIndependentValuesAndOwnedLifetimes()
    {
        var firstData = new MemoryDataStore();
        var secondData = new MemoryDataStore();
        await using var first = Create("first", firstData);
        await using var second = Create("second", secondData);
        await first.KeyValueStore.SetAsync("value", 7);
        await second.KeyValueStore.SetAsync("value", 9);

        await first.DisposeAsync();
        await first.DisposeAsync();

        Assert.Equal(1, firstData.DisposeCount);
        Assert.Equal(0, secondData.DisposeCount);
        Assert.Equal(9, await second.KeyValueStore.GetAsync<int>("value"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => first.KeyValueStore.GetAsync<int>("value"));
    }

    [Fact]
    public async Task DisposedSettingsNoLongerHandleEventsAndPreservePreviouslySavedValues()
    {
        var data = new MemoryDataStore();
        await data.SaveAsync(SettingsStorage.FileName, new Dictionary<string, int> { ["unknown"] = 17 });
        await using var session = Create("settings", data);
        var setting = Number();
        await session.Settings.Register(setting);
        setting.SetValue(42);
        await new SettingChangedHandler().Handle(new SettingChanged(setting), default);

        await session.DisposeAsync();

        var values = await data.ReadAsync<Dictionary<string, JsonElement>>(SettingsStorage.FileName);
        Assert.Equal(42, values![setting.Id].GetInt32());
        Assert.Equal(17, values["unknown"].GetInt32());
        setting.SetValue(99);
        await new SettingChangedHandler().Handle(new SettingChanged(setting), default);
        values = await data.ReadAsync<Dictionary<string, JsonElement>>(SettingsStorage.FileName);
        Assert.Equal(42, values![setting.Id].GetInt32());
    }

    [Fact]
    public async Task DisposeWaitsForKeyValueWriteBeforeReleasingDataStore()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var data = new MemoryDataStore
        {
            BeforeSave = async (_, _) => { entered.SetResult(); await release.Task; }
        };
        await using var session = Create("writing", data);
        var write = session.KeyValueStore.SetAsync("value", 10);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var closing = session.DisposeAsync().AsTask();
        try
        {
            Assert.False(closing.IsCompleted);
            Assert.Equal(0, data.DisposeCount);
        }
        finally { release.TrySetResult(); }
        await write;
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, data.DisposeCount);
    }

    [Fact]
    public async Task DisposeReleasesResourcesWithoutSavingSettingsAgain()
    {
        var data = new MemoryDataStore();
        var session = Create("failure", data);
        var setting = Number();
        await session.Settings.Register(setting);
        await new SettingChangedHandler().Handle(new SettingChanged(setting), default);
        data.WriteFailure = new IOException("Disk unavailable.");

        await session.DisposeAsync();

        Assert.Equal(1, data.DisposeCount);
        Assert.Empty(session.Settings.Settings);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.KeyValueStore.GetAsync<int>("value"));
    }

    private static ProjectSession Create(string name, MemoryDataStore data)
        => new(Path.Combine(Path.GetTempPath(), name), data,
            new ProjectSettings(data, NullLogger<ProjectSettings>.Instance), new KeyValueStore(data));

    private static IntSetting Number()
        => new("example.count", new LocalizedText("en", "Count"), 1, SettingsUiPaths.Appearance);
}
