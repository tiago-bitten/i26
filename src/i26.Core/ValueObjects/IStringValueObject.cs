using i26.Core.Results;

namespace i26.Core.ValueObjects;

/// <summary>A value object that is one string, checked before it exists.</summary>
/// <typeparam name="TSelf">The implementing type itself.</typeparam>
/// <remarks>
/// What lets one converter, one comparer and one <c>HasValueObject</c> serve every value object,
/// here and in the domain of whoever consumes this — the same trade
/// <see cref="Ids.ITypedId{TSelf}"/> makes for identifiers. Deriving from
/// <see cref="IParsable{TSelf}"/> is what makes a minimal API bind one from a route or a query with
/// no registration.
/// </remarks>
public interface IStringValueObject<TSelf> : IParsable<TSelf>
    where TSelf : class, IStringValueObject<TSelf>
{
    /// <summary>The longest the value may be, which is also the width of its column.</summary>
    static abstract int MaxLength { get; }

    /// <summary>Checks a value and answers with it, or with why not.</summary>
    static abstract Result<TSelf> Create(string? value);

    /// <summary>The value itself.</summary>
    string Value { get; }
}
