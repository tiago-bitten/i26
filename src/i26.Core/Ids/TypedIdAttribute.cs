namespace i26.Core.Ids;

/// <summary>
/// Declares a typed id, and lets the generator write the rest of it.
/// </summary>
/// <param name="prefix">
/// The type's prefix: up to three lowercase letters, checked while the project compiles.
/// </param>
/// <remarks>
/// <para>
/// The members of a typed id never vary — only the name and three letters do. Written by hand they
/// are eleven lines per entity, and every one of them is a chance to copy the neighbouring id and
/// forget to change the prefix. With the attribute the declaration is the whole thing:
/// </para>
/// <code>
/// [TypedId("crs")]
/// public readonly partial record struct CourseId;
/// </code>
/// <para>
/// What comes out is the same shape the canonical hand-written id has —
/// <see cref="ITypedId{TSelf}"/> and its members, <c>New</c>, <c>ToString</c>, <c>Parse</c> and
/// <c>TryParse</c> — so the two are interchangeable and nothing depends on which one was used.
/// </para>
/// <para>
/// The prefix rules become compile errors instead of runtime ones, and two entities declaring the
/// same prefix stop the build rather than making an id ambiguous in production.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class TypedIdAttribute(string prefix) : Attribute
{
    /// <summary>The type's prefix, without the separator.</summary>
    public string Prefix { get; } = prefix;

    /// <summary>
    /// Set to <see langword="true"/> to allow a prefix longer than
    /// <see cref="TypedIdPrefix.MaxLength"/>, up to <see cref="TypedIdPrefix.MaxExtendedLength"/>.
    /// </summary>
    /// <remarks>
    /// Emitted as <see cref="ITypedId{TSelf}.UsesExtendedPrefix"/>, so the generated id says out
    /// loud what a hand-written one would have to.
    /// </remarks>
    public bool UsesExtendedPrefix { get; init; }
}
