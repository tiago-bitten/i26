namespace i26.Core.Ids;

/// <summary>
/// A strongly typed identifier in the TypeID format: <c>{prefix}_{uuidv7 in Crockford base32}</c>.
/// </summary>
/// <typeparam name="TSelf">The implementing type itself.</typeparam>
/// <remarks>
/// Declare one with <see cref="TypedIdAttribute"/> and the generator writes the members. Deriving
/// from <see cref="IParsable{TSelf}"/> is what makes minimal API route and query binding work with
/// no registration.
/// </remarks>
public interface ITypedId<TSelf> : IParsable<TSelf>
    where TSelf : struct, ITypedId<TSelf>
{
    /// <summary>The type's prefix, without the separator: up to three lowercase letters.</summary>
    /// <remarks>Checked once per id type, the first time one is formatted or parsed.</remarks>
    static abstract string Prefix { get; }

    /// <summary>Allows a prefix of up to ten characters instead of three.</summary>
    static virtual bool UsesExtendedPrefix => false;

    /// <summary>Creates the id from the UUIDv7 it wraps.</summary>
    static abstract TSelf FromGuid(Guid value);

    /// <summary>The UUIDv7 behind the id.</summary>
    Guid Value { get; }
}
