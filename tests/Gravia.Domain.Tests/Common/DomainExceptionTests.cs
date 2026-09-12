using System.Text.Json;
using Gravia.Domain.Common;

namespace Gravia.Domain.Tests.Common;

public sealed class DomainExceptionTests
{
    [Fact]
    public void PreservesCodeMessageAndInnerException()
    {
        var cause = new InvalidOperationException("Internal detail");
        var exception = new TestDomainException("rule_rejected", "Safe explanation", cause);

        Assert.Equal("rule_rejected", exception.Code);
        Assert.Equal("Safe explanation", exception.Message);
        Assert.Same(cause, exception.InnerException);
        Assert.Empty(exception.Extra);
    }

    [Fact]
    public void ExposesStructuredDetailsAsReadOnlyData()
    {
        var nodeId = Guid.NewGuid();
        var exception = new TestDomainException("rule_rejected", "Safe explanation",
            details: [("nodeId", nodeId), ("limit", 12.5m), ("optional", null)]);

        Assert.Equal(nodeId, Assert.IsType<Guid>(exception.Extra["nodeId"]));
        Assert.Equal(12.5m, exception.Extra["limit"]);
        Assert.Null(exception.Extra["optional"]);
        var dictionary = Assert.IsAssignableFrom<IDictionary<string, object?>>(exception.Extra);
        Assert.Throws<NotSupportedException>(() => dictionary.Add("unexpected", 1));
        Assert.Throws<NotSupportedException>(() => dictionary["nodeId"] = Guid.Empty);
        Assert.Throws<NotSupportedException>(() => dictionary.Clear());
    }

    [Fact]
    public void ReplacesDuplicateKeysAndUsesOrdinalKeyComparison()
    {
        var exception = new TestDomainException("rule_rejected", "Safe explanation",
            details: [("value", 1), ("value", 2), ("Value", 3)]);

        Assert.Equal(2, exception.Extra.Count);
        Assert.Equal(2, exception.Extra["value"]);
        Assert.Equal(3, exception.Extra["Value"]);
    }

    [Fact]
    public void DetailsAreNotSharedBetweenExceptions()
    {
        var first = new TestDomainException("rule_rejected", "Safe explanation", details: [("value", 1)]);
        var second = new TestDomainException("rule_rejected", "Safe explanation");

        Assert.Single(first.Extra);
        Assert.Empty(second.Extra);
    }

    [Fact]
    public void SelectedClientDataCanBeSerializedWithoutExceptionInternals()
    {
        var nodeId = Guid.NewGuid();
        var exception = new TestDomainException("rule_rejected", "Safe explanation",
            new InvalidOperationException("Internal detail"), [("nodeId", nodeId)]);

        // Illustrates an adapter projection, not serialization of Exception itself.
        var json = JsonSerializer.Serialize(new
        {
            code = exception.Code,
            message = exception.Message,
            extra = exception.Extra
        });
        using var document = JsonDocument.Parse(json);

        Assert.Equal("rule_rejected", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(nodeId, document.RootElement.GetProperty("extra").GetProperty("nodeId").GetGuid());
        Assert.DoesNotContain("Internal detail", json);
        Assert.False(document.RootElement.TryGetProperty("InnerException", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RejectsMissingCode(string? code)
    {
        Assert.ThrowsAny<ArgumentException>(() => new TestDomainException(code!, "Safe explanation"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RejectsMissingMessage(string? message)
    {
        Assert.ThrowsAny<ArgumentException>(() => new TestDomainException("rule_rejected", message!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RejectsMissingDetailKey(string? key)
    {
        Assert.ThrowsAny<ArgumentException>(() => new TestDomainException("rule_rejected", "Safe explanation",
            details: [(key!, 1)]));
    }

    private sealed class TestDomainException : DomainException
    {
        public TestDomainException(string code, string message, Exception? innerException = null,
            params (string Key, object? Value)[] details) : base(code, message, innerException)
        {
            foreach (var (key, value) in details)
                Add(key, value);
        }
    }
}
