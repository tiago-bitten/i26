using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace i26.Core.Generators;

/// <summary>
/// Writes the members of every <c>[TypedId]</c> struct.
/// </summary>
/// <remarks>
/// The output is the canonical hand-written id, character for character, so a generated id and a
/// hand-written one are interchangeable. What the generator adds on top is the checking: the prefix
/// rules and the collision between two ids become compile errors, where a hand-written id would
/// only find out at first use, or in a test, or never.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class TypedIdGenerator : IIncrementalGenerator
{
    private const string AttributeName = "i26.Core.Ids.TypedIdAttribute";
    private const string ExtendedPrefixArgument = "UsesExtendedPrefix";

    /// <summary>Longest prefix a typed id may have.</summary>
    /// <remarks>
    /// The same rule as <c>TypedIdPrefix</c> in i26.Core, restated because an analyzer targets
    /// netstandard2.0 and cannot reference the library it generates for. The two are kept in step
    /// by the tests.
    /// </remarks>
    private const int MaxPrefixLength = 3;

    /// <summary>Longest prefix an id that opted in may have.</summary>
    private const int MaxExtendedPrefixLength = 10;

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var ids = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                // A record struct is a RecordDeclarationSyntax, not a StructDeclarationSyntax, and
                // both are TypeDeclarationSyntax. The attribute already restricts the target to a
                // struct, so nothing else can reach here.
                predicate: static (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (attributed, _) => Describe(attributed))
            .Where(static id => id is not null)
            .Select(static (id, _) => id!)
            .Collect();

        context.RegisterSourceOutput(ids, static (production, all) => Emit(production, all));
    }

    /// <summary>Reads everything the generator needs off one attributed declaration.</summary>
    private static TypedId? Describe(GeneratorAttributeSyntaxContext attributed)
    {
        if (attributed.TargetSymbol is not INamedTypeSymbol type)
        {
            return null;
        }

        var attribute = attributed.Attributes[0];

        var prefix = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as string
            : null;

        var extended = attribute.NamedArguments
            .FirstOrDefault(argument => argument.Key == ExtendedPrefixArgument)
            .Value.Value is true;

        if (attributed.TargetNode is not TypeDeclarationSyntax declaration)
        {
            return null;
        }

        return new TypedId(
            type.Name,
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal",
            prefix ?? string.Empty,
            extended,
            declaration.Modifiers.Any(modifier => modifier.ValueText == "partial"),
            type.IsReadOnly,
            type.IsRecord,
            type.ContainingType is not null,
            LocationInfo.From(declaration.Identifier.GetLocation()));
    }

    /// <summary>Checks the whole set, then writes the ones that hold up.</summary>
    private static void Emit(SourceProductionContext production, ImmutableArray<TypedId> ids)
    {
        var owners = new Dictionary<string, TypedId>(StringComparer.Ordinal);

        // Sorted so a collision always lands on the same declaration. Left in the order the
        // compilation happened to hand them over, the error would move between builds.
        foreach (var id in ids.OrderBy(id => id.Namespace ?? string.Empty, StringComparer.Ordinal)
                     .ThenBy(id => id.Name, StringComparer.Ordinal))
        {
            if (id.IsNested)
            {
                production.ReportDiagnostic(Diagnostic.Create(
                    TypedIdDiagnostics.Nested, id.Location.ToLocation(), id.Name));
                continue;
            }

            if (!id.IsPartial)
            {
                production.ReportDiagnostic(Diagnostic.Create(
                    TypedIdDiagnostics.NotPartial, id.Location.ToLocation(), id.Name));
                continue;
            }

            if (Explain(id) is { } complaint)
            {
                production.ReportDiagnostic(Diagnostic.Create(
                    TypedIdDiagnostics.InvalidPrefix, id.Location.ToLocation(), id.Prefix, id.Name, complaint));
                continue;
            }

            // A prefix names the entity, so two of them naming the same one is never intended.
            if (owners.TryGetValue(id.Prefix, out var owner))
            {
                production.ReportDiagnostic(Diagnostic.Create(
                    TypedIdDiagnostics.DuplicatePrefix, id.Location.ToLocation(), owner.Name, id.Name, id.Prefix));
                continue;
            }

            owners.Add(id.Prefix, id);

            production.AddSource(id.HintName, SourceText.From(Write(id), Encoding.UTF8));
        }
    }

    /// <summary>Says what is wrong with a prefix, or nothing when it holds up.</summary>
    private static string? Explain(TypedId id)
    {
        var maxLength = id.UsesExtendedPrefix ? MaxExtendedPrefixLength : MaxPrefixLength;

        if (id.Prefix.Length == 0)
        {
            return $"is empty. A prefix is one to {maxLength} lowercase ASCII letters";
        }

        if (id.Prefix.Length > maxLength)
        {
            return id.UsesExtendedPrefix
                ? $"is {id.Prefix.Length} characters long, and even an extended prefix stops at {MaxExtendedPrefixLength}"
                : $"is {id.Prefix.Length} characters long, and a prefix stops at {MaxPrefixLength}. " +
                  "Set UsesExtendedPrefix on the attribute to allow up to " + MaxExtendedPrefixLength;
        }

        foreach (var character in id.Prefix)
        {
            if (character is < 'a' or > 'z')
            {
                return $"holds '{character}'. A prefix is lowercase ASCII letters only, so that an id " +
                       "has exactly one textual form and '_' stays unambiguous as the separator";
            }
        }

        return null;
    }

    /// <summary>Writes the id out, fully qualified so nothing in the file can be shadowed.</summary>
    private static string Write(TypedId id)
    {
        var source = new StringBuilder();
        var indent = id.Namespace is null ? string.Empty : "    ";

        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();

        if (id.Namespace is not null)
        {
            source.AppendLine($"namespace {id.Namespace}");
            source.AppendLine("{");
        }

        var kind = id.IsRecord ? "record struct" : "struct";
        var modifiers = id.IsReadOnly ? $"{id.Accessibility} readonly partial" : $"{id.Accessibility} partial";

        source.AppendLine($"{indent}{modifiers} {kind} {id.Name} : global::i26.Core.Ids.ITypedId<{id.Name}>");
        source.AppendLine($"{indent}{{");
        source.AppendLine($"{indent}    /// <summary>The prefix every {id.Name} carries.</summary>");
        source.AppendLine($"{indent}    public static string Prefix => \"{id.Prefix}\";");

        if (id.UsesExtendedPrefix)
        {
            source.AppendLine();
            source.AppendLine($"{indent}    /// <summary>This id carries a prefix longer than three characters.</summary>");
            source.AppendLine($"{indent}    public static bool UsesExtendedPrefix => true;");
        }

        source.AppendLine();
        source.AppendLine($"{indent}    /// <summary>Creates the id around the UUIDv7 it wraps.</summary>");
        source.AppendLine($"{indent}    public {id.Name}(global::System.Guid value) => Value = value;");
        source.AppendLine();
        source.AppendLine($"{indent}    /// <summary>The UUIDv7 behind the id.</summary>");
        source.AppendLine($"{indent}    public global::System.Guid Value {{ get; init; }}");
        source.AppendLine();
        source.AppendLine($"{indent}    /// <summary>Creates a new {id.Name}.</summary>");
        source.AppendLine($"{indent}    public static {id.Name} New() => global::i26.Core.Ids.TypedId.New<{id.Name}>();");
        source.AppendLine();
        source.AppendLine($"{indent}    /// <summary>Creates the id from the UUIDv7 it wraps.</summary>");
        source.AppendLine($"{indent}    public static {id.Name} FromGuid(global::System.Guid value) => new {id.Name}(value);");
        source.AppendLine();
        source.AppendLine($"{indent}    /// <summary>The id as {id.Prefix}_ followed by its encoded suffix.</summary>");
        source.AppendLine($"{indent}    public override string ToString() => global::i26.Core.Ids.TypedId.Format(this);");
        source.AppendLine();
        source.AppendLine($"{indent}    /// <summary>Reads an id back from its textual form.</summary>");
        source.AppendLine($"{indent}    public static {id.Name} Parse(string s, global::System.IFormatProvider? provider = null)");
        source.AppendLine($"{indent}        => global::i26.Core.Ids.TypedId.Parse<{id.Name}>(s);");
        source.AppendLine();
        source.AppendLine($"{indent}    /// <summary>Tries to read an id back from its textual form.</summary>");
        source.AppendLine($"{indent}    public static bool TryParse(string? s, global::System.IFormatProvider? provider, out {id.Name} result)");
        source.AppendLine($"{indent}        => global::i26.Core.Ids.TypedId.TryParse(s, out result);");
        source.AppendLine($"{indent}}}");

        if (id.Namespace is not null)
        {
            source.AppendLine("}");
        }

        return source.ToString();
    }

    /// <summary>One attributed declaration, reduced to what the generator needs and can compare.</summary>
    private sealed record TypedId(
        string Name,
        string? Namespace,
        string Accessibility,
        string Prefix,
        bool UsesExtendedPrefix,
        bool IsPartial,
        bool IsReadOnly,
        bool IsRecord,
        bool IsNested,
        LocationInfo Location)
    {
        internal string HintName => Namespace is null ? $"{Name}.g.cs" : $"{Namespace}.{Name}.g.cs";
    }

    /// <summary>
    /// A location the pipeline can compare. <see cref="Location"/> itself holds on to the syntax
    /// tree, which would keep every generation from being reused.
    /// </summary>
    private sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
    {
        internal static LocationInfo From(Location location) =>
            new(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);

        internal Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
    }
}
