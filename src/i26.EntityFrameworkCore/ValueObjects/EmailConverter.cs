using i26.Core.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.ValueObjects;

/// <summary>Stores an <see cref="Email"/> as the text it is.</summary>
/// <remarks>The named form of <see cref="ValueObjectConverter{TValue}"/>, for a property that says so.</remarks>
public sealed class EmailConverter : ValueObjectConverter<Email>
{
    /// <summary>Creates the converter.</summary>
    public EmailConverter()
    {
    }

    /// <summary>Creates the converter with mapping hints.</summary>
    /// <param name="mappingHints">Provider mapping hints, or <see langword="null"/>.</param>
    public EmailConverter(ConverterMappingHints? mappingHints)
        : base(mappingHints)
    {
    }
}
