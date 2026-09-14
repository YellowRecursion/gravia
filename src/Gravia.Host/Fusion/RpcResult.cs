namespace Gravia.Host.Fusion;

public class RpcResult
{
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? TraceId { get; init; }

    public IReadOnlyDictionary<string, object?>? ErrorData { get; init; }

    public bool IsSuccess => ErrorCode is null;
}

public sealed class RpcResult<T> : RpcResult
{
    public T? Data { get; init; }
}