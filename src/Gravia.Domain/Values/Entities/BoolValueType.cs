namespace Gravia.Domain.Values.Entities;

public sealed record BoolValueType : ValueTypeDescriptor
{
    public override string Kind => "bool";
}
