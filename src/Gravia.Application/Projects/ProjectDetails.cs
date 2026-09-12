namespace Gravia.Application.Projects;

/// <summary>
/// An immutable client-facing projection, not the on-disk project format.
/// Includes the absolute location; receiving it does not activate an editor session.
/// </summary>
public sealed record ProjectDetails(Guid Id, string Name, string DirectoryPath);
