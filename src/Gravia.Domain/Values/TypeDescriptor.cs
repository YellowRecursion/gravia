using System.Text.Json.Serialization;
using Gravia.Domain.Values.Entities;

namespace Gravia.Domain.Values;

[JsonDerivedType(typeof(StringType))]
[JsonDerivedType(typeof(BoolType))]
[JsonDerivedType(typeof(IntType))]
[JsonDerivedType(typeof(DoubleType))]
[JsonDerivedType(typeof(ListType))]
[JsonDerivedType(typeof(DictionaryType))]
public abstract record TypeDescriptor
{
    public abstract string Kind { get; }
    public bool Nullable { get; init; }
}
