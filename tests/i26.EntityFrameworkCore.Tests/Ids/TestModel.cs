using i26.Core.Ids;
using i26.EntityFrameworkCore.Ids;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.Tests.Ids;

public readonly record struct OrderId(Guid Value) : ITypedId<OrderId>
{
    public static string Prefix => "ord";

    public static OrderId FromGuid(Guid value) => new(value);

    public static OrderId New() => TypedId.New<OrderId>();

    public override string ToString() => TypedId.Format(this);

    public static OrderId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<OrderId>(s);

    public static bool TryParse(string? s, IFormatProvider? _, out OrderId result)
        => TypedId.TryParse(s, out result);
}

public readonly record struct UserId(Guid Value) : ITypedId<UserId>
{
    public static string Prefix => "usr";

    public static UserId FromGuid(Guid value) => new(value);

    public static UserId New() => TypedId.New<UserId>();

    public override string ToString() => TypedId.Format(this);

    public static UserId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<UserId>(s);

    public static bool TryParse(string? s, IFormatProvider? _, out UserId result)
        => TypedId.TryParse(s, out result);
}

public sealed class Order
{
    public OrderId Id { get; init; }

    public UserId OwnerId { get; init; }

    public UserId? ReviewerId { get; init; }

    public string Description { get; init; } = string.Empty;
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ApplyTypedIdConventions(typeof(OrderId).Assembly);
}
