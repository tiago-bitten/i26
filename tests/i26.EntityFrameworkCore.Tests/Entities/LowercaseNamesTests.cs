using i26.Core.Entities;
using i26.Core.Ids;
using i26.EntityFrameworkCore.Entities;
using i26.EntityFrameworkCore.Ids;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.Tests.Entities;

[TypedId("uat")]
public readonly partial record struct UserAuthId;

[TypedId("prv")]
public readonly partial record struct AuthProviderId;

public sealed class AuthProvider : Entity<AuthProviderId>
{
    public string ProviderName { get; set; } = string.Empty;
}

/// <summary>The name from the question: UserAuth is userauth, and every column follows.</summary>
public sealed class UserAuth : Entity<UserAuthId>
{
    public string EmailAddress { get; set; } = string.Empty;

    public AuthProviderId ProviderId { get; set; }

    public AuthProvider Provider { get; set; } = null!;
}

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<UserAuth> UserAuths => Set<UserAuth>();

    public DbSet<AuthProvider> AuthProviders => Set<AuthProvider>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTypedIdConventions(TypedIdStorage.ProviderDefault, typeof(UserAuthId).Assembly);
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAuth>().HasIndex(auth => auth.EmailAddress).IsUnique();

        // A name chosen by hand, to show it is lowercased too.
        modelBuilder.Entity<AuthProvider>().ToTable("AuthProviders");

        // Last, over whatever everything before it decided.
        modelBuilder.ApplyLowercaseNames();
    }
}

/// <summary>
/// Everything the model puts in the database is lowercase, because on Postgres an identifier that
/// is not has to be quoted in every migration and every query anybody writes afterwards.
/// </summary>
public sealed class LowercaseNamesTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public LowercaseNamesTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private AuthDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AuthDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public void The_table_is_the_entity_in_lowercase()
    {
        using var context = CreateContext();

        Assert.Equal("userauths", context.Model.FindEntityType(typeof(UserAuth))!.GetTableName());
    }

    [Fact]
    public void A_name_chosen_by_hand_is_lowercased_as_well()
    {
        using var context = CreateContext();

        Assert.Equal("authproviders", context.Model.FindEntityType(typeof(AuthProvider))!.GetTableName());
    }

    [Fact]
    public void Every_column_follows()
    {
        using var context = CreateContext();

        var columns = context.Model
            .FindEntityType(typeof(UserAuth))!
            .GetProperties()
            .Select(property => property.GetColumnName());

        Assert.All(columns, column => Assert.Equal(column.ToLowerInvariant(), column));
        Assert.Contains("emailaddress", columns);
        Assert.Contains("createdat", columns);
    }

    [Fact]
    public void So_do_the_keys_the_indexes_and_the_foreign_keys()
    {
        using var context = CreateContext();
        var auth = context.Model.FindEntityType(typeof(UserAuth))!;

        Assert.All(auth.GetKeys(), key => Assert.Equal(key.GetName()!.ToLowerInvariant(), key.GetName()));
        Assert.All(auth.GetIndexes(), index =>
            Assert.Equal(index.GetDatabaseName()!.ToLowerInvariant(), index.GetDatabaseName()));
        Assert.All(auth.GetForeignKeys(), foreignKey =>
            Assert.Equal(foreignKey.GetConstraintName()!.ToLowerInvariant(), foreignKey.GetConstraintName()));

        Assert.NotEmpty(auth.GetForeignKeys());
    }

    [Fact]
    public void The_schema_it_creates_is_the_schema_it_queries()
    {
        using var context = CreateContext();
        context.Database.EnsureCreated();

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("\"userauths\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("\"UserAuths\"", script, StringComparison.Ordinal);

        // And the model still reads what it wrote, which is the part a name change can break.
        context.AuthProviders.Add(new AuthProvider { ProviderName = "google" });
        context.SaveChanges();

        Assert.Single(context.AuthProviders.ToList());
    }
}
