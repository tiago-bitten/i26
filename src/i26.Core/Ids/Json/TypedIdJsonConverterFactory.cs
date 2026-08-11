using System.Text.Json;
using System.Text.Json.Serialization;

namespace i26.Core.Ids.Json;

/// <summary>
/// JSON converter factory for any type implementing <see cref="ITypedId{TSelf}"/>. A single
/// registration covers every typed id that exists today and every one added later.
/// </summary>
/// <remarks>
/// <para>Registration:</para>
/// <code>
/// options.Converters.Add(new TypedIdJsonConverterFactory());
/// </code>
/// <para>
/// Reflection only runs when the <see cref="JsonSerializerOptions"/> resolves the converter for a
/// type (once per type, cached by System.Text.Json itself); serialization does not use reflection.
/// </para>
/// </remarks>
public sealed class TypedIdJsonConverterFactory : JsonConverterFactory
{
    /// <summary>
    /// Tells whether the type is a struct implementing <see cref="ITypedId{TSelf}"/> with itself as
    /// the generic argument.
    /// </summary>
    /// <param name="typeToConvert">The candidate type.</param>
    /// <returns><see langword="true"/> when it is a typed id.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        if (!typeToConvert.IsValueType)
        {
            return false;
        }

        foreach (var candidate in typeToConvert.GetInterfaces())
        {
            if (candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(ITypedId<>) &&
                candidate.GenericTypeArguments[0] == typeToConvert)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Creates the converter for the given typed id.</summary>
    /// <param name="typeToConvert">The id type.</param>
    /// <param name="options">The serializer options in use.</param>
    /// <returns>The matching converter.</returns>
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
