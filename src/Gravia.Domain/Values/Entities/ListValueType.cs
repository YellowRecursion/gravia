namespace Gravia.Domain.Values.Entities;

public sealed record ListValueType(ValueTypeDescriptor Element) : ValueTypeDescriptor
{
    public override string Kind => "list";
}
