using System.Text.Json.Serialization;
using Gravia.Domain.Values.Entities;

namespace Gravia.Domain.Values;

[JsonDerivedType(typeof(StringValueType))]
[JsonDerivedType(typeof(BoolValueType))]
[JsonDerivedType(typeof(IntValueType))]
[JsonDerivedType(typeof(DoubleValueType))]
[JsonDerivedType(typeof(ListValueType))]
[JsonDerivedType(typeof(DictionaryValueType))]
public abstract record ValueTypeDescriptor
{
    public abstract string Kind { get; }
    public bool Nullable { get; init; }
}
