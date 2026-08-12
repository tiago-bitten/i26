using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace i26.Core.Generators;

/// <summary>Writes the members of every <c>[TypedId]</c> struct.</summary>
/// <remarks>
/// The output is the canonical hand-written id, so the two are interchangeable. What the generator
/// adds is the checking: the prefix rules and the collision between two ids become compile errors.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class TypedIdGenerator : IIncrementalGenerator
{
    private const string AttributeName = "i26.Core.Ids.TypedIdAttribute";
    private const string ExtendedPrefixArgument = "UsesExtendedPrefix";
    private const string MintedArgument = "Minted";

    /// <summary>Longest prefix a typed id may have.</summary>
    /// <remarks>
    /// The same rule as <c>TypedIdPrefix</c> in i26.Core, restated because an analyzer targets
    /// netstandard2.0 and cannot reference the library it generates for. Held in step by
    /// <c>PrefixRuleTests</c>, which reads both through reflection.
    /// </remarks>
    internal const int MaxPrefixLength = 3;

    /// <summary>Longest prefix an id that opted in may have.</summary>
    internal const int MaxExtendedPrefixLength = 10;

    // Spelled in full so nothing a consumer declares can shadow them. Hoisting these into constants
    // would only move the text; the qualified names are what the generated file has to say.
    private const string GuidType = "global::System.Guid";
    private const string FormatProviderType = "global::System.IFormatProvider";
    private const string EquatableInterface = "global::System.IEquatable";
    private const string IdInterface = "global::i26.Core.Ids.ITypedId";
    private const string Runtime = "global::i26.Core.Ids.TypedId";

    /// <summary>Tracking names, so a test can assert the pipeline reused what it should have.</summary>
    internal const string ShapesNode = "Shapes";

    /// <summary>Tracking name of the per-declaration diagnostics node.</summary>
    internal const string SelfChecksNode = "SelfChecks";

    /// <summary>Tracking name of the batched prefix-claim node.</summary>
    internal const string ClaimsNode = "Claims";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var ids = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                predicate: static (node, _) => IsStructDeclaration(node),
                transform: static (attributed, token) => Describe(attributed, token))
            .Where(static id => id is not null)
            .Select(static (id, _) => id!);

        // Three nodes rather than one. Nothing about CourseId's generated text depends on StudentId
        // existing — only the duplicate-prefix rule does — so only that rule pays for the batch.

        // Per id, keyed on the shape alone: an edit anywhere else, including above this declaration,
        // leaves it equal and nothing is written again.
        context.RegisterSourceOutput(
            ids.Select(static (id, _) => id.Shape).WithTrackingName(ShapesNode),
            static (production, shape) =>
            {
                production.CancellationToken.ThrowIfCancellationRequested();

                if (shape.IsEmittable)
                {
                    production.AddSource(shape.HintName, SourceText.From(Write(shape), Encoding.UTF8));
                }
            });

        // Per id: the checks one declaration answers on its own.
        context.RegisterSourceOutput(
            ids.Select(static (id, _) => id.SelfCheck).WithTrackingName(SelfChecksNode),
            static (production, check) => ReportSelfCheck(production, check));

        // Whole compilation, but only the prefix claim travels and only diagnostics come out.
        context.RegisterSourceOutput(
            ids.Select(static (id, _) => id.Claim).Collect().WithTrackingName(ClaimsNode),
            static (production, claims) => ReportDuplicates(production, claims));
    }

    /// <summary>
    /// Struct-shaped syntax only. A <see cref="RecordDeclarationSyntax"/> is also a record class,
    /// which reaches here because the attribute's target is enforced by the compiler, not by this
    /// pipeline.
    /// </summary>
    private static bool IsStructDeclaration(SyntaxNode node) =>
        node is StructDeclarationSyntax
        || (node is RecordDeclarationSyntax record
            && record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword));

    /// <summary>Reads everything the generator needs off one attributed declaration.</summary>
    private static TypedId? Describe(GeneratorAttributeSyntaxContext attributed, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        // A record class still arrives here, and the compiler has already rejected it. Piling
        // generated errors onto code that will not compile anyway helps nobody.
        if (attributed.TargetSymbol is not INamedTypeSymbol type
            || type.TypeKind != TypeKind.Struct
            || attributed.TargetNode is not TypeDeclarationSyntax declaration)
        {
            return null;
        }

        var attribute = attributed.Attributes[0];

        // The attribute arrives once per declaration it is written on, so a type attributed on two
        // partial parts arrives twice. Writing the same file twice throws out of AddSource and
        // discards every generated id in the compilation.
        if (!SpeaksForTheType(type, attribute))
        {
            return null;
        }

        var prefix = (attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as string
            : null) ?? string.Empty;

        var extended = attribute.NamedArguments
            .FirstOrDefault(argument => argument.Key == ExtendedPrefixArgument)
            .Value.Value is true;

        // Absent means minted: the common id is the one this service creates, so only the exception
        // is written down.
        var minted = attribute.NamedArguments
            .FirstOrDefault(argument => argument.Key == MintedArgument)
            .Value.Value is not false;

        var containing = type.ContainingNamespace.IsGlobalNamespace
            ? null
            : type.ContainingNamespace.ToDisplayString();

        var parameters = FindParameterList(type, declaration, token);
        var complaint = Explain(prefix, extended);

        var isPartial = declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        var isNested = type.ContainingType is not null;
        var isGeneric = type.Arity > 0;

        // Settled once here rather than per run, and shared by the two nodes that need it: an id the
        // generator cannot write must not claim its prefix either, or a rejected declaration would
        // stack a spurious collision on top of its real error.
        var emittable = isPartial
                        && !isNested
                        && !isGeneric
                        && type is { IsFileLocal: false, IsRefLikeType: false }
                        && parameters is null
                        && complaint is null;

        var location = LocationInfo.From(declaration.Identifier.GetLocation());
        var prefixLocation = PrefixLocationOf(attribute, location, token);

        return new TypedId(
            new Shape(
                type.Name,
                containing,
                type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal",
                prefix,
                extended,
                minted,
                type.IsReadOnly,
                type.IsRecord,
                emittable),
            new SelfCheck(
                type.Name,
                prefix,
                isPartial,
                isNested,
                isGeneric,
                type.IsFileLocal,
                type.IsRefLikeType,
                complaint,
                location,
                prefixLocation,
                parameters is null ? null : LocationInfo.From(parameters.GetLocation())),
            new Claim(type.Name, containing, prefix, emittable, location, prefixLocation));
    }

    /// <summary>
    /// True when this is the one attributed part that speaks for the type. Ordering by file path
    /// rather than by the compilation's tree order keeps the choice stable as files come and go.
    /// </summary>
    private static bool SpeaksForTheType(INamedTypeSymbol type, AttributeData attribute)
    {
        var applications = type.GetAttributes()
            .Where(candidate => candidate.AttributeClass?.ToDisplayString() == AttributeName)
            .Select(candidate => candidate.ApplicationSyntaxReference)
            .Where(reference => reference is not null)
            .ToList();

        if (applications.Count < 2)
        {
            return true;
        }

        var first = applications
            .OrderBy(reference => reference!.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(reference => reference!.Span.Start)
            .First()!;

        var mine = attribute.ApplicationSyntaxReference;

        return mine is not null
            && mine.SyntaxTree.FilePath == first.SyntaxTree.FilePath
            && mine.Span.Start == first.Span.Start;
    }

    /// <summary>
    /// The primary constructor parameter list, wherever it was written: only one part may carry it,
    /// and it does not have to be the part carrying the attribute.
    /// </summary>
    private static ParameterListSyntax? FindParameterList(
        INamedTypeSymbol type,
        TypeDeclarationSyntax declaration,
        CancellationToken token)
    {
        if (declaration.ParameterList is not null || type.DeclaringSyntaxReferences.Length == 1)
        {
            return declaration.ParameterList;
        }

        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(token) is TypeDeclarationSyntax part && part.ParameterList is not null)
            {
                return part.ParameterList;
            }
        }

        return null;
    }

    /// <summary>
    /// Where the prefix was written, so a complaint about it lands on the string to edit rather than
    /// on the type name.
    /// </summary>
    private static LocationInfo PrefixLocationOf(
        AttributeData attribute,
        LocationInfo fallback,
        CancellationToken token)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax(token) is not AttributeSyntax syntax)
        {
            return fallback;
        }

        // The first argument that is not a property assignment: [TypedId(prefix: "crs")] is legal,
        // so a name colon still means the prefix.
        var argument = syntax.ArgumentList?.Arguments
            .FirstOrDefault(candidate => candidate.NameEquals is null);

        return LocationInfo.From(argument?.GetLocation() ?? syntax.GetLocation());
    }

    /// <summary>Reports what one declaration can be judged on without seeing any other.</summary>
    private static void ReportSelfCheck(SourceProductionContext production, SelfCheck check)
    {
        production.CancellationToken.ThrowIfCancellationRequested();

        // Shape first: a declaration the generator cannot write into at all gets one complaint
        // rather than a list of them.
        if (Unsupported(check) is { } shape)
        {
            production.ReportDiagnostic(Diagnostic.Create(
                TypedIdDiagnostics.UnsupportedShape, check.Location.ToLocation(), check.Name, shape));
            return;
        }

        if (check.IsNested)
        {
            production.ReportDiagnostic(Diagnostic.Create(
                TypedIdDiagnostics.Nested, check.Location.ToLocation(), check.Name));
            return;
        }

        if (!check.IsPartial)
        {
            production.ReportDiagnostic(Diagnostic.Create(
                TypedIdDiagnostics.NotPartial, check.Location.ToLocation(), check.Name));
            return;
        }

        if (check.ParameterListLocation is { } parameters)
        {
            production.ReportDiagnostic(Diagnostic.Create(
                TypedIdDiagnostics.PrimaryConstructor, parameters.ToLocation(), check.Name));
            return;
        }

        if (check.Complaint is { } complaint)
        {
            production.ReportDiagnostic(Diagnostic.Create(
                TypedIdDiagnostics.InvalidPrefix,
                check.PrefixLocation.ToLocation(),
                Printable(check.Prefix),
                check.Name,
                complaint));
        }
    }

    /// <summary>The word for a declaration shape the generator cannot write a partial part of.</summary>
    private static string? Unsupported(SelfCheck check) =>
        check.IsGeneric ? "generic"
            : check.IsRefLike ? "a ref struct"
            : check.IsFileLocal ? "file-local"
            : null;

    /// <summary>Settles the one rule no single declaration can answer: who owns a prefix.</summary>
    private static void ReportDuplicates(SourceProductionContext production, ImmutableArray<Claim> claims)
    {
        var owners = new Dictionary<string, Claim>(StringComparer.Ordinal);

        // Source order, so the error lands on the later declaration and points back at the first,
        // the way CS0101 does. Path and span are not a total order on their own, so namespace and
        // name settle the rest and keep the choice the same between builds.
        foreach (var claim in claims
                     .Where(claim => claim.IsValid)
                     .OrderBy(claim => claim.Location.FilePath, StringComparer.Ordinal)
                     .ThenBy(claim => claim.Location.Span.Start)
                     .ThenBy(claim => claim.Namespace ?? string.Empty, StringComparer.Ordinal)
                     .ThenBy(claim => claim.Name, StringComparer.Ordinal))
        {
            production.CancellationToken.ThrowIfCancellationRequested();

            if (owners.TryGetValue(claim.Prefix, out var owner))
            {
                production.ReportDiagnostic(Diagnostic.Create(
                    TypedIdDiagnostics.DuplicatePrefix,
                    claim.PrefixLocation.ToLocation(),
                    additionalLocations: [owner.PrefixLocation.ToLocation()],
                    messageArgs: [owner.Name, claim.Name, claim.Prefix]));
                continue;
            }

            owners.Add(claim.Prefix, claim);
        }
    }

    /// <summary>Says what is wrong with a prefix, or nothing when it holds up.</summary>
    private static string? Explain(string prefix, bool extended)
    {
        var maxLength = extended ? MaxExtendedPrefixLength : MaxPrefixLength;

        if (prefix.Length == 0)
        {
            return $"is empty. A prefix is one to {maxLength} lowercase ASCII letters";
        }

        if (prefix.Length > maxLength)
        {
            return extended
                ? $"is {prefix.Length} characters long, and even an extended prefix stops at {MaxExtendedPrefixLength}"
                : $"is {prefix.Length} characters long, and a prefix stops at {MaxPrefixLength}. " +
                  $"Set UsesExtendedPrefix on the attribute to allow up to {MaxExtendedPrefixLength}";
        }

        foreach (var character in prefix)
        {
            if (character is < 'a' or > 'z')
            {
                return $"holds {Printable(character)}. A prefix is lowercase ASCII letters only, so that an id " +
                       "has exactly one textual form and '_' stays unambiguous as the separator";
            }
        }

        return null;
    }

    /// <summary>Writes the id out, fully qualified so nothing in the file can be shadowed.</summary>
    private static string Write(Shape id)
    {
        var name = Escape(id.Name);
        var kind = id.IsRecord ? "record struct" : "struct";
        var modifiers = id.IsReadOnly ? $"{id.Accessibility} readonly partial" : $"{id.Accessibility} partial";

        // Every hole carries its own line breaks and indentation: a hole is inserted verbatim, not
        // re-indented, and when it is empty it has to leave no trace.
        var declaredNamespace = id.Namespace is null ? string.Empty : $"namespace {id.Namespace};\n\n";

        // A record struct is handed Equals, GetHashCode, == and != by the compiler. A plain struct is
        // not: it would fall back to ValueType.Equals, which boxes on every dictionary lookup, and
        // `left == right` would not compile at all.
        var equatable = id.IsRecord ? string.Empty : $", {EquatableInterface}<{name}>";

        var extendedPrefix = id.UsesExtendedPrefix
            ? "\n\n    /// <summary>Allows a prefix of up to ten characters instead of three.</summary>"
              + "\n    public static bool UsesExtendedPrefix => true;"
            : string.Empty;

        var minted = id.IsMinted
            ? string.Empty
            : "\n\n    /// <summary>Another service mints this prefix, so nothing here creates one.</summary>"
              + "\n    public static bool Minted => false;";

        var create = id.IsMinted
            ? $"\n\n    /// <summary>Creates a new {id.Name}.</summary>"
              + $"\n    public static {name} New() => {Runtime}.New<{name}>();"
            : string.Empty;

        var equality = id.IsRecord
            ? string.Empty
            : "\n\n    /// <summary>Tells whether the other id has the same value.</summary>"
              + $"\n    public bool Equals({name} other) => Value.Equals(other.Value);"
              + "\n\n    /// <summary>Tells whether the object is an id of this type with the same value.</summary>"
              + $"\n    public override bool Equals(object? obj) => obj is {name} other && Equals(other);"
              + "\n\n    /// <summary>A hash code over the id's value.</summary>"
              + "\n    public override int GetHashCode() => Value.GetHashCode();"
              + "\n\n    /// <summary>Tells whether two ids have the same value.</summary>"
              + $"\n    public static bool operator ==({name} left, {name} right) => left.Equals(right);"
              + "\n\n    /// <summary>Tells whether two ids have different values.</summary>"
              + $"\n    public static bool operator !=({name} left, {name} right) => !left.Equals(right);";

        var source = $$"""
            // <auto-generated/>
            #nullable enable

            {{declaredNamespace}}{{modifiers}} {{kind}} {{name}} : {{IdInterface}}<{{name}}>{{equatable}}
            {
                /// <summary>The prefix every {{id.Name}} carries.</summary>
                public static string Prefix => "{{id.Prefix}}";{{extendedPrefix}}{{minted}}

                /// <summary>Creates the id around the UUIDv7 it wraps.</summary>
                public {{name}}({{GuidType}} value) => Value = value;

                /// <summary>The UUIDv7 behind the id.</summary>
                public {{GuidType}} Value { get; init; }{{create}}

                /// <summary>Creates the id from the UUIDv7 it wraps.</summary>
                public static {{name}} FromGuid({{GuidType}} value) => new {{name}}(value);

                /// <summary>The id as {{id.Prefix}}_ followed by its encoded suffix.</summary>
                public override string ToString() => {{Runtime}}.Format(this);

                /// <summary>Reads an id back from its textual form.</summary>
                public static {{name}} Parse(string s, {{FormatProviderType}}? provider = null)
                    => {{Runtime}}.Parse<{{name}}>(s);

                /// <summary>Tries to read an id back from its textual form.</summary>
                public static bool TryParse(string? s, {{FormatProviderType}}? provider, out {{name}} result)
                    => {{Runtime}}.TryParse(s, out result);

                /// <summary>Orders this id against another by the bytes behind them, oldest first.</summary>
                public int CompareTo({{name}} other) => {{Runtime}}.Compare(this, other);

                /// <summary>Tells whether the left id sorts before the right one.</summary>
                public static bool operator <({{name}} left, {{name}} right) => {{Runtime}}.Compare(left, right) < 0;

                /// <summary>Tells whether the left id sorts before the right one, or is it.</summary>
                public static bool operator <=({{name}} left, {{name}} right) => {{Runtime}}.Compare(left, right) <= 0;

                /// <summary>Tells whether the left id sorts after the right one.</summary>
                public static bool operator >({{name}} left, {{name}} right) => {{Runtime}}.Compare(left, right) > 0;

                /// <summary>Tells whether the left id sorts after the right one, or is it.</summary>
                public static bool operator >=({{name}} left, {{name}} right) => {{Runtime}}.Compare(left, right) >= 0;{{equality}}
            }

            """;

        // A raw string literal carries the line endings of this file, and AppendLine carried the
        // build machine's. Neither is a decision, so the result is normalised and a Windows build
        // and a Linux build write the same bytes.
        return source.Replace("\r\n", "\n");
    }

    /// <summary>The identifier as it has to be written down: a keyword name carries its '@'.</summary>
    /// <remarks>
    /// The namespace arrives escaped already, because <c>ToDisplayString</c> does it;
    /// <c>INamedTypeSymbol.Name</c> does not, so a type named <c>record</c> would emit source that
    /// does not parse.
    /// </remarks>
    private static string Escape(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None
        && SyntaxFacts.GetContextualKeywordKind(identifier) == SyntaxKind.None
            ? identifier
            : "@" + identifier;

    /// <summary>
    /// A hint name is a file name to Roslyn, which rejects anything it would not open — and a
    /// namespace display string keeps the '@' of an escaped identifier.
    /// </summary>
    /// <remarks>
    /// Two names differing only in the characters being replaced would land on the same file, and a
    /// repeated hint name throws out of <c>AddSource</c> and discards every generated id in the
    /// compilation. So a name that had to be rewritten carries a fingerprint of the original.
    /// </remarks>
    private static string Sanitize(string value)
    {
        var safe = new StringBuilder(value.Length);
        var rewritten = false;

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
            {
                safe.Append(character);
            }
            else
            {
                safe.Append('_');
                rewritten = true;
            }
        }

        return rewritten ? $"{safe}_{Fingerprint(value)}" : safe.ToString();
    }

    /// <summary>A stable hash of a string as eight hex characters: FNV-1a, 32 bit.</summary>
    /// <remarks>
    /// Not <c>string.GetHashCode</c>, which is randomised per process — a hint name that changes
    /// between two builds of the same source is a file that appears twice in an incremental one.
    /// </remarks>
    private static string Fingerprint(string value)
    {
        unchecked
        {
            var hash = 2166136261u;

            foreach (var character in value)
            {
                hash = (hash ^ character) * 16777619u;
            }

            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>A character as it can safely be written into a diagnostic.</summary>
    private static string Printable(char character) =>
        character is >= ' ' and <= '~' ? $"'{character}'" : $"U+{(int)character:X4}";

    /// <summary>
    /// A prefix as it can safely be written into a diagnostic: a tab would be pasted straight into
    /// the message, and a line break would cut it in half in a build log.
    /// </summary>
    private static string Printable(string value)
    {
        var safe = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            safe.Append(character is >= ' ' and <= '~'
                ? character.ToString()
                : $"<U+{(int)character:X4}>");
        }

        return safe.ToString();
    }

    /// <summary>One typed id, projected into the three things the pipeline does with it.</summary>
    private sealed record TypedId(Shape Shape, SelfCheck SelfCheck, Claim Claim);

    /// <summary>
    /// Everything <see cref="Write"/> reads, and nothing else. No location, so a comment added above
    /// the declaration leaves this equal and the file is not written again.
    /// </summary>
    private sealed record Shape(
        string Name,
        string? Namespace,
        string Accessibility,
        string Prefix,
        bool UsesExtendedPrefix,
        bool IsMinted,
        bool IsReadOnly,
        bool IsRecord,
        bool IsEmittable)
    {
        internal string HintName => Sanitize(Namespace is null ? Name : $"{Namespace}.{Name}") + ".g.cs";
    }

    /// <summary>What one declaration can be judged on without seeing any other.</summary>
    private sealed record SelfCheck(
        string Name,
        string Prefix,
        bool IsPartial,
        bool IsNested,
        bool IsGeneric,
        bool IsFileLocal,
        bool IsRefLike,
        string? Complaint,
        LocationInfo Location,
        LocationInfo PrefixLocation,
        LocationInfo? ParameterListLocation);

    /// <summary>A claim on a prefix, which only the whole compilation can settle.</summary>
    private sealed record Claim(
        string Name,
        string? Namespace,
        string Prefix,
        bool IsValid,
        LocationInfo Location,
        LocationInfo PrefixLocation);

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
