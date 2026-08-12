using i26.Core.Entities;
using i26.Core.Ids;
using i26.Core.Results;

namespace i26.Core.Tests.Entities;

/// <summary>
/// Deleting says so rather than removing, and says it once: the second attempt is a failure the
/// caller can answer with, not an exception and not a shrug.
/// </summary>
public sealed class DeletableEntityTests
{
    [Fact]
    public void A_new_entity_is_not_deleted()
    {
        var invoice = new Invoice();

        Assert.False(invoice.IsDeleted);
        Assert.Null(invoice.DeletedAt);
    }

    [Fact]
    public void Deleting_says_so()
    {
        var invoice = new Invoice();

        var result = invoice.Delete();

        Assert.True(result.IsSuccess);
        Assert.True(invoice.IsDeleted);
    }

    [Fact]
    public void Deleting_twice_is_a_failure_and_not_an_exception()
    {
        var invoice = new Invoice();
        invoice.Delete();

        var result = invoice.Delete();

        Assert.True(result.IsFailure);
        Assert.Equal(EntityErrors.AlreadyDeleted, result.Error);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public void Restoring_brings_it_back()
    {
        var invoice = new Invoice();
        invoice.Delete();

        var result = invoice.Restore();

        Assert.True(result.IsSuccess);
        Assert.False(invoice.IsDeleted);
        Assert.Null(invoice.DeletedAt);
    }

    [Fact]
    public void Restoring_what_was_never_deleted_is_refused()
    {
        var result = new Invoice().Restore();

        Assert.True(result.IsFailure);
        Assert.Equal(EntityErrors.NotDeleted, result.Error);
    }

    [Fact]
    public void An_entity_with_a_reason_of_its_own_gets_the_last_word()
    {
        var order = new Order { HasShipped = true };

        var refused = order.Delete();

        Assert.Equal(Order.Shipped, refused.Error);
        Assert.False(order.IsDeleted);

        order.HasShipped = false;

        Assert.True(order.Delete().IsSuccess);
        Assert.True(order.IsDeleted);
    }
}
