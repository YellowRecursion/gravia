using Gravia.Domain.Projects;
using Gravia.Domain.Projects.Exceptions;

namespace Gravia.Domain.Tests.Projects;

public sealed class ProjectTests
{
    [Fact]
    public void CreateAssignsDistinctNonEmptyIdentitiesEvenForTheSameName()
    {
        var first = Project.Create("FCC");
        var second = Project.Create("FCC");

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("FCC", "FCC")]
    [InlineData("  Оптимизация FCC  ", "Оптимизация FCC")]
    [InlineData("\tПроект\r\n", "Проект")]
    [InlineData("FCC: сырьё / продукт", "FCC: сырьё / продукт")]
    public void CreateNormalizesDisplayNameWithoutApplyingFilenameRules(string name, string expected)
    {
        Assert.Equal(expected, Project.Create(name).Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData("\u2003")]
    public void CreateRejectsMissingNameWithStableDomainCode(string? name)
    {
        var exception = Assert.Throws<ProjectNameRequiredException>(() => Project.Create(name!));

        Assert.Equal("project_name_required", exception.Code);
        Assert.Empty(exception.Extra);
    }

    [Fact]
    public void RenamePreservesIdentity()
    {
        var project = Project.Create("FCC");
        var originalId = project.Id;

        project.Rename("  Новый проект  ");

        Assert.Equal(originalId, project.Id);
        Assert.Equal("Новый проект", project.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void InvalidRenameDoesNotChangeExistingProject(string? name)
    {
        var project = Project.Create("FCC");
        var originalId = project.Id;

        Assert.Throws<ProjectNameRequiredException>(() => project.Rename(name!));

        Assert.Equal("FCC", project.Name);
        Assert.Equal(originalId, project.Id);
    }

    [Fact]
    public void OpenPreservesExistingIdentity()
    {
        var id = Guid.NewGuid();
        var restored = Project.Open(id, "  FCC  ");

        Assert.Equal(id, restored.Id);
        Assert.Equal("FCC", restored.Name);
    }

    [Fact]
    public void EmptyExistingIdentityIsAnInvalidArgumentNotABusinessRejection()
    {
        var exception = Assert.Throws<ArgumentException>(() => Project.Open(Guid.Empty, "FCC"));

        Assert.Equal("id", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void OpenDoesNotBypassNameInvariant(string? name)
    {
        Assert.Throws<ProjectNameRequiredException>(() => Project.Open(Guid.NewGuid(), name!));
    }
}
