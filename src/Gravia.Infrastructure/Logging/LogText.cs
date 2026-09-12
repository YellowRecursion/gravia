namespace Gravia.Infrastructure.Logging;

internal static class LogText
{
    public static string Limit(string value, int length, ref bool truncated)
    {
        if (value.Length <= length)
            return value;
        truncated = true;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
            length--;
        return value[..length];
    }
}

