using System.Threading.Channels;
using ActualLab.Fusion;
using Gravia.Application.Logging;

namespace Gravia.Host.Fusion.Logging;

/// <summary>Coalesces change notifications, not records. No polling and no deliberate update delay.</summary>
public sealed class LogJournalInvalidation(ILogJournal journal, LogJournalApi api) : BackgroundService
{
    private readonly Channel<bool> _changes = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        journal.Changed += OnChanged;
        OnChanged(); // Covers any cached state computed before the subscription.
        return base.StartAsync(cancellationToken);
    }

    private void OnChanged() => _changes.Writer.TryWrite(true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var change in _changes.Reader.ReadAllAsync(stoppingToken))
        {
            using var invalidation = Invalidation.Begin();
            _ = api.GetState(CancellationToken.None);
        }
    }

    public override void Dispose()
    {
        journal.Changed -= OnChanged;
        _changes.Writer.TryComplete();
        base.Dispose();
    }
}
