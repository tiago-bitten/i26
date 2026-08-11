namespace i26.Core.Ids;

/// <summary>
/// Contract of a strongly typed identifier in the TypeID format:
/// <c>{prefix}_{uuidv7 in Crockford base32}</c>, for example
/// <c>auth_01h455vb4pex5vsknk084sn02q</c>.
/// </summary>
/// <typeparam name="TSelf">The implementing type itself (CRTP pattern).</typeparam>
/// <remarks>
/// <para>
/// Every entity gets its own <c>readonly record struct</c> with the prefix baked into the type, so
/// ids of different entities are not interchangeable at compile time. The canonical implementation
/// delegates everything to the helpers on <see cref="TypedId"/>:
/// </para>
/// <code>
/// public readonly record struct UserId(Guid Value) : ITypedId&lt;UserId&gt;
/// {
///     public static string Prefix => "usr";
///     public static UserId FromGuid(Guid value) => new(value);
///     public static UserId New() => TypedId.New&lt;UserId&gt;();
///     public override string ToString() => TypedId.Format(this);
///     public static UserId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse&lt;UserId&gt;(s);
///     public static bool TryParse(string? s, IFormatProvider? _, out UserId result)
///         =&gt; TypedId.TryParse(s, out result);
/// }
/// </code>
/// <para>
/// Deriving from <see cref="IParsable{TSelf}"/> is what makes minimal API route and query string
/// binding work with no extra registration.
/// </para>
/// <para>
/// To reference an id owned by another microservice, declare a value object of your own carrying
/// that service's prefix and <em>no</em> <c>New()</c> method: only the service that owns a prefix
/// mints ids with it.
/// </para>
/// </remarks>
public interface ITypedId<TSelf> : IParsable<TSelf>
    where TSelf : struct, ITypedId<TSelf>
{
    /// <summary>
    /// The type's prefix, without the separator. Must be lowercase and must not contain <c>_</c>
    /// — for example <c>auth</c>, <c>usr</c>, <c>ord</c>.
    /// </summary>
    static abstract string Prefix { get; }

    /// <summary>Creates the id from the <see cref="Guid"/> it wraps.</summary>
    /// <param name="value">The UUIDv7 behind the id.</param>
    /// <returns>The matching typed id.</returns>
    static abstract TSelf FromGuid(Guid value);

    /// <summary>The UUIDv7 behind the id.</summary>
    Guid Value { get; }
}
