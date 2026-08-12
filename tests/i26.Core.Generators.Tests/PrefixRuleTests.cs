using i26.Core.Ids;
using Microsoft.CodeAnalysis;

namespace i26.Core.Generators.Tests;

/// <summary>
/// The two things about this generator that nothing else can check: that its copy of the prefix
/// rules still agrees with the runtime's, and that what it says survives a build log.
/// </summary>
public class PrefixRuleTests
{
    [Fact]
    public void The_generator_and_the_runtime_agree_on_the_prefix_rules()
    {
        // An analyzer targets netstandard2.0 and cannot reference the library it generates for, so
        // the rules exist twice. This is the only thing holding the two copies together.
        Assert.Equal(TypedIdPrefix.MaxLength, TypedIdGenerator.MaxPrefixLength);
        Assert.Equal(TypedIdPrefix.MaxExtendedLength, TypedIdGenerator.MaxExtendedPrefixLength);
    }

    [Fact]
    public void Every_descriptor_is_pure_ascii()
    {
        // This file has already lost a character to a round trip through a cp1252 tool, and the
        // damage shipped in the message of I26ID003. Diagnostics travel through terminals and build
        // logs of unknown encoding; ASCII is the only thing that survives all of them.
        foreach (var descriptor in Descriptors())
        {
            AssertAscii(descriptor.Id, descriptor.Title.ToString());
            AssertAscii(descriptor.Id, descriptor.MessageFormat.ToString());
        }
    }

    [Fact]
    public void Every_descriptor_is_an_error_in_the_same_category()
    {
        foreach (var descriptor in Descriptors())
        {
            Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity);
            Assert.Equal("i26.Ids", descriptor.Category);
            Assert.True(descriptor.IsEnabledByDefault);
        }
    }

    [Fact]
    public void The_rule_ids_are_the_ones_the_release_notes_track()
    {
        var declared = Descriptors().Select(descriptor => descriptor.Id).OrderBy(id => id, StringComparer.Ordinal);

        Assert.Equal(["I26ID001", "I26ID002", "I26ID003", "I26ID004", "I26ID005", "I26ID006"], declared);
    }

    private static void AssertAscii(string id, string text)
    {
        foreach (var character in text)
        {
            Assert.True(
                character is >= ' ' and <= '~',
                $"{id} carries U+{(int)character:X4} ('{character}'), which is not ASCII.");
        }
    }

    private static IEnumerable<DiagnosticDescriptor> Descriptors() =>
        typeof(TypedIdDiagnostics)
            .GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
            .Select(field => (DiagnosticDescriptor)field.GetValue(null)!);
}
