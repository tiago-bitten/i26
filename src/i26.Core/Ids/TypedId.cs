using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace i26.Core.Ids;

/// <summary>
/// Creating, formatting, parsing and comparing typed ids. The types implementing
/// <see cref="ITypedId{TSelf}"/> delegate here.
/// </summary>
public static class TypedId
{
    /// <summary>Character separating the prefix from the encoded suffix.</summary>
    public const char Separator = '_';

    /// <summary>Creates a new id backed by a fresh UUIDv7.</summary>
    /// <exception cref="InvalidOperationException">
    /// The type declares <see cref="ITypedId{TSelf}.Minted"/> as <see langword="false"/>, so its
    /// prefix belongs to another service.
    /// </exception>
    public static TId New<TId>()
        where TId : struct, ITypedId<TId>
    {
        // Constant per TId once the JIT specialises this, so the check costs nothing at runtime and
        // the convention stops being one a caller can walk around by reaching for the generic form.
        if (!TId.Minted)
        {
            throw new InvalidOperationException(
                $"{typeof(TId).Name} declares Minted as false: the prefix '{TId.Prefix}' belongs to " +
                "another service, which is the only one that mints ids with it. Parse the id that " +
                "service handed over instead of creating one.");
        }

        return TId.FromGuid(Uuid7.New());
    }

    /// <summary>The id every bit of which is zero, which is also <c>default</c>.</summary>
    /// <remarks>
    /// It formats and parses like any other id, so nothing downstream rejects it. Treat it the way
    /// you would treat a null: a field nobody filled in.
    /// </remarks>
    public static TId Empty<TId>()
        where TId : struct, ITypedId<TId>
        => TId.FromGuid(Guid.Empty);

    /// <summary>Tells whether the id is the zero one — an id nobody assigned.</summary>
    public static bool IsEmpty<TId>(TId id)
        where TId : struct, ITypedId<TId>
        => id.Value == Guid.Empty;

    /// <summary>Formats the id as <c>{prefix}_{suffix}</c>, in one allocation.</summary>
    public static string Format<TId>(TId id)
        where TId : struct, ITypedId<TId>
    {
        var prefix = TypedIdPrefixCache<TId>.Value;

        return string.Create(
            GetFormattedLength(prefix),
            (Prefix: prefix, id.Value),
            static (destination, state) =>
            {
                state.Prefix.AsSpan().CopyTo(destination);
                destination[state.Prefix.Length] = Separator;

                Span<byte> bytes = stackalloc byte[CrockfordBase32.DecodedLength];
                state.Value.TryWriteBytes(bytes, bigEndian: true, out _);
                CrockfordBase32.Encode(bytes, destination[(state.Prefix.Length + 1)..]);
            });
    }

    /// <summary>Formats the id into a caller-owned buffer, allocating nothing.</summary>
    /// <returns><see langword="false"/> when it does not fit, having written nothing.</returns>
    public static bool TryFormat<TId>(TId id, Span<char> destination, out int charsWritten)
        where TId : struct, ITypedId<TId>
    {
        var prefix = TypedIdPrefixCache<TId>.Value;
        var length = GetFormattedLength(prefix);

        if (destination.Length < length)
        {
            charsWritten = 0;
            return false;
        }

        prefix.AsSpan().CopyTo(destination);
        destination[prefix.Length] = Separator;

        Span<byte> bytes = stackalloc byte[CrockfordBase32.DecodedLength];
        id.Value.TryWriteBytes(bytes, bigEndian: true, out _);
        CrockfordBase32.Encode(bytes, destination[(prefix.Length + 1)..]);

        charsWritten = length;
        return true;
    }

