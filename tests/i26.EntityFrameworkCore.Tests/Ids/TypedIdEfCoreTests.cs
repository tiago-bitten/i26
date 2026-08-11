using i26.Core.Ids;
using i26.EntityFrameworkCore.Ids;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace i26.EntityFrameworkCore.Tests.Ids;

/// <summary>
/// Exercises the conventions against a real database. The provider here is in-memory SQLite — the
/// target is Postgres, but what is under test (converter applied, column type, collation, round
/// trip, comparison and ordering) does not depend on the provider.
/// </summary>
public sealed class TypedIdEfCoreTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public TypedIdEfCoreTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        // Postgres ships the "C" collation (ordinal, byte by byte); on SQLite it is registered here
        // with the same semantics so the DDL emitted by the convention works.
        _connection.CreateCollation("C", static (left, right) => string.CompareOrdinal(left, right));

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new TestDbContext(options);
    }

    /// <summary>
    /// The runtime model is trimmed down and does not carry design-time configuration (collation,
    /// column type); the design-time model is what answers for it.
    /// </summary>
    private IEntityType GetOrderEntityType(TestDbContext context)
        => context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Order))!;

    [Fact]
    public void Convention_maps_to_text_with_collation_C()
    {
        using var context = CreateContext();

        var entity = GetOrderEntityType(context);

        foreach (var propertyName in new[] { nameof(Order.Id), nameof(Order.OwnerId), nameof(Order.ReviewerId) })
        {
            var property = entity.FindProperty(propertyName)!;

            Assert.Equal("text", property.GetColumnType());
            Assert.Equal("C", property.GetCollation());
            Assert.Equal(typeof(string), property.GetValueConverter()!.ProviderClrType);
        }
    }

    [Fact]
    public void Convention_applies_the_typed_id_converter_and_comparer()
    {
        using var context = CreateContext();

        var entity = GetOrderEntityType(context);

        Assert.IsType<TypedIdToStringConverter<OrderId>>(entity.FindProperty(nameof(Order.Id))!.GetValueConverter());
        Assert.IsType<TypedIdComparer<OrderId>>(entity.FindProperty(nameof(Order.Id))!.GetValueComparer());
        Assert.IsType<TypedIdToStringConverter<UserId>>(entity.FindProperty(nameof(Order.OwnerId))!.GetValueConverter());
    }

    [Fact]
    public void Convention_leaves_ordinary_properties_alone()
    {
        using var context = CreateContext();

        var description = GetOrderEntityType(context).FindProperty(nameof(Order.Description))!;

        Assert.Null(description.GetValueConverter());
        Assert.Null(description.GetCollation());
    }

    [Fact]
    public void Writes_and_reads_back_the_typed_id()
    {
        var order = new Order
        {
            Id = OrderId.New(),
            OwnerId = UserId.New(),
            ReviewerId = UserId.New(),
            Description = "test order",
        };

        using (var context = CreateContext())
        {
            context.Orders.Add(order);
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            var loaded = context.Orders.Single(candidate => candidate.Id == order.Id);

            Assert.Equal(order.Id, loaded.Id);
            Assert.Equal(order.OwnerId, loaded.OwnerId);
            Assert.Equal(order.ReviewerId, loaded.ReviewerId);
        }
    }

    [Fact]
    public void Writes_null_for_a_nullable_id()
    {
        var order = new Order { Id = OrderId.New(), OwnerId = UserId.New(), ReviewerId = null };

        using (var context = CreateContext())
        {
            context.Orders.Add(order);
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            Assert.Null(context.Orders.Single().ReviewerId);
        }
    }

    [Fact]
    public void Persists_the_full_prefixed_string()
    {
        var order = new Order { Id = OrderId.New(), OwnerId = UserId.New() };

        using (var context = CreateContext())
        {
            context.Orders.Add(order);
            context.SaveChanges();
        }

        using var command = _connection.CreateCommand();
        command.CommandText = """SELECT "Id", "OwnerId" FROM "Orders" """;

        using var reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(order.Id.ToString(), reader.GetString(0));
        Assert.Equal(order.OwnerId.ToString(), reader.GetString(1));
        Assert.StartsWith("ord_", reader.GetString(0), StringComparison.Ordinal);
        Assert.StartsWith("usr_", reader.GetString(1), StringComparison.Ordinal);
    }

    [Fact]
    public void Ordering_by_the_column_returns_creation_order()
    {
        var ids = new OrderId[5];

        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = OrderId.New();
            Thread.Sleep(2);
        }

        using (var context = CreateContext())
        {
            // Inserted out of order on purpose.
            for (var i = ids.Length - 1; i >= 0; i--)
            {
                context.Orders.Add(new Order { Id = ids[i], OwnerId = UserId.New() });
            }

            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            var ordered = context.Orders
                .OrderBy(order => order.Id)
                .Select(order => order.Id)
                .ToArray();

            Assert.Equal(ids, ordered);
        }
    }

    [Fact]
    public void Corrupted_data_fails_loudly_on_read()
    {
        using (var context = CreateContext())
        {
            context.Orders.Add(new Order { Id = OrderId.New(), OwnerId = UserId.New() });
            context.SaveChanges();
        }

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = """UPDATE "Orders" SET "OwnerId" = 'not an id' """;
            command.ExecuteNonQuery();
        }

        using (var context = CreateContext())
        {
            Assert.ThrowsAny<Exception>(() => context.Orders.ToList());
        }
    }

    [Fact]
    public void Change_tracker_leaves_an_untouched_id_unchanged()
    {
        var order = new Order { Id = OrderId.New(), OwnerId = UserId.New() };

        using (var context = CreateContext())
        {
            context.Orders.Add(order);
            context.SaveChanges();
        }

        using (var context = CreateContext())
        {
            var loaded = context.Orders.Single();

            context.ChangeTracker.DetectChanges();

            Assert.Equal(EntityState.Unchanged, context.Entry(loaded).State);
        }
    }
}
