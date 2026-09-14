using System.Collections.ObjectModel;
using System.Diagnostics;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Middlewares;
using FluentValidation;
using Gravia.Domain.Common;

namespace Gravia.Host.Fusion;

/// <summary>Converts failures of Fusion API methods to their declared <see cref="RpcResult"/> type.</summary>
public sealed class RpcExceptionMiddleware(ILogger<RpcExceptionMiddleware> logger) : IRpcMiddleware
{
    public double Priority => RpcInboundMiddlewarePriority.Initial;

    public Func<RpcInboundCall, Task<T>> Create<T>(
        RpcMiddlewareContext<T> context,
        Func<RpcInboundCall, Task<T>> next)
    {
        if (!typeof(RpcResult).IsAssignableFrom(typeof(T)))
            return next;

        return async call =>
        {
            try
            {
                return await next(call);
            }
            catch (OperationCanceledException) when (call.CallCancelToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DomainException exception)
            {
                return RpcResult.Failure<T>(exception.Code, exception.Message, CreateTraceId(),
                    exception.Extra.Count == 0 ? null : exception.Extra);
            }
            catch (ValidationException exception)
            {
                var errors = exception.Errors.Select(error => new RpcFieldError(
                    error.PropertyName,
                    string.IsNullOrWhiteSpace(error.ErrorCode) ? "invalid_value" : error.ErrorCode,
                    error.ErrorMessage)).ToArray();
                IReadOnlyDictionary<string, object?> data = new ReadOnlyDictionary<string, object?>(
                    new Dictionary<string, object?> { ["errors"] = errors });
                return RpcResult.Failure<T>("validation_failed", "The request is invalid.", CreateTraceId(), data);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                var traceId = CreateTraceId();
                try
                {
                    logger.LogError(exception, "Unexpected failure. TraceId: {TraceId}", traceId);
                }
                catch (Exception loggingFailure) when (loggingFailure is not OutOfMemoryException)
                {
                    // A faulty logger must not replace the original failure or expose it to the client.
                }

                return RpcResult.Failure<T>(
                    "internal_error", "An internal error occurred.", traceId);
            }
        };
    }

    private static string CreateTraceId()
        => Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}
