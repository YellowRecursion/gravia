namespace Gravia.Domain.Values.Entities;

public sealed record ListType(TypeDescriptor Element) : TypeDescriptor
{
    public override string Kind => "list";
}
