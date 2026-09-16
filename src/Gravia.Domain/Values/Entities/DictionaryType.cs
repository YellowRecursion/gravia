namespace Gravia.Domain.Values.Entities;

public sealed record DictionaryType(TypeDescriptor Value) : TypeDescriptor
{
    public override string Kind => "dictionary";
}
