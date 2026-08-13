using i26.Core.Entities;
using i26.Core.Ids;
using i26.Core.ValueObjects;
using i26.EntityFrameworkCore.Entities;
using i26.EntityFrameworkCore.Ids;
using i26.EntityFrameworkCore.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.Tests.ValueObjects;

[TypedId("acc")]
public readonly partial record struct AccountId;

public sealed class Account : Entity<AccountId>
{
    public Email Email { get; set; } = null!;

    public Email? Recovery { get; set; }
}

public sealed class AccountConfiguration : EntityConfiguration<Account, AccountId>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Account> builder)
    {
        builder.HasValueObject(account => account.Email, unique: true).IsRequired();
        builder.HasValueObject(account => account.Recovery);
    }
}

/// <summary>Deliberately without <c>ApplyValueObjectConventions</c>: the configuration says it all.</summary>
public sealed class AccountDbContext(DbContextOptions<AccountDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTypedIdConventions(TypedIdStorage.ProviderDefault, typeof(AccountId).Assembly);
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
}

/// <summary>
/// The address mapped from the configuration of the entity that holds it, which is where an index
/// over it belongs — unique is a decision about this entity, not about the type.
/// </summary>
public sealed class EmailConfigurationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public EmailConfigurationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private AccountDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AccountDbContext>().UseSqlite(_connection).Options);

    private IEntityType Mapped
    {
        get
        {
            using var context = CreateContext();

            return context.Model.FindEntityType(typeof(Account))!;
        }
    }

    [Fact]
    public void The_property_is_mapped_without_the_convention_having_run()
    {
        var email = Mapped.FindProperty(nameof(Account.Email))!;

        Assert.Equal(typeof(string), email.GetValueConverter()!.ProviderClrType);
        Assert.Equal(Email.MaxLength, email.GetMaxLength());
        Assert.NotNull(email.GetValueComparer());
        Assert.False(email.IsNullable);
    }

    [Fact]
    public void A_second_address_on_the_same_entity_is_mapped_too_and_stays_optional()
    {
        var recovery = Mapped.FindProperty(nameof(Account.Recovery))!;

        Assert.Equal(Email.MaxLength, recovery.GetMaxLength());
        Assert.True(recovery.IsNullable);
    }

    [Fact]
    public void The_one_asked_to_be_unique_is()
    {
        var index = Assert.Single(
            Mapped.GetIndexes(),
            candidate => candidate.Properties.Any(p => p.Name == nameof(Account.Email)));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void The_one_that_was_not_asked_has_no_index_of_its_own()
    {
        Assert.DoesNotContain(
            Mapped.GetIndexes(),
            index => index.Properties.Any(p => p.Name == nameof(Account.Recovery)));
    }

    [Fact]
    public async Task The_unique_index_is_in_the_database_and_refuses_a_second_one()
    {
        using var context = CreateContext();
        context.Accounts.Add(new Account { Email = Email.Parse("tiago@example.com") });
        await context.SaveChangesAsync();

        using var second = CreateContext();
        second.Accounts.Add(new Account { Email = Email.Parse("TIAGO@example.com") });

        // The same address written differently is the same address, which is the whole reason it
        // is lowercased before it gets here.
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task An_optional_address_round_trips_as_nothing()
    {
        using var context = CreateContext();
        context.Accounts.Add(new Account { Email = Email.Parse("tiago@example.com") });
        await context.SaveChangesAsync();

        using var reading = CreateContext();

        Assert.Null((await reading.Accounts.SingleAsync()).Recovery);
    }
}
