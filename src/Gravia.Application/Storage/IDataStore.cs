namespace Gravia.Application.Storage;

/// <summary>Stores documents at hierarchical paths relative to this instance's root directory.</summary>
/// <remarks>
/// Each instance is bound to a root directory for its lifetime.
/// Paths are non-empty and relative to that root, with '/' separating non-empty segments,
/// for example "settings/general" or "plugins/vendor.example/settings".
/// Absolute paths, backslashes, and '.' or '..' segments are not allowed.
/// Implementations must prevent access outside the root directory.
/// Physical file formats and extensions are implementation details.
/// </remarks>
public interface IDataStore
{
    /// <summary>
    /// Checks whether a document exists at the path without reading or deserializing it.
    /// The result does not guarantee availability during a subsequent operation.
    /// </summary>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the document, or null if no document exists at the path.
    /// Read failures and incompatible types must not be treated as missing documents.
    /// </summary>
    Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Saves a document, replacing the value at this path within this instance's root directory.
    /// A file-based implementation creates missing parent directories.
    /// </summary>
    Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Reads the entire document as raw bytes, or returns null if it does not exist.</summary>
    Task<byte[]?> ReadBytesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Saves raw bytes without serialization. The caller must not modify data until the operation completes.</summary>
    Task SaveBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>Deletes only the document at the path, not a directory tree. A missing document is not an error.</summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
