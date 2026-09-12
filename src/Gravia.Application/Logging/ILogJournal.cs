namespace Gravia.Application.Logging;

/// <summary>Replaceable, process-local log history. No calculation or project lifecycle semantics.</summary>
public interface ILogJournal
{
    bool TryWrite(LogMessage message);
    LogPage Read(LogQuery? query = null);
    LogStatistics GetStatistics();
    void Clear();
}

