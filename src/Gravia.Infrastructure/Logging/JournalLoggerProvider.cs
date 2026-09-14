using System.Globalization;
using Gravia.Application.Logging;
using Microsoft.Extensions.Logging;

namespace Gravia.Infrastructure.Logging;

/// <summary>Standard ILogger adapter. Filtering is handled by LoggerFactory before formatting.</summary>
[ProviderAlias("Gravia")]
public sealed class JournalLoggerProvider(ILogJournal journal, SessionFileLog? fileLog = null) : ILoggerProvider, ISupportExternalScope
{
    private readonly ILogJournal _journal = journal;
    private readonly SessionFileLog? _fileLog = fileLog;
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName) => new JournalLogger(this, categoryName);
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        Volatile.Write(ref _scopes, scopeProvider);
    public void Dispose() { } // The journal lifetime belongs to DI, not to this adapter.

    private sealed class JournalLogger(JournalLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            Volatile.Read(ref owner._scopes).Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel is >= LogLevel.Trace and < LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            try
            {
                var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
                var scopeCount = 0;
                var truncated = false;
                Volatile.Read(ref owner._scopes).ForEachScope((scope, values) =>
                {
                    if (++scopeCount <= 16) AddProperties(scope, values, ref truncated);
                    else truncated = true;
                }, properties);
                AddProperties(state, properties, ref truncated);
                var exceptionText = FormatException(exception, ref truncated);
                // Store only rendered strings, never caller objects, exceptions or scope references.
                var message = new LogMessage(logLevel, category, formatter(state, exception), eventId,
                    exceptionText, properties, truncated);
                owner._fileLog?.TryWrite(message);
                owner._journal.TryWrite(message);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // A broken formatter/ToString must not change the outcome of the caller's operation.
                // No ILogger recursion: emit a bounded fallback directly, best effort for replaceable sinks.
                try
                {
                    var fallback = new LogMessage(LogLevel.Warning, category,
                        "A log entry could not be formatted or stored.", new EventId(0, "LoggingFailure"));
                    owner._fileLog?.TryWrite(fallback);
                    owner._journal.TryWrite(fallback);
                }
                catch (Exception fallbackError) when (fallbackError is not OutOfMemoryException) { }
            }
        }

        private static void AddProperties(object? state, Dictionary<string, string?> destination, ref bool truncated)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> values) return;
            var visited = 0;
            foreach (var pair in values)
            {
                if (++visited > 32) { truncated = true; break; }
                if (pair.Key == "{OriginalFormat}") continue;
                var key = LogText.Limit(pair.Key, 64, ref truncated);
                if (destination.Count >= 32 && !destination.ContainsKey(key)) { truncated = true; continue; }
                destination[key] = pair.Value switch
                {
                    null => null,
                    string value => LogText.Limit(value, 256, ref truncated),
                    IFormattable value => LogText.Limit(value.ToString(null, CultureInfo.InvariantCulture) ?? "", 256, ref truncated),
                    _ => LogText.Limit(pair.Value.ToString() ?? "", 256, ref truncated)
                };
            }
        }

        private static string? FormatException(Exception? exception, ref bool truncated)
        {
            if (exception is null) return null;
            // Includes inner/aggregate exceptions. No exception reference escapes the synchronous call.
            return LogText.Limit(exception.ToString(), 32768, ref truncated);
        }
    }
}
