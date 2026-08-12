using System.Reflection;
using System.Runtime.ExceptionServices;

namespace i26.Core.Ids;

/// <summary>The rules a typed id prefix has to follow, and where they are checked.</summary>
/// <remarks>
/// Up to three lowercase letters, because the prefix is repeated in every id, log line and URL. The
/// check runs once per id type, the first time one is formatted or parsed.
/// </remarks>
public static class TypedIdPrefix
{
    /// <summary>Longest prefix an id may have.</summary>
    public const int MaxLength = 3;

    /// <summary>Longest prefix an id that opted into extended prefixes may have.</summary>
    public const int MaxExtendedLength = 10;

    /// <summary>Longest prefix allowed under one of the two rules.</summary>
    public static int MaxLengthFor(bool extended) => extended ? MaxExtendedLength : MaxLength;

    /// <summary>Tells whether a prefix follows the rules.</summary>
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
    /// <exception cref="InvalidOperationException">The prefix breaks one of the rules.</exception>
    /// <remarks>Remembered, so it costs one check per id type however often it runs.</remarks>
    public static string Validate<TId>()
        where TId : struct, ITypedId<TId>
        => TypedIdPrefixCache<TId>.Value;

    /// <summary>
    /// Checks every typed id in the given assemblies: each prefix follows the rules, and no two
    /// share one.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A prefix breaks a rule, or two ids declare the same one.
    /// </exception>
    /// <remarks>
    /// Uniqueness is the part no per-type check can catch, so one test in the project that declares
    /// the ids is worth having: <c>TypedIdPrefix.ValidateAll(typeof(CourseId).Assembly)</c>.
    /// </remarks>
    public static void ValidateAll(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        ValidateAll(TypedId.FindTypedIds(assemblies));
    }

    /// <summary>Checks the given typed ids: each prefix follows the rules, and no two share one.</summary>
    /// <exception cref="ArgumentException">One of the types is not a typed id.</exception>
    /// <exception cref="InvalidOperationException">
    /// A prefix breaks a rule, or two ids declare the same one.
    /// </exception>
    public static void ValidateAll(IEnumerable<Type> idTypes)
    {
        ArgumentNullException.ThrowIfNull(idTypes);

        var owners = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var idType in idTypes)
        {
            ArgumentNullException.ThrowIfNull(idType);

            if (!TypedId.IsTypedId(idType))
            {
                throw new ArgumentException(
                    $"{idType.Name} does not implement ITypedId<> with itself as the generic " +
                    "argument, so it has no prefix to check.",
                    nameof(idTypes));
            }

            var prefix = ValidateOf(idType);

            if (owners.TryGetValue(prefix, out var owner))
            {
                throw new InvalidOperationException(
                    $"{owner.Name} and {idType.Name} both declare the prefix '{prefix}'. A prefix " +
                    $"names the entity, so sharing one makes '{prefix}{TypedId.Separator}…' ambiguous: " +
                    "it no longer says which entity the id belongs to.");
            }

            owners.Add(prefix, idType);
        }
    }

    private static string ValidateOf(Type idType)
    {
        try
        {
            return (string)ValidateDefinition.MakeGenericMethod(idType).Invoke(null, null)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            // Surface what is wrong with the prefix, not the fact that reflection was involved.
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static readonly MethodInfo ValidateDefinition =
        typeof(TypedIdPrefix).GetMethod(nameof(Validate), genericParameterCount: 1, Type.EmptyTypes)!;

    /// <summary>Checks a prefix, and says what is wrong with it when it breaks a rule.</summary>
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

/// <summary>Holds the validated prefix of one id type, checked on first read.</summary>
/// <typeparam name="TId">The id type.</typeparam>
internal static class TypedIdPrefixCache<TId>
    where TId : struct, ITypedId<TId>
{
    private static string? _prefix;

    // Lazily rather than in a static initializer, so a broken prefix surfaces as the exception that
    // says what is wrong instead of a TypeInitializationException wrapping it. Two threads racing
    // here reach the same answer, so there is nothing to lock.
    internal static string Value =>
        _prefix ??= TypedIdPrefix.Validate(TId.Prefix, TId.UsesExtendedPrefix, typeof(TId));
}
