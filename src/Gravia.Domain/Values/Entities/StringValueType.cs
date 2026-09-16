namespace Gravia.Domain.Values.Entities;

public sealed record StringValueType : ValueTypeDescriptor
{
    public override string Kind => "string";
}
