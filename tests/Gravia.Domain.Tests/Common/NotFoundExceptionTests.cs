using Gravia.Domain.CommonExceptions;

namespace Gravia.Domain.Tests.Common;

public sealed class NotFoundExceptionTests
{
    [Fact]
    public void UsesGenericCodeByDefault()
    {
        var exception = new NotFoundException("The setting is not registered.");

        Assert.Equal("not_found", exception.Code);
        Assert.Equal("The setting is not registered.", exception.Message);
        Assert.Empty(exception.Extra);
    }
}
