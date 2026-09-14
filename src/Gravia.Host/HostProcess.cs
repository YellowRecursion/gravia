using System.Diagnostics;
using Gravia.Domain.Common;

namespace Gravia.Host;

/// <summary>
/// Outermost boundary for the awaited host lifetime, including build and disposal.
/// It cannot catch exceptions from detached tasks or arbitrary threads.
/// </summary>
public static class HostProcess
{
    public static async Task<int> RunAsync(Func<CancellationToken, Task> run,
        ILogger logger, TextWriter errorOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(errorOutput);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await run(cancellationToken);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (DomainException exception)
        {
            return await WriteFailure(exception.Code, exception.Message, CreateTraceId());
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var traceId = CreateTraceId();
            try
            {
                logger.LogError(exception, "Unexpected process failure. TraceId: {TraceId}", traceId);
            }
            catch (Exception loggingFailure) when (loggingFailure is not OutOfMemoryException)
            {
                // Best effort: process failure must still produce a nonzero exit code.
            }

            return await WriteFailure("internal_error", "An internal error occurred.", traceId);
        }

        async Task<int> WriteFailure(string code, string message, string traceId)
        {
            // No raw exception message, request payload or Extra in process output.
            // The process boundary terminates; it must not pretend a failed startup succeeded.
            try
            {
                await errorOutput.WriteLineAsync($"{code}: {message} (traceId: {traceId})");
            }
            catch (Exception outputFailure) when (outputFailure is not OutOfMemoryException)
            {
                // Broken stderr must not change a failure exit code into another exception.
            }
            return 1;
        }
    }

    private static string CreateTraceId()
        => Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}
