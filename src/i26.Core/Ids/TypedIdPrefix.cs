namespace i26.Core.Ids;

/// <summary>
/// The rules a typed id prefix has to follow, and where they are checked.
/// </summary>
/// <remarks>
/// <para>
/// A prefix is up to three lowercase letters — <c>usr</c>, <c>ord</c>, <c>crs</c>. Short is the
/// point: the prefix is repeated in every id, every log line and every URL, and three characters
/// are enough to tell entities apart at a glance. Ids that need more say so out loud:
/// </para>
/// <code>
/// public static string Prefix => "workspace";
/// public static bool UsesExtendedPrefix => true;   // up to ten
/// </code>
/// <para>
/// The check runs once per id type, the first time something formats or parses one of them. Call
/// <see cref="Validate{TId}"/> at startup, or from a test, to find a bad prefix before a request
/// does.
/// </para>
/// </remarks>
public static class TypedIdPrefix
{
    /// <summary>Longest prefix an id may have.</summary>
    public const int MaxLength = 3;

    /// <summary>
    /// Longest prefix an id that sets <see cref="ITypedId{TSelf}.UsesExtendedPrefix"/> may have.
    /// </summary>
    public const int MaxExtendedLength = 10;

    /// <summary>Longest prefix allowed under one of the two rules.</summary>
    /// <param name="extended">Whether the id opted into extended prefixes.</param>
    /// <returns><see cref="MaxExtendedLength"/> when it did, <see cref="MaxLength"/> otherwise.</returns>
    public static int MaxLengthFor(bool extended) => extended ? MaxExtendedLength : MaxLength;

    /// <summary>Tells whether a prefix follows the rules.</summary>
    /// <param name="prefix">The prefix to check.</param>
    /// <param name="extended">Whether the id opted into extended prefixes.</param>
    /// <returns>
    /// <see langword="true"/> when it is one to <see cref="MaxLengthFor"/> lowercase ASCII letters.
    /// </returns>
    public static bool IsValid(string? prefix, bool extended = false)
    {
        if (prefix is null || prefix.Length == 0 || prefix.Length > MaxLengthFor(extended))
        {
            return false;
        }

        foreach (var character in prefix)
        {
            if (character is < 'a' or > 'z')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks the prefix of an id type, throwing when it breaks a rule.</summary>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <returns>The prefix.</returns>
    /// <exception cref="InvalidOperationException">The prefix breaks one of the rules.</exception>
    /// <remarks>
    /// The result is remembered, so this costs one check per id type no matter how often it runs.
    /// </remarks>
    public static string Validate<TId>()
        where TId : struct, ITypedId<TId>
        => TypedIdPrefixCache<TId>.Value;

    /// <summary>Builds the message explaining what is wrong with a prefix.</summary>
    internal static string Validate(string? prefix, bool extended, Type idType)
    {
        if (prefix is null || prefix.Length == 0)
        {
            throw new InvalidOperationException(
                $"{idType.Name} declares an empty prefix. A typed id prefix is one to " +
                $"{MaxLengthFor(extended)} lowercase ASCII letters.");
        }

        var maxLength = MaxLengthFor(extended);

        if (prefix.Length > maxLength)
        {
            var hint = extended
                ? $"{nameof(MaxExtendedLength)} is {MaxExtendedLength}, and this is the extended rule already."
                : $"Declare 'public static bool UsesExtendedPrefix => true;' on {idType.Name} to allow " +
                  $"up to {MaxExtendedLength}.";

            throw new InvalidOperationException(
                $"The prefix '{prefix}' of {idType.Name} is {prefix.Length} characters long, and a typed " +
                $"id prefix is at most {maxLength}. {hint}");
        }

        foreach (var character in prefix)
        {
            if (character is < 'a' or > 'z')
            {
                throw new InvalidOperationException(
                    $"The prefix '{prefix}' of {idType.Name} contains '{character}'. A typed id prefix is " +
                    "lowercase ASCII letters only, so that an id has exactly one textual form and the " +
                    $"'{TypedId.Separator}' stays unambiguous as the separator.");
            }
        }

        return prefix;
    }
}

/// <summary>
/// Holds the validated prefix of one id type. Generic statics give one slot per closed type, which
/// is what makes the check run once and stay out of the way afterwards.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
internal static class TypedIdPrefixCache<TId>
    where TId : struct, ITypedId<TId>
{
    private static string? _prefix;

    /// <summary>The prefix of <typeparamref name="TId"/>, checked on first read.</summary>
    /// <remarks>
    /// Validated lazily rather than in a static initializer on purpose: a broken prefix surfaces as
    /// the <see cref="InvalidOperationException"/> that says what is wrong, not wrapped in a
    /// <see cref="TypeInitializationException"/>. Two threads racing here both reach the same
    /// answer, so there is nothing to lock.
    /// </remarks>
    internal static string Value =>
        _prefix ??= TypedIdPrefix.Validate(TId.Prefix, TId.UsesExtendedPrefix, typeof(TId));
}
