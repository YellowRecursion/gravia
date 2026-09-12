using Gravia.Domain.Common;

namespace Gravia.Domain.Projects.Exceptions;

/// <summary>An expected rejection of a project without a meaningful display name.</summary>
public sealed class ProjectNameRequiredException : DomainException
{
    public const string ErrorCode = "project_name_required";

    public ProjectNameRequiredException() : base(ErrorCode, "Project name is required.")
    {
    }
}
