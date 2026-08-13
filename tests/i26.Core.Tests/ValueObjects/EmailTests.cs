using i26.Core.ValueObjects;

namespace i26.Core.Tests.ValueObjects;

/// <summary>
/// The rules, one refusal at a time. What a form shows someone depends on which of these answered,
/// so each carries its own code rather than a single "invalid".
/// </summary>
public sealed class EmailTests
{
    [Theory]
    [InlineData("tiago@nextfit.com.br")]
    [InlineData("a@b.co")]
    [InlineData("first.last@example.com")]
    [InlineData("first+tag@example.com")]
    [InlineData("first_last@sub.example.co.uk")]
    [InlineData("user-1@my-domain.dev")]
    [InlineData("123@456.com")]
    public void An_address_that_holds_up(string value)
    {
        var email = Email.Create(value);

        Assert.True(email.IsSuccess, $"'{value}' was refused with {email.Error.Code}");
        Assert.Equal(value, email.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_is_not_an_address(string? value)
    {
        Assert.Equal(EmailErrors.Required, Email.Create(value).Error);
    }

    [Theory]
    [InlineData("tiago")]
    [InlineData("@example.com")]
    [InlineData("tiago@")]
    [InlineData("tiago@@example.com")]
    [InlineData("tiago@a@example.com")]
    public void An_address_has_one_at_with_something_on_both_sides(string value)
    {
        Assert.Equal(EmailErrors.Malformed, Email.Create(value).Error);
    }

    [Theory]
    [InlineData(".tiago@example.com")]
    [InlineData("tiago.@example.com")]
    [InlineData("ti..ago@example.com")]
    [InlineData("ti ago@example.com")]
    [InlineData("ti!ago@example.com")]
    [InlineData("\"ti ago\"@example.com")]
    public void The_part_before_the_at_is_checked(string value)
    {
        Assert.Equal(EmailErrors.InvalidLocalPart, Email.Create(value).Error);
    }

    [Theory]
    [InlineData("tiago@example")]
    [InlineData("tiago@.example.com")]
    [InlineData("tiago@example.com.")]
    [InlineData("tiago@exa..mple.com")]
    [InlineData("tiago@-example.com")]
    [InlineData("tiago@exa mple.com")]
    [InlineData("tiago@exa_mple.com")]
    [InlineData("tiago@[192.168.0.1]")]
    public void The_part_after_the_at_is_a_domain_name(string value)
    {
        Assert.Equal(EmailErrors.InvalidDomain, Email.Create(value).Error);
    }

    [Fact]
    public void An_address_longer_than_a_path_can_carry_is_refused()
    {
        var value = new string('a', 60) + "@" + new string('b', Email.MaxLength) + ".com";

        var email = Email.Create(value);

        Assert.Equal(EmailErrors.TooLong(Email.MaxLength).Code, email.Error.Code);
        Assert.Equal(Email.MaxLength, Assert.Single(email.Error.Arguments!));
    }

    [Fact]
    public void A_local_part_longer_than_sixty_four_is_refused()
    {
        var value = new string('a', Email.MaxLocalPartLength + 1) + "@example.com";

        Assert.Equal(EmailErrors.InvalidLocalPart, Email.Create(value).Error);
    }

    [Theory]
    [InlineData("  tiago@example.com  ", "tiago@example.com")]
    [InlineData("TIAGO@EXAMPLE.COM", "tiago@example.com")]
    [InlineData("Tiago.Bittencourt@Example.COM", "tiago.bittencourt@example.com")]
    public void An_address_is_kept_the_way_it_will_be_compared(string typed, string stored)
    {
        Assert.Equal(stored, Email.Create(typed).Value.Value);
    }

    [Fact]
    public void Two_people_typing_the_same_address_differently_wrote_the_same_one()
    {
        // Which is what makes equality and a unique index agree.
        Assert.Equal(Email.Create("Tiago@Example.com ").Value, Email.Create("tiago@example.com").Value);
    }

    [Fact]
    public void The_two_halves_are_there_without_splitting_it_again()
    {
        var email = Email.Create("first.last@sub.example.com").Value;

        Assert.Equal("first.last", email.LocalPart);
        Assert.Equal("sub.example.com", email.Domain);
        Assert.Equal("first.last@sub.example.com", email.ToString());
    }

    [Fact]
    public void Parsing_what_is_already_known_to_be_one()
    {
        Assert.Equal("tiago@example.com", Email.Parse("tiago@example.com").Value);
    }

    [Fact]
    public void Parsing_something_else_throws_and_says_which_rule()
    {
        var exception = Assert.Throws<FormatException>(() => Email.Parse("tiago"));

        Assert.Contains(EmailErrors.Malformed.Code, exception.Message, StringComparison.Ordinal);
    }
}
