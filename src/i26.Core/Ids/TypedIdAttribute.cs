namespace i26.Core.Ids;

/// <summary>Declares a typed id and lets the generator write its members.</summary>
/// <param name="prefix">Up to three lowercase letters, checked while the project compiles.</param>
/// <remarks>
/// <code>
/// [TypedId("crs")]
/// public readonly partial record struct CourseId;
/// </code>
/// What comes out is the canonical hand-written id, so the two are interchangeable.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class TypedIdAttribute(string prefix) : Attribute
{
    /// <summary>The type's prefix, without the separator.</summary>
    public string Prefix { get; } = prefix;

    /// <summary>Allows a prefix of up to ten characters instead of three.</summary>
    public bool UsesExtendedPrefix { get; init; }
}
