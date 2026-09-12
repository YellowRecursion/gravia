using FluentValidation;
using FluentValidation.Results;

namespace Gravia.Application.Projects;

internal static class ProjectDirectory
{
    public static ValidationFailure? Validate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Failure("project_directory_required", "Project directory is required.");
        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return Failure("project_directory_invalid", "Project directory is invalid.");
        if (!Path.IsPathFullyQualified(path))
            return Failure("project_directory_absolute_required", "Project directory must be an absolute path.");
        try
        {
            _ = Path.GetFullPath(path);
            return null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failure("project_directory_invalid", "Project directory is invalid.");
        }
    }

    public static string Normalize(string path)
    {
        if (Validate(path) is { } failure)
            throw new ValidationException([failure]);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static ValidationFailure Failure(string code, string message)
        => new("directoryPath", message) { ErrorCode = code };
}
