using System.Collections.ObjectModel;
using System.Diagnostics;
using FluentValidation;
using Gravia.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Gravia.Host.Errors;

/// <summary>
/// Classifies a failure at its owning boundary. Call once, not at every rethrow.
/// Expected rejections are not logged as technical faults.
/// </summary>
public sealed class ExceptionHandler(ILogger<ExceptionHandler> logger)
{
    public ErrorDetails Handle(Exception exception, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var empty = ReadOnlyDictionary<string, object?>.Empty;

        switch (exception)
        {
            case DomainException domain:
                return new(ErrorKind.Domain, domain.Code, domain.Message, traceId,
                    new ReadOnlyDictionary<string, object?>(
                        new Dictionary<string, object?>(domain.Extra, StringComparer.Ordinal)), []);

            case ValidationException validation:
                var fields = validation.Errors.Select(failure => new FieldError(
                    failure.PropertyName,
                    string.IsNullOrWhiteSpace(failure.ErrorCode) ? "invalid_value" : failure.ErrorCode,
                    failure.ErrorMessage)).ToArray();
                return new(ErrorKind.Validation, "validation_failed", "The request is invalid.",
                    traceId, empty, Array.AsReadOnly(fields));

            case OperationCanceledException when cancellationToken.IsCancellationRequested:
                return new(ErrorKind.Cancelled, "operation_cancelled", "The operation was cancelled.",
                    traceId, empty, []);

            default:
                // A faulty replacement logger must not replace the original failure or expose it.
                try
                {
                    logger.LogError(exception, "Unexpected failure. TraceId: {TraceId}", traceId);
                }
                catch (Exception loggingFailure) when (loggingFailure is not OutOfMemoryException)
                {
                    // Best effort: the caller still receives a safe failure, never success.
                }
                return new(ErrorKind.Internal, "internal_error", "An internal error occurred.",
                    traceId, empty, []);
        }
    }
}
