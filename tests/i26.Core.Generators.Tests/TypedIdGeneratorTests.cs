using Microsoft.CodeAnalysis;
using static i26.Core.Generators.Tests.GeneratorHarness;

namespace i26.Core.Generators.Tests;

public class TypedIdGeneratorTests
{
    [Fact]
    public void A_valid_id_generates_one_file_and_says_nothing()
    {
        var run = Once(Id("CourseId", "crs"));

        Assert.Empty(run.Diagnostics);
        Assert.Single(run.Sources);
        Assert.Empty(run.CompilerErrors);
    }

    [Fact]
    public void The_generated_file_is_named_after_the_full_type_name()
    {
        var run = Once(Id("CourseId", "crs"));

        Assert.Equal("Probe.CourseId.g.cs", run.Sources[0].HintName);
    }

    [Fact]
    public void The_generated_text_has_no_carriage_returns()
    {
        // Written on Windows, built on Linux: the same commit has to produce the same bytes, since
        // generated sources are embedded in the PDB.
        Assert.DoesNotContain('\r', Once(Id("CourseId", "crs")).Text);
    }

    [Fact]
    public void An_extended_prefix_is_declared_on_the_generated_id()
    {
        var run = Once(Id("WorkspaceId", "workspace", attributeArguments: ", UsesExtendedPrefix = true"));

        Assert.Empty(run.Diagnostics);
        Assert.Contains("public static bool UsesExtendedPrefix => true;", run.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public readonly partial record struct")]
    [InlineData("public partial record struct")]
    [InlineData("public readonly partial struct")]
    [InlineData("internal partial struct")]
    [InlineData("partial struct")]
    public void The_generated_part_matches_the_declaration_it_completes(string declaration)
    {
        var run = Once(Id("CourseId", "crs", declaration));

        Assert.Empty(run.Diagnostics);
        Assert.Empty(run.CompilerErrors);
    }

    // ---- the rules -------------------------------------------------------------------------

    [Fact]
    public void A_type_that_is_not_partial_is_refused()
    {
        var run = Once(Id("CourseId", "crs", "public readonly record struct"));

        Assert.Equal("I26ID001", run.Single().Id);
        Assert.Empty(run.Sources);
    }

    [Theory]
    [InlineData("", "is empty")]
    [InlineData("crse", "stops at 3")]
    [InlineData("CRS", "lowercase ASCII letters only")]
    [InlineData("cr1", "lowercase ASCII letters only")]
    [InlineData("cr_", "lowercase ASCII letters only")]
    public void A_prefix_that_breaks_a_rule_is_refused_and_the_message_says_which(string prefix, string says)
    {
        var run = Once(Id("CourseId", prefix));

        var diagnostic = run.Single();

        Assert.Equal("I26ID002", diagnostic.Id);
        Assert.Contains(says, diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void An_extended_prefix_past_ten_is_refused_too()
    {
        var run = Once(Id("WorkspaceId", "enrollments", attributeArguments: ", UsesExtendedPrefix = true"));

        Assert.Equal("I26ID002", run.Single().Id);
        Assert.Contains("stops at 10", run.Single().GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_complaint_about_the_prefix_lands_on_the_prefix()
    {
        // The attribute and the type name are on different lines, so the assertion means something.
        var run = Once(Id("CourseId", "crse"));

        Assert.Equal(5, Run.LineOf(run.Single()));
    }

    [Fact]
    public void A_control_character_in_the_prefix_does_not_reach_the_message_raw()
    {
        var run = Once(("Probe.cs",
            """
            using i26.Core.Ids;

            namespace Probe;

            [TypedId("a\tb")]
            public readonly partial record struct CourseId;
            """));

        var message = run.Single().GetMessage();

        Assert.DoesNotContain('\t', message);
        Assert.Contains("<U+0009>", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_nested_id_is_refused()
    {
        var run = Once(("Probe.cs",
            """
            using i26.Core.Ids;

            namespace Probe;

            public static class Holder
            {
                [TypedId("crs")]
                public readonly partial record struct CourseId;
            }
            """));

        Assert.Equal("I26ID004", run.Single().Id);
        Assert.Empty(run.Sources);
    }

    [Theory]
    [InlineData("public readonly partial record struct CourseId<T>;", "generic")]
    [InlineData("public ref partial struct CourseId;", "a ref struct")]
    [InlineData("file readonly partial record struct CourseId;", "file-local")]
    public void A_shape_the_generator_cannot_complete_is_refused(string declaration, string word)
    {
        var run = Once(("Probe.cs",
            $$"""
              using i26.Core.Ids;

              namespace Probe;

              [TypedId("crs")]
              {{declaration}}
              """));

        var diagnostic = run.Single();

        Assert.Equal("I26ID005", diagnostic.Id);
        Assert.Contains(word, diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void A_record_class_is_left_to_the_compiler()
    {
        // The attribute targets a struct, so the compiler already rejects this. Piling a generated
        // error on top of CS0592 helps nobody.
        var run = Once(("Probe.cs",
            """
            using i26.Core.Ids;

            namespace Probe;

            [TypedId("crs")]
            public partial record class CourseId;
            """));

        Assert.Empty(run.Diagnostics);
        Assert.Empty(run.Sources);
    }

    [Theory]
    [InlineData("(global::System.Guid Value)")]
    [InlineData("()")]
    public void A_primary_constructor_is_refused(string parameters)
    {
        var run = Once(("Probe.cs",
            $$"""
              using i26.Core.Ids;

              namespace Probe;

              [TypedId("crs")]
              public readonly partial record struct CourseId{{parameters}};
              """));

        Assert.Equal("I26ID006", run.Single().Id);
        Assert.Empty(run.Sources);
    }

    // ---- the rule no single declaration can answer -------------------------------------------

    [Fact]
    public void Two_ids_sharing_a_prefix_are_refused_and_both_are_named()
    {
        var run = Once(Id("CourseId", "crs"), Id("ClassroomId", "crs"));

        var diagnostic = run.Single();

        Assert.Equal("I26ID003", diagnostic.Id);
        Assert.Contains("CourseId", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("ClassroomId", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_collision_points_back_at_the_declaration_that_claimed_the_prefix_first()
    {
        var run = Once(Id("CourseId", "crs", fileName: "A.cs"), Id("ClassroomId", "crs", fileName: "B.cs"));

        var diagnostic = run.Single();

        // The path, not SourceTree: a location rebuilt from a file path and a span is an external
        // one, which is how the generator keeps the syntax tree out of the pipeline. The compiler
        // and the IDE navigate it the same way.
        Assert.Equal("B.cs", diagnostic.Location.GetLineSpan().Path);
        Assert.Equal("A.cs", Assert.Single(diagnostic.AdditionalLocations).GetLineSpan().Path);
    }

    [Fact]
    public void Both_sides_of_a_collision_still_get_their_members()
    {
        // The build fails either way, since I26ID003 is an error. Withholding the members buries it
        // under a CS1061 at every use site instead.
        var run = Once(Id("CourseId", "crs"), Id("ClassroomId", "crs"));

        Assert.Equal(2, run.Sources.Length);
    }

    [Fact]
    public void A_declaration_that_was_already_refused_does_not_claim_its_prefix()
    {
        // The nested one is out, so the top-level one owns 'crs' and there is no second complaint.
        var run = Once(
            Id("CourseId", "crs"),
            ("Nested.cs",
                """
                using i26.Core.Ids;

                namespace Probe;

                public static class Holder
                {
                    [TypedId("crs")]
                    public readonly partial record struct ClassroomId;
                }
                """));

        Assert.Equal("I26ID004", run.Single().Id);
    }

    // ---- the shapes that used to crash the generator ------------------------------------------

    [Fact]
    public void A_type_attributed_on_two_partial_parts_generates_once()
    {
        // Both parts carry the attribute, so the type arrives twice. Writing the same hint name
        // twice throws out of AddSource and discards every generated id in the compilation.
        var run = Once(
            ("A.cs",
                """
                using i26.Core.Ids;

                namespace Probe;

                [TypedId("crs")]
                public readonly partial record struct CourseId;
                """),
            ("B.cs",
                """
                using i26.Core.Ids;

                namespace Probe;

                [TypedId("crs")]
                public readonly partial record struct CourseId;
                """));

        Assert.Null(run.Result.Exception);
        Assert.Single(run.Sources);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void A_type_split_across_files_generates_from_the_part_carrying_the_attribute()
    {
        // The other part sorts first by file name, which is what a dedupe over declarations rather
        // than over attribute applications would trip on.
        var run = Once(
            ("CourseId.Json.cs",
                """
                namespace Probe;

                public readonly partial record struct CourseId;
                """),
            ("CourseId.cs",
                """
                using i26.Core.Ids;

                namespace Probe;

                [TypedId("crs")]
                public readonly partial record struct CourseId;
                """));

        Assert.Single(run.Sources);
        Assert.Empty(run.Diagnostics);
    }

    [Fact]
    public void A_keyword_name_is_escaped_in_the_code_and_stripped_from_the_file_name()
    {
        var run = Once(("Probe.cs",
            """
            using i26.Core.Ids;

            namespace Probe.@class;

            [TypedId("kwd")]
            public readonly partial record struct @record;
            """));

        Assert.Empty(run.CompilerErrors);
        Assert.Contains("partial record struct @record", run.Text, StringComparison.Ordinal);
        Assert.DoesNotContain('@', run.Sources[0].HintName);
    }

    [Fact]
    public void An_id_in_the_global_namespace_generates_without_one()
    {
        var run = Once(("Probe.cs",
            """
            using i26.Core.Ids;

            [TypedId("crs")]
            public readonly partial record struct CourseId;
            """));

        Assert.Empty(run.CompilerErrors);
        Assert.DoesNotContain("namespace", run.Text, StringComparison.Ordinal);
        Assert.Equal("CourseId.g.cs", run.Sources[0].HintName);
    }

    // ---- caching -----------------------------------------------------------------------------

    [Fact]
    public void A_comment_added_above_a_declaration_does_not_rewrite_it()
    {
        var before = Compile(Id("CourseId", "crs"));
        var driver = Driver().RunGenerators(before);

        var edited = before.ReplaceSyntaxTree(
            before.SyntaxTrees.Single(),
            Compile(("CourseId.cs",
                """
                using i26.Core.Ids;

                namespace Probe;

                // A comment that shifts every span below it.
                [TypedId("crs")]
                public readonly partial record struct CourseId;
                """)).SyntaxTrees.Single());

        var second = driver.RunGenerators(edited);

        Assert.All(
            second.GetRunResult().Results[0].TrackedSteps[TypedIdGenerator.ShapesNode]
                .SelectMany(step => step.Outputs),
            output => Assert.Contains(
                output.Reason,
                new[] { IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged }));
    }

    [Fact]
    public void An_edit_in_one_file_does_not_rewrite_the_id_in_another()
    {
        var before = Compile(Id("CourseId", "crs", fileName: "A.cs"), Id("StudentId", "std", fileName: "B.cs"));
        var driver = Driver().RunGenerators(before);

        var a = before.SyntaxTrees.Single(tree => tree.FilePath == "A.cs");

        var edited = before.ReplaceSyntaxTree(
            a,
            Compile(("A.cs",
                """
                using i26.Core.Ids;

                namespace Probe;

                [TypedId("crx")]
                public readonly partial record struct CourseId;
                """)).SyntaxTrees.Single());

        var second = driver.RunGenerators(edited);

        var reused = second.GetRunResult().Results[0].TrackedSteps[TypedIdGenerator.ShapesNode]
            .SelectMany(step => step.Outputs)
            .Count(output => output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);

        // B was untouched, so its shape is reused; A changed and is written again.
        Assert.Equal(1, reused);
    }
}
