using System.Reflection;
using i26.Core.Ids;

namespace i26.Core.Tests.Ids;

/// <summary>
/// The id that names another service's entity. Leaving <c>New()</c> off the declaration used to be
/// the whole of the convention, and <c>TypedId.New&lt;TId&gt;()</c> walked straight around it.
/// </summary>
public class TypedIdMintingTests
{
    [Fact]
    public void An_id_this_service_does_not_mint_says_so()
    {
        Assert.False(MintedOf<GeneratedExternalId>());

        // The default lives on the interface, so the generator writes nothing for the usual id.
        Assert.True(MintedOf<GeneratedId>());
    }

    private static bool MintedOf<TId>()
        where TId : struct, ITypedId<TId>
        => TId.Minted;

    [Fact]
    public void The_generator_leaves_New_off_it()
    {
        var created = typeof(GeneratedExternalId).GetMethod(
            "New",
            BindingFlags.Public | BindingFlags.Static,
            Type.EmptyTypes);

        Assert.Null(created);
        Assert.NotNull(typeof(GeneratedId).GetMethod("New", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes));
    }

    [Fact]
    public void The_generic_form_does_not_get_around_it()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => _ = TypedId.New<GeneratedExternalId>());

        Assert.Contains("gex", exception.Message, StringComparison.Ordinal);
        Assert.Contains("another service", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void It_still_parses_and_formats_like_any_other_id()
    {
        var id = GeneratedExternalId.FromGuid(Uuid7.New());

        Assert.StartsWith("gex_", id.ToString(), StringComparison.Ordinal);
        Assert.Equal(id, GeneratedExternalId.Parse(id.ToString()));
    }

    [Fact]
    public void The_zero_id_is_the_one_nobody_assigned()
    {
        var empty = TypedId.Empty<TestUserId>();

        Assert.Equal(default, empty);
        Assert.True(TypedId.IsEmpty(empty));
        Assert.Equal("usr_00000000000000000000000000", empty.ToString());
        Assert.False(TypedId.IsEmpty(TestUserId.New()));
    }
}
