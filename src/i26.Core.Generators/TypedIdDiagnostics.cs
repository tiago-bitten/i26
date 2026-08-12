using Microsoft.CodeAnalysis;

namespace i26.Core.Generators;

/// <summary>What the generator refuses to write, and why.</summary>
/// <remarks>
/// Every title and message here is ASCII. They travel through build logs and terminals of unknown
/// encoding, and this file has already lost a character to a round trip through one.
/// </remarks>
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
        "A typed id prefix has to be one to three lowercase letters, or ten when extended",
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
        "'{0}' and '{1}' both declare the prefix '{2}'. A prefix names the entity, so sharing one makes '{2}_...' ambiguous.",
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

    /// <summary>A declaration shape the generator cannot write a partial part of.</summary>
    internal static readonly DiagnosticDescriptor UnsupportedShape = new(
        "I26ID005",
        "A typed id has to be a plain top-level struct",
        "'{0}' is {1}, so the members the generator writes would land on a different type. " +
        "Declare it as a plain struct, or write its members by hand.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The generator writes the constructor and Value, so the declaration must not.</summary>
    internal static readonly DiagnosticDescriptor PrimaryConstructor = new(
        "I26ID006",
        "A typed id must not declare a primary constructor",
        "'{0}' declares a primary constructor, and the generator writes one too. Drop the parameter " +
        "list: Value and the constructor come with the generated members.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
