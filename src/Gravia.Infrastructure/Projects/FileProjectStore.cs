using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Gravia.Application.Projects;
using Gravia.Domain.Projects;

namespace Gravia.Infrastructure.Projects;

/// <summary>Versioned project passport storage; not an editor session or calculation result store.</summary>
public sealed class FileProjectStore : IProjectStore
{
    public const string ManifestFileName = "project.gravia.json";
    public const int CurrentFormatVersion = 1;
    public const int MaxManifestBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task CreateAsync(Project project, string directoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (project.Name.Length > MaxManifestBytes)
            throw TooLarge();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new Manifest(CurrentFormatVersion, project.Id, project.Name), JsonOptions);
        if (bytes.Length > MaxManifestBytes)
            throw TooLarge();

        var manifestPath = Path.Combine(directoryPath, ManifestFileName);
        string? ownedTemporaryPath = null;
        try
        {
            if (File.Exists(directoryPath))
                throw InvalidDirectory();
            if (File.Exists(manifestPath) || Directory.Exists(manifestPath))
                throw AlreadyExists();
            if (Directory.Exists(directoryPath) && Directory.EnumerateFileSystemEntries(directoryPath).Any())
                throw Error("project_directory_not_empty", "Choose a new or empty project directory.");

            Directory.CreateDirectory(directoryPath);
            var temporaryPath = Path.Combine(directoryPath, $".project.gravia.{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous))
            {
                ownedTemporaryPath = temporaryPath;
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Commit only the complete passport. Never overwrite a concurrent creator's file.
                File.Move(temporaryPath, manifestPath, overwrite: false);
                ownedTemporaryPath = null;
            }
            catch (IOException) when (File.Exists(manifestPath) || Directory.Exists(manifestPath))
            {
                throw AlreadyExists();
            }
            // No cancellation check after commit: the project now exists.
        }
        catch (UnauthorizedAccessException) { throw AccessDenied(); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw InvalidDirectory();
        }
        finally
        {
            // Only this operation's temporary file is ours to remove; never delete the directory.
            if (ownedTemporaryPath is not null)
            {
                try { File.Delete(ownedTemporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    public async Task<Project> OpenAsync(string directoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (File.Exists(directoryPath))
                throw InvalidDirectory();
            await using var stream = new FileStream(Path.Combine(directoryPath, ManifestFileName),
                FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            if (stream.Length > MaxManifestBytes)
                throw TooLarge();

            // Bound the actual read as well: another process may grow the file after Length was read.
            var bytes = new byte[MaxManifestBytes + 1];
            var count = 0;
            while (count < bytes.Length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(count), cancellationToken);
                if (read == 0) break;
                count += read;
            }
            if (count > MaxManifestBytes)
                throw TooLarge();

            // Accept UTF-8 files produced by editors that include a BOM.
            var offset = count >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            using var document = JsonDocument.Parse(bytes.AsMemory(offset, count - offset));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("formatVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var versionNumber))
                throw InvalidManifest();
            if (versionNumber != CurrentFormatVersion)
                throw Error("project_format_unsupported", "This project format version is not supported.");

            var properties = root.EnumerateObject().Select(property => property.Name).ToArray();
            if (properties.Length != 3 || properties.Distinct(StringComparer.Ordinal).Count() != 3 ||
                !root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String ||
                !id.TryGetGuid(out var projectId) || projectId == Guid.Empty ||
                !root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(name.GetString()))
                throw InvalidManifest();

            cancellationToken.ThrowIfCancellationRequested();
            return Project.Open(projectId, name.GetString()!);
        }
        catch (JsonException) { throw InvalidManifest(); }
        catch (FileNotFoundException) { throw NotFound(); }
        catch (DirectoryNotFoundException) { throw NotFound(); }
        catch (UnauthorizedAccessException) { throw AccessDenied(); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw InvalidDirectory();
        }
    }

    private static ValidationException Error(string code, string message)
        => new([new ValidationFailure("directoryPath", message) { ErrorCode = code }]);
    private static ValidationException InvalidDirectory()
        => Error("project_directory_invalid", "Project directory is invalid.");
    private static ValidationException AlreadyExists()
        => Error("project_already_exists", "A project already exists in this directory.");
    private static ValidationException InvalidManifest()
        => Error("project_manifest_invalid", "The project passport is invalid.");
    private static ValidationException NotFound()
        => Error("project_not_found", "No project passport was found in this directory.");
    private static ValidationException TooLarge()
        => Error("project_manifest_too_large", "The project passport exceeds the supported size.");
    private static ValidationException AccessDenied()
        => Error("project_access_denied", "Access to the project directory or passport was denied.");

    private sealed record Manifest(int FormatVersion, Guid Id, string Name);
}
