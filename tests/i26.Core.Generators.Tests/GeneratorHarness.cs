using System.Collections.Immutable;
using i26.Core.Ids;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace i26.Core.Generators.Tests;

/// <summary>Drives the generator over source text, the way the compiler would.</summary>
/// <remarks>
/// A hand-rolled <see cref="CSharpGeneratorDriver"/> rather than the testing packages: what these
/// tests assert is exact messages, exact spans and whether a step was reused, and the driver says
/// all three without a layer in between.
/// </remarks>
internal static class GeneratorHarness
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Latest);

    /// <summary>Every assembly this test run loaded, which is more than enough to compile a probe.</summary>
    private static readonly ImmutableArray<MetadataReference> References = [
        ..((string)AppContext
            .GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        .Select(MetadataReference (path) => MetadataReference.CreateFromFile(path))
    ];

    internal static CSharpCompilation Compile(params (string Name, string Source)[] files) =>
        CSharpCompilation.Create(
            "Probe",
            files.Select(file => CSharpSyntaxTree.ParseText(file.Source, Parse, path: file.Name)),
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

    internal static GeneratorDriver Driver() =>
        CSharpGeneratorDriver.Create(
            [new TypedIdGenerator().AsSourceGenerator()],
            parseOptions: Parse,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

    /// <summary>Runs the generator once and reports everything it did.</summary>
    internal static Run Once(params (string Name, string Source)[] files)
    {
        var compilation = Compile(files);
        var driver = Driver().RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        return new Run(driver, output, driver.GetRunResult().Results[0]);
    }

    /// <summary>A file with one attributed id in it, in the <c>Probe</c> namespace.</summary>
    internal static (string Name, string Source) Id(
        string name,
        string prefix,
        string declaration = "public readonly partial record struct",
        string attributeArguments = "",
        string fileName = "") =>
        (fileName.Length == 0 ? $"{name}.cs" : fileName,
            $$"""
              using i26.Core.Ids;

              namespace Probe;

              [TypedId("{{prefix}}"{{attributeArguments}})]
              {{declaration}} {{name}};
              """);

    /// <summary>What one run of the generator produced.</summary>
    internal sealed record Run(GeneratorDriver Driver, Compilation Output, GeneratorRunResult Result)
    {
        /// <summary>What the generator itself reported, not what the compiler thought of the result.</summary>
        internal ImmutableArray<Diagnostic> Diagnostics => Result.Diagnostics;

        /// <summary>The ids of the diagnostics, in order.</summary>
        internal string[] Ids => [.. Diagnostics.Select(diagnostic => diagnostic.Id)];

        /// <summary>The files the generator wrote.</summary>
        internal ImmutableArray<GeneratedSourceResult> Sources => Result.GeneratedSources;

        /// <summary>Everything the generator wrote, as one string.</summary>
        internal string Text => string.Concat(Sources.Select(source => source.SourceText.ToString()));

        /// <summary>Errors the compiler found in the source plus everything generated.</summary>
        internal ImmutableArray<Diagnostic> CompilerErrors =>
            [.. Output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

        /// <summary>The single diagnostic, failing the test when there is not exactly one.</summary>
        internal Diagnostic Single() => Assert.Single(Diagnostics);

        /// <summary>The line the diagnostic points at, one-based, as an editor shows it.</summary>
        internal static int LineOf(Diagnostic diagnostic) =>
            diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;
    }

    /// <summary>Both prefix rules, read off the runtime library rather than restated here.</summary>
    internal static (int Max, int MaxExtended) RuntimeRules() =>
        (TypedIdPrefix.MaxLength, TypedIdPrefix.MaxExtendedLength);
}
