using Gravia.Host.Errors;

namespace Gravia.Host;

/// <summary>
/// Outermost boundary for the awaited host lifetime, including build and disposal.
/// It cannot catch exceptions from detached tasks or arbitrary threads.
/// </summary>
public static class HostProcess
{
    public static async Task<int> RunAsync(Func<CancellationToken, Task> run,
        ExceptionHandler exceptionHandler, TextWriter errorOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(exceptionHandler);
        ArgumentNullException.ThrowIfNull(errorOutput);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await run(cancellationToken);
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var error = exceptionHandler.Handle(exception, cancellationToken);
            if (error.Kind == ErrorKind.Cancelled) return 0;

            // No raw exception message, request payload or Extra in process output.
            // The process boundary terminates; it must not pretend a failed startup succeeded.
            try
            {
                await errorOutput.WriteLineAsync($"{error.Code}: {error.Message} (traceId: {error.TraceId})");
            }
            catch (Exception outputFailure) when (outputFailure is not OutOfMemoryException)
            {
                // Broken stderr must not change a failure exit code into another exception.
            }
            return 1;
        }
    }
}
