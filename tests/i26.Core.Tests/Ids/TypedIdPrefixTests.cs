using i26.Core.Ids;

namespace i26.Core.Tests.Ids;

public class TypedIdPrefixTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("us")]
    [InlineData("usr")]
    [InlineData("crs")]
    public void One_to_three_lowercase_letters_are_valid(string prefix)
    {
        Assert.True(TypedIdPrefix.IsValid(prefix));
    }

    [Theory]
    // Longer than the three-character rule.
    [InlineData("auth")]
    [InlineData("course")]
    // Empty or missing.
    [InlineData("")]
    [InlineData(null)]
    // Uppercase: an id would have more than one textual form.
    [InlineData("Usr")]
    [InlineData("USR")]
    // The separator itself, and anything else outside a-z.
    [InlineData("u_r")]
    [InlineData("us1")]
    [InlineData("us-")]
    [InlineData("çao")]
    public void Anything_else_is_not(string? prefix)
    {
        Assert.False(TypedIdPrefix.IsValid(prefix));
    }

    [Theory]
    [InlineData("auth")]
    [InlineData("course")]
    [InlineData("workspace")]
    [InlineData("enrollment")]
    public void The_extended_rule_takes_up_to_ten(string prefix)
    {
        Assert.False(TypedIdPrefix.IsValid(prefix));
        Assert.True(TypedIdPrefix.IsValid(prefix, extended: true));
    }

    [Fact]
    public void Not_even_the_extended_rule_takes_eleven()
    {
        Assert.Equal(10, TypedIdPrefix.MaxExtendedLength);
        Assert.False(TypedIdPrefix.IsValid("enrollments", extended: true));
    }

    [Fact]
    public void The_extended_rule_does_not_relax_the_alphabet()
    {
        Assert.False(TypedIdPrefix.IsValid("Workspace", extended: true));
        Assert.False(TypedIdPrefix.IsValid("work_space", extended: true));
    }

    [Fact]
    public void The_rules_are_three_and_ten()
    {
        Assert.Equal(3, TypedIdPrefix.MaxLength);
        Assert.Equal(3, TypedIdPrefix.MaxLengthFor(extended: false));
        Assert.Equal(10, TypedIdPrefix.MaxLengthFor(extended: true));
    }

    [Fact]
    public void A_well_formed_id_passes_the_check()
    {
        Assert.Equal("usr", TypedIdPrefix.Validate<TestUserId>());
        Assert.Equal("ord", TypedIdPrefix.Validate<TestOrderId>());
    }

    [Fact]
    public void An_id_that_opted_in_may_go_past_three()
    {
        Assert.Equal("auth", TypedIdPrefix.Validate<TestExternalAuthId>());
        Assert.Equal("auth_01h455vb4pex5vsknk084sn02q", TypedId.Parse<TestExternalAuthId>("auth_01h455vb4pex5vsknk084sn02q").ToString());
    }

    [Fact]
    public void A_prefix_past_three_without_opting_in_says_how_to_opt_in()
    {
        var exception = Assert.Throws<InvalidOperationException>(TypedIdPrefix.Validate<LongPrefixId>);

        Assert.Contains("'auth'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(LongPrefixId), exception.Message, StringComparison.Ordinal);
        Assert.Contains("UsesExtendedPrefix", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_prefix_past_ten_is_refused_even_after_opting_in()
    {
        var exception = Assert.Throws<InvalidOperationException>(TypedIdPrefix.Validate<TooLongExtendedPrefixId>);

        Assert.Contains("at most 10", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Declare", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_uppercase_prefix_is_refused()
    {
        var exception = Assert.Throws<InvalidOperationException>(TypedIdPrefix.Validate<UppercasePrefixId>);

        Assert.Contains("lowercase ASCII letters only", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_check_also_fires_on_the_first_format_or_parse()
    {
        // Nothing has to call Validate explicitly for a bad prefix to be caught.
        Assert.Throws<InvalidOperationException>(() => TypedId.Format(LongPrefixId.FromGuid(Uuid7.New())));
        Assert.Throws<InvalidOperationException>(() => TypedId.TryParse<LongPrefixId>("auth_x", out _));
    }

    [Fact]
    public void Creating_an_id_with_a_bad_prefix_is_not_what_fails()
    {
        // The prefix only matters once the id has to become text.
        var id = LongPrefixId.FromGuid(Uuid7.New());

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    private readonly record struct LongPrefixId(Guid Value) : ITypedId<LongPrefixId>
    {
        public static string Prefix => "auth";

        public static LongPrefixId FromGuid(Guid value) => new(value);

        public static LongPrefixId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<LongPrefixId>(s);

        public static bool TryParse(string? s, IFormatProvider? _, out LongPrefixId result)
            => TypedId.TryParse(s, out result);

        public int CompareTo(LongPrefixId other) => TypedId.Compare(this, other);
    }

    private readonly record struct TooLongExtendedPrefixId(Guid Value) : ITypedId<TooLongExtendedPrefixId>
    {
        public static string Prefix => "enrollments";

        public static bool UsesExtendedPrefix => true;

        public static TooLongExtendedPrefixId FromGuid(Guid value) => new(value);

        public static TooLongExtendedPrefixId Parse(string s, IFormatProvider? _ = null)
            => TypedId.Parse<TooLongExtendedPrefixId>(s);

        public static bool TryParse(string? s, IFormatProvider? _, out TooLongExtendedPrefixId result)
            => TypedId.TryParse(s, out result);

        public int CompareTo(TooLongExtendedPrefixId other) => TypedId.Compare(this, other);
    }

    private readonly record struct UppercasePrefixId(Guid Value) : ITypedId<UppercasePrefixId>
    {
        public static string Prefix => "Usr";

        public static UppercasePrefixId FromGuid(Guid value) => new(value);

        public static UppercasePrefixId Parse(string s, IFormatProvider? _ = null)
            => TypedId.Parse<UppercasePrefixId>(s);

        public static bool TryParse(string? s, IFormatProvider? _, out UppercasePrefixId result)
            => TypedId.TryParse(s, out result);

        public int CompareTo(UppercasePrefixId other) => TypedId.Compare(this, other);
    }
}
