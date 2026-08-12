using Microsoft.CodeAnalysis;

namespace i26.Core.Generators;

/// <summary>
/// What the generator refuses to write, and why.
/// </summary>
internal static class TypedIdDiagnostics
{
    private const string Category = "i26.Ids";

    /// <summary>The type has to be partial for the generator to add anything to it.</summary>
    internal static readonly DiagnosticDescriptor NotPartial = new(
        "I26ID001",
        "A typed id has to be partial",
        "'{0}' is marked with [TypedId] but is not partial, so the generator has nowhere to write its members",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The prefix breaks one of the rules, caught while compiling instead of at first use.</summary>
    internal static readonly DiagnosticDescriptor InvalidPrefix = new(
        "I26ID002",
        "A typed id prefix has to be one to three lowercase letters",
        "The prefix '{0}' of '{1}' {2}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Two entities claiming the same prefix, which no per-type check can catch and which makes an
    /// id ambiguous the moment it is written down.
    /// </summary>
    internal static readonly DiagnosticDescriptor DuplicatePrefix = new(
        "I26ID003",
        "Two typed ids cannot share a prefix",
        "'{0}' and '{1}' both declare the prefix '{2}'. A prefix names the entity, so sharing one makes '{2}_â€¦' ambiguous.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>A nested id would need every type around it to be partial as well.</summary>
    internal static readonly DiagnosticDescriptor Nested = new(
        "I26ID004",
        "A typed id cannot be nested",
        "'{0}' is declared inside another type. Move it out, or write its members by hand.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
