using i26.Core.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.ValueObjects;

/// <summary>Stores an <see cref="Email"/> as the text it is, and reads it back as one.</summary>
/// <remarks>
/// Reading goes through <see cref="Email.Parse"/>: a row holding something that is not an address
/// fails immediately rather than becoming an <c>Email</c> that never passed a check — the same
/// trade the typed ids make.
/// </remarks>
public sealed class EmailConverter : ValueConverter<Email, string>
{
    /// <summary>Creates the converter.</summary>
    public EmailConverter()
        : this(null)
    {
    }

    /// <summary>Creates the converter with mapping hints.</summary>
    /// <param name="mappingHints">Provider mapping hints, or <see langword="null"/>.</param>
    public EmailConverter(ConverterMappingHints? mappingHints)
        : base(
            email => email.Value,
            value => Email.Parse(value),
            mappingHints)
    {
    }
}
