namespace Gravia.Infrastructure;

/// <summary>Provides application directories without creating them.</summary>
public static class ApplicationPaths
{
    /// <summary>Gets the current user's application storage directory.</summary>
    public static string StorageDirectory
    {
        get
        {
            var directory = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify);

            if (!Path.IsPathFullyQualified(directory))
                throw new InvalidOperationException("The local application data directory is unavailable.");

            return Path.Combine(directory, "Gravia");
        }
    }
}