    /// <summary>Parses the textual form of an id.</summary>
    /// <exception cref="FormatException">
    /// The prefix, the length or the alphabet does not match. The message says what was expected.
    /// </exception>
    public static TId Parse<TId>(string? s)
        where TId : struct, ITypedId<TId>
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse<TId>(s.AsSpan());
    }

    /// <summary>Parses the textual form of an id.</summary>
    /// <exception cref="FormatException">
    /// The prefix, the length or the alphabet does not match. The message says what was expected.
    /// </exception>
    public static TId Parse<TId>(ReadOnlySpan<char> s)
        where TId : struct, ITypedId<TId>
    {
        if (!TryParse<TId>(s, out var result))
        {
            var prefix = TypedIdPrefixCache<TId>.Value;

            throw new FormatException(
                $"'{s.ToString()}' is not a valid {typeof(TId).Name}. Expected '{prefix}{Separator}' " +
                $"followed by {CrockfordBase32.EncodedLength} characters of the lowercase Crockford " +
                $"base32 alphabet ('{CrockfordBase32.Alphabet}'), {GetFormattedLength(prefix)} characters in total.");
        }

        return result;
    }

    /// <summary>Tries to parse the textual form of an id.</summary>
    public static bool TryParse<TId>([NotNullWhen(true)] string? s, out TId result)
        where TId : struct, ITypedId<TId>
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>Tries to parse the textual form of an id.</summary>
    public static bool TryParse<TId>(ReadOnlySpan<char> s, out TId result)
        where TId : struct, ITypedId<TId>
    {
        result = default;

        var prefix = TypedIdPrefixCache<TId>.Value;
        if (s.Length != GetFormattedLength(prefix))
        {
            return false;
        }

        // Ordinal comparison: the prefix is part of the type's identity, not user-facing text.
        if (!s[..prefix.Length].SequenceEqual(prefix.AsSpan()) || s[prefix.Length] != Separator)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[CrockfordBase32.DecodedLength];
        if (!CrockfordBase32.TryDecode(s[(prefix.Length + 1)..], bytes))
        {
            return false;
        }

        result = TId.FromGuid(new Guid(bytes, bigEndian: true));
        return true;
    }

    /// <summary>Compares two ids by the bytes behind them, oldest first.</summary>
    /// <remarks>
    /// Big-endian byte order, which matches an ordinal comparison of the formatted strings and the
    /// order of a <c>text COLLATE "C"</c> column. Not the order of
    /// <see cref="Guid.ToByteArray()"/>, which is little-endian for the first three fields. Two ids
    /// minted in the same millisecond are ordered by their random bits, not by which came first.
    /// </remarks>
    public static int Compare<TId>(TId left, TId right)
        where TId : struct, ITypedId<TId>
    {
        Span<byte> leftBytes = stackalloc byte[CrockfordBase32.DecodedLength];
        Span<byte> rightBytes = stackalloc byte[CrockfordBase32.DecodedLength];

        left.Value.TryWriteBytes(leftBytes, bigEndian: true, out _);
        right.Value.TryWriteBytes(rightBytes, bigEndian: true, out _);

        return leftBytes.SequenceCompareTo(rightBytes);
    }

    /// <summary>Reads the creation instant embedded in the id's UUIDv7.</summary>
    /// <exception cref="ArgumentException">
    /// The id does not wrap a version 7 UUID, or the instant is out of range.
    /// </exception>
    /// <remarks>
    /// Use <see cref="TryGetTimestamp{TId}"/> for an id that arrived from outside. Parsing checks
    /// the prefix and the alphabet, not the 128 bits, so a well-formed id can carry any of them —
    /// including bits that name no instant at all.
    /// </remarks>
    public static DateTimeOffset GetTimestamp<TId>(TId id)
        where TId : struct, ITypedId<TId>
        => Uuid7.GetTimestamp(id.Value);

    /// <summary>Tries to read the creation instant embedded in the id's UUIDv7.</summary>
    /// <returns>
    /// <see langword="false"/> when the id does not wrap a version 7 UUID, or when the instant it
    /// carries is out of the range a <see cref="DateTimeOffset"/> holds.
    /// </returns>
    public static bool TryGetTimestamp<TId>(TId id, out DateTimeOffset timestamp)
        where TId : struct, ITypedId<TId>
        => Uuid7.TryGetTimestamp(id.Value, out timestamp);

    /// <summary>Length of the textual form of an id with the given prefix.</summary>
    public static int GetFormattedLength(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return prefix.Length + 1 + CrockfordBase32.EncodedLength;
    }

    /// <summary>Tells whether a type is a typed id.</summary>
    public static bool IsTypedId(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!type.IsValueType || type.ContainsGenericParameters)
        {
            return false;
        }

        foreach (var candidate in type.GetInterfaces())
        {
            if (candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(ITypedId<>) &&
                candidate.GenericTypeArguments[0] == type)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds every typed id declared in the given assemblies, non-public ones included.</summary>
    /// <remarks>Setup-time reflection, for conventions and startup checks. Never on a request path.</remarks>
    [RequiresUnreferencedCode("Enumerates every type in the given assemblies, which trimming may have removed.")]
    public static IEnumerable<Type> FindTypedIds(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return Enumerate(assemblies);

        static IEnumerable<Type> Enumerate(Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                ArgumentNullException.ThrowIfNull(assembly);

                foreach (var type in LoadableTypes(assembly))
                {
                    if (IsTypedId(type))
                    {
                        yield return type;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The types of an assembly, skipping the ones that will not load.
    /// </summary>
    /// <remarks>
    /// An assembly with an optional dependency that is not installed throws out of
    /// <see cref="Assembly.GetTypes"/> and hands back the types it did load. This runs at startup,
    /// where letting that exception through would stop an application over a type no id lives in.
    /// </remarks>
    [RequiresUnreferencedCode("Enumerates every type in the assembly, which trimming may have removed.")]
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
