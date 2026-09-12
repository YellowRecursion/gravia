using Gravia.Domain.Projects.Exceptions;

namespace Gravia.Domain.Projects;

/// <summary>
/// A user's modelling project. Its identity does not depend on its name or location.
/// Storage format, filesystem paths and editor session state are not domain data.
/// </summary>
public sealed class Project
{
    private Project(Guid id, string name)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Project identity must not be empty.", nameof(id));

        Id = id;
        Name = NormalizeName(name);
    }

    public Guid Id { get; }
    public string Name { get; private set; }

    public static Project Create(string name) => new(Guid.NewGuid(), name);

    /// <summary>
    /// Reconstitutes an existing project without assigning a new identity.
    /// The persistence adapter is responsible for validating its document format.
    /// </summary>
    public static Project Open(Guid id, string name) => new(id, name);

    public void Rename(string name) => Name = NormalizeName(name);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ProjectNameRequiredException();

        return name.Trim();
    }
}
