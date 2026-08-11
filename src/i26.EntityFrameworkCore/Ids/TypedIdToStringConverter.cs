using i26.Core.Ids;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.Ids;

/// <summary>
/// Converts a typed id to its full textual form — prefix included — on write, and back on read.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
/// <remarks>
/// What lands in the database is exactly what shows up in the API and in the logs, which makes an
/// id searchable and copy-pasteable across all three without translation. Reading goes through
/// <see cref="TypedId.Parse{TId}(string?)"/> on purpose: corrupted data, or data carrying another
/// entity's prefix, fails immediately instead of quietly becoming the wrong id.
/// </remarks>
public sealed class TypedIdToStringConverter<TId> : ValueConverter<TId, string>
    where TId : struct, ITypedId<TId>
{
    /// <summary>Creates the converter.</summary>
    public TypedIdToStringConverter()
        : this(null)
    {
    }

    /// <summary>Creates the converter with mapping hints.</summary>
    /// <param name="mappingHints">Provider mapping hints, or <see langword="null"/>.</param>
    public TypedIdToStringConverter(ConverterMappingHints? mappingHints)
        : base(
            id => TypedId.Format(id),
            value => TypedId.Parse<TId>(value),
            mappingHints)
    {
    }
}
