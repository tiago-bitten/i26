using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace i26.Core.Ids.Json;

/// <summary>
/// Serializes every typed id as its prefixed string. One registration covers the ones that exist
/// and the ones added later.
/// </summary>
/// <remarks>
/// <c>options.Converters.Add(new TypedIdJsonConverterFactory())</c>. Reflection runs once per type,
/// when the options resolve its converter; serializing does not use it.
/// </remarks>
public sealed class TypedIdJsonConverterFactory : JsonConverterFactory
{
    /// <summary>Creates the factory.</summary>
    [RequiresDynamicCode("Builds a converter for each id type at runtime.")]
    public TypedIdJsonConverterFactory()
    {
    }

    /// <summary>Tells whether the type is a typed id.</summary>
    public override bool CanConvert(Type typeToConvert) => TypedId.IsTypedId(typeToConvert);

    /// <summary>Creates the converter for the given typed id.</summary>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(TypedIdJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    /// <summary>
    /// Converter for one specific typed id: reads and writes the textual <c>{prefix}_{suffix}</c>
    /// form, including when the id is used as a dictionary key.
    /// </summary>
    /// <typeparam name="TId">The id type.</typeparam>
    private sealed class TypedIdJsonConverter<TId> : JsonConverter<TId>
        where TId : struct, ITypedId<TId>
    {
        /// <summary>
        /// Stack buffer used when writing. Prefixes are short by convention; should an id ever
        /// exceed this size, writing falls back to the path that allocates the string.
        /// </summary>
        private const int StackBufferLength = 64;

        public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException(
                    $"Expected a string value for {typeof(TId).Name}, but found {reader.TokenType}.");
            }

            return ReadValue(ref reader);
        }

        public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            Span<char> buffer = stackalloc char[StackBufferLength];
            if (TypedId.TryFormat(value, buffer, out var written))
            {
                writer.WriteStringValue(buffer[..written]);
            }
            else
            {
                writer.WriteStringValue(TypedId.Format(value));
            }
        }

        public override TId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => ReadValue(ref reader);

        public override void WriteAsPropertyName(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            Span<char> buffer = stackalloc char[StackBufferLength];
            if (TypedId.TryFormat(value, buffer, out var written))
            {
                writer.WritePropertyName(buffer[..written]);
            }
            else
            {
                writer.WritePropertyName(TypedId.Format(value));
            }
        }

        private static TId ReadValue(ref Utf8JsonReader reader)
        {
            var text = reader.GetString();

            if (!TypedId.TryParse<TId>(text, out var result))
            {
                throw new JsonException(
                    $"'{text}' is not a valid {typeof(TId).Name}. Expected '{TId.Prefix}{TypedId.Separator}' " +
                    $"followed by {CrockfordBase32.EncodedLength} lowercase Crockford base32 characters.");
            }

            return result;
        }
    }
}
