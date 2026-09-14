namespace Gravia.Host.Fusion;

public class RpcResult
{
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? TraceId { get; private set; }

    public IReadOnlyDictionary<string, object?>? ErrorData { get; private set; }

    public bool IsSuccess => ErrorCode is null;

    public static RpcResult Success() => new();

    public static RpcResult<T> Success<T>(T? data) => new() { Data = data };

    internal static TResult Failure<TResult>(string code, string message, string traceId,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        var result = Activator.CreateInstance<TResult>();
        if (result is not RpcResult rpcResult)
            throw new InvalidOperationException($"{typeof(TResult)} is not an RPC result type.");

        rpcResult.ErrorCode = code;
        rpcResult.ErrorMessage = message;
        rpcResult.TraceId = traceId;
        rpcResult.ErrorData = data;
        return result;
    }
}

public sealed class RpcResult<T> : RpcResult
{
    public T? Data { get; init; }
}

public sealed record RpcFieldError(string Field, string Code, string Message);
