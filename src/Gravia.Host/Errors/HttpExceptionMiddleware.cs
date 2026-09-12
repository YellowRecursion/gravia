using Microsoft.AspNetCore.WebUtilities;

namespace Gravia.Host.Errors;

/// <summary>HTTP adapter for the shared error classification. It owns logging for failed requests.</summary>
public sealed class HttpExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ExceptionHandler handler)
    {
        try { await next(context); }
        catch (Exception exception) when (!context.Response.HasStarted && exception is not OutOfMemoryException)
        {
            var error = handler.Handle(exception, context.RequestAborted);
            if (context.RequestAborted.IsCancellationRequested)
            {
                context.Response.StatusCode = 499;
                return; // The caller has disconnected; there is no response body to deliver.
            }
            var status = error.Kind == ErrorKind.Internal ? 500 : 400;
            context.Response.Clear();
            await Results.Problem(statusCode: status, title: ReasonPhrases.GetReasonPhrase(status),
                detail: error.Message, extensions: new Dictionary<string, object?>
                {
                    ["code"] = error.Code, ["traceId"] = error.TraceId,
                    ["extra"] = error.Extra, ["errors"] = error.Errors
                }).ExecuteAsync(context);
        }
    }
}
