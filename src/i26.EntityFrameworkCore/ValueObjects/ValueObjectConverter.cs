using i26.Core.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.ValueObjects;

/// <summary>Stores a value object as the string it is, and reads it back as one.</summary>
/// <typeparam name="TValue">The value object.</typeparam>
/// <remarks>
/// Reading goes through <c>Parse</c>: a row holding something the type would have refused fails
/// immediately rather than becoming a value that never passed a check — the same trade the typed
/// ids make.
/// </remarks>
public class ValueObjectConverter<TValue> : ValueConverter<TValue, string>
    where TValue : class, IStringValueObject<TValue>
{
    /// <summary>Creates the converter.</summary>
    public ValueObjectConverter()
        : this(null)
    {
    }

    /// <summary>Creates the converter with mapping hints.</summary>
    /// <param name="mappingHints">Provider mapping hints, or <see langword="null"/>.</param>
    public ValueObjectConverter(ConverterMappingHints? mappingHints)
        : base(value => value.Value, stored => Read(stored), mappingHints)
    {
    }

    // Through a method of this class rather than TValue.Parse in the lambda: an expression tree
    // cannot hold a call to a static abstract interface member, and this is an expression tree.
    private static TValue Read(string stored) => TValue.Parse(stored, provider: null);
}
