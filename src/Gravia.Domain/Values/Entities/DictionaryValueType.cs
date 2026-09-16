namespace Gravia.Domain.Values.Entities;

public sealed record DictionaryValueType(ValueTypeDescriptor Value) : ValueTypeDescriptor
{
    public override string Kind => "dictionary";
}
