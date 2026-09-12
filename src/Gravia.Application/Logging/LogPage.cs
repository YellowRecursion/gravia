namespace Gravia.Application.Logging;

/// <summary>
/// Upsert Entries by Sequence, then continue from NextSequence.
/// HasGap means older history was evicted or cleared. A new JournalId requires a new cursor.
/// </summary>
public sealed record LogPage(
    Guid JournalId,
    IReadOnlyList<LogEntry> Entries,
    long NextSequence,
    bool HasMore,
    bool HasGap);

