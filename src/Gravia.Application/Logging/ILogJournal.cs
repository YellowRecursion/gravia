namespace Gravia.Application.Logging;

/// <summary>Replaceable, process-local log history. No calculation or project lifecycle semantics.</summary>
public interface ILogJournal
{
    // Notification only: subscribers must not do I/O or call back while holding their own locks.
    event Action? Changed;
    bool TryWrite(LogMessage message);
    LogBatch Read(Guid? journalId = null, long afterSequence = 0);
    LogState GetState();
    LogStatistics GetStatistics();
    void Clear();
}
