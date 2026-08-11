using i26.Core.Ids;

namespace i26.Core.Tests.Ids;

/// <summary>
/// The sweep a service runs over its own ids. Nothing here can be caught by the compiler: two
/// entities are free to pick the same three letters, and the code goes on compiling.
/// </summary>
public class TypedIdPrefixSweepTests
{
    [Fact]
    public void A_set_of_distinct_prefixes_passes()
    {
        TypedIdPrefix.ValidateAll([typeof(TestUserId), typeof(TestOrderId), typeof(TestExternalAuthId)]);
    }

    [Fact]
    public void Two_ids_sharing_a_prefix_name_both_types_and_the_prefix()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TypedIdPrefix.ValidateAll([typeof(TestUserId), typeof(ShadowUserId)]));

        Assert.Contains(nameof(TestUserId), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ShadowUserId), exception.Message, StringComparison.Ordinal);
        Assert.Contains("'usr'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ambiguous", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_order_of_the_types_does_not_change_the_verdict()
    {
        Assert.Throws<InvalidOperationException>(
            () => TypedIdPrefix.ValidateAll([typeof(ShadowUserId), typeof(TestUserId)]));
    }

    [Fact]
    public void An_id_of_the_same_entity_declared_twice_is_still_a_collision()
    {
        // Same type twice is not a duplicate prefix, it is the same id.
        Assert.Throws<InvalidOperationException>(
            () => TypedIdPrefix.ValidateAll([typeof(TestUserId), typeof(TestUserId)]));
    }

    [Fact]
    public void A_broken_prefix_is_reported_by_the_sweep_too()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TypedIdPrefix.ValidateAll([typeof(TestUserId), typeof(FourLetterId)]));

        Assert.Contains(nameof(FourLetterId), exception.Message, StringComparison.Ordinal);
        Assert.Contains("UsesExtendedPrefix", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Something_that_is_not_an_id_is_refused_up_front()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => TypedIdPrefix.ValidateAll([typeof(Guid)]));

        Assert.Contains("ITypedId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_set_passes()
    {
        // Typed explicitly: an empty collection expression cannot tell the two overloads apart.
        TypedIdPrefix.ValidateAll(Array.Empty<Type>());
    }

    [Fact]
    public void FindTypedIds_picks_up_the_ids_of_an_assembly()
    {
        var found = TypedId.FindTypedIds(typeof(TestUserId).Assembly).ToArray();

        Assert.Contains(typeof(TestUserId), found);
        Assert.Contains(typeof(TestOrderId), found);
        Assert.Contains(typeof(ShadowUserId), found);
        Assert.DoesNotContain(typeof(Guid), found);
    }

    [Theory]
    [InlineData(typeof(TestUserId), true)]
    [InlineData(typeof(TestExternalAuthId), true)]
    [InlineData(typeof(Guid), false)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(ITypedId<TestUserId>), false)]
    public void IsTypedId_recognizes_the_real_ones(Type type, bool expected)
    {
        Assert.Equal(expected, TypedId.IsTypedId(type));
    }

    /// <summary>The mistake the sweep exists to catch: someone else's <c>usr</c>.</summary>
    private readonly record struct ShadowUserId(Guid Value) : ITypedId<ShadowUserId>
    {
        public static string Prefix => "usr";

        public static ShadowUserId FromGuid(Guid value) => new(value);

        public static ShadowUserId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<ShadowUserId>(s);

        public static bool TryParse(string? s, IFormatProvider? _, out ShadowUserId result)
            => TypedId.TryParse(s, out result);
    }

    private readonly record struct FourLetterId(Guid Value) : ITypedId<FourLetterId>
    {
        public static string Prefix => "crse";

        public static FourLetterId FromGuid(Guid value) => new(value);

        public static FourLetterId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<FourLetterId>(s);

        public static bool TryParse(string? s, IFormatProvider? _, out FourLetterId result)
            => TypedId.TryParse(s, out result);
    }
}
