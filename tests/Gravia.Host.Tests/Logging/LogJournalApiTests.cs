using ActualLab.Fusion;
using Gravia.Application.Logging;
using Gravia.Host.Fusion.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FluentValidation;

namespace Gravia.Host.Tests.Logging;

public sealed class LogJournalApiTests
{
    private static IHost Create()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddHostServices();
        return builder.Build();
    }

    [Fact]
    public async Task ILoggerWriteAndClearInvalidateTheSameSmallComputeState()
    {
        using var host = Create();
        await host.StartAsync();
        try
        {
            var api = host.Services.GetRequiredService<LogJournalApi>();
            var journal = host.Services.GetRequiredService<ILogJournal>();
            var before = await Computed.Capture(() => api.GetState());
            var previous = before.Value.Data!;
            host.Services.GetRequiredService<ILogger<LogJournalApiTests>>()
                .LogError(new InvalidOperationException("outer", new NullReferenceException("inner")), "failure");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await before.WhenInvalidated(timeout.Token);
            var batch = (await api.Read(previous.JournalId, previous.LastSequence)).Data!;
            var entry = Assert.Single(batch.Entries);
            Assert.Contains("NullReferenceException: inner", entry.Exception);
            Assert.True(batch.State.Revision > previous.Revision);
            Assert.Equal(journal.GetState(), batch.State);
            var updated = await Computed.Capture(() => api.GetState());
            await api.Clear();
            await updated.WhenInvalidated(timeout.Token);
            var cleared = (await api.Read(batch.State.JournalId, batch.State.LastSequence)).Data!;
            Assert.Empty(cleared.Entries);
            Assert.Equal(batch.State.LastSequence + 1, cleared.State.FirstSequence);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task FreshConnectionReadsAllHistoryAndRejectsNegativeSequence()
    {
        using var host = Create();
        var api = host.Services.GetRequiredService<LogJournalApi>();
        var journal = host.Services.GetRequiredService<ILogJournal>();
        for (var i = 0; i < 700; i++) journal.TryWrite(new(LogLevel.Error, "Model", "NRE"));
        var batch = (await api.Read(null, 0)).Data!;
        Assert.True(batch.Reset);
        Assert.Equal(700, batch.Entries.Count);
        Assert.Empty((await api.Read(batch.State.JournalId, batch.State.LastSequence)).Data!.Entries);
        await Assert.ThrowsAsync<ValidationException>(() => api.Read(null, -1));
    }
}
