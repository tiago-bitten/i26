using i26.Core.Entities;
using i26.Core.Ids;
using i26.Core.ValueObjects;
using i26.EntityFrameworkCore.Ids;
using i26.EntityFrameworkCore.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.Tests.ValueObjects;

[TypedId("cnt")]
public readonly partial record struct ContactId;

public sealed class Contact : Entity<ContactId>
{
    public Email Email { get; set; } = null!;
}

public sealed class ContactDbContext(DbContextOptions<ContactDbContext> options) : DbContext(options)
{
    public DbSet<Contact> Contacts => Set<Contact>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTypedIdConventions(TypedIdStorage.ProviderDefault, typeof(ContactId).Assembly);
        configurationBuilder.ApplyValueObjectConventions();

        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}

/// <summary>
/// The value object as a column: it goes down as the text it is, comes back as an address that
/// passed, and change tracking knows two equal ones are not a change.
/// </summary>
public sealed class EmailPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public EmailPersistenceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private ContactDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ContactDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task An_address_round_trips()
    {
        var email = Email.Parse("tiago@nextfit.com.br");

        using (var context = CreateContext())
        {
            context.Contacts.Add(new Contact { Email = email });
            await context.SaveChangesAsync();
        }

        using var reading = CreateContext();
        var read = await reading.Contacts.SingleAsync();

        Assert.Equal(email, read.Email);
        Assert.Equal("nextfit.com.br", read.Email.Domain);
    }

    [Fact]
    public async Task It_is_stored_as_the_text_it_is()
    {
        using var context = CreateContext();
        context.Contacts.Add(new Contact { Email = Email.Parse("TIAGO@Example.com") });
        await context.SaveChangesAsync();

        await using var command = _connection.CreateCommand();
        command.CommandText = "select Email from Contacts";

        // Lowercased on the way in, so a unique index on this column means what it looks like.
        Assert.Equal("tiago@example.com", await command.ExecuteScalarAsync());
    }

    [Fact]
    public void The_column_is_as_wide_as_an_address_can_be()
    {
        using var context = CreateContext();

        var column = context.Model.FindEntityType(typeof(Contact))!.FindProperty(nameof(Contact.Email))!;

        Assert.Equal(Email.MaxLength, column.GetMaxLength());
        Assert.Equal(typeof(string), column.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public async Task An_address_can_be_queried_by_value()
    {
        var email = Email.Parse("tiago@example.com");

        using var context = CreateContext();
        context.Contacts.Add(new Contact { Email = email });
        await context.SaveChangesAsync();

        // Translated to a comparison on the column, not evaluated after reading the table.
        Assert.NotNull(await context.Contacts.AsNoTracking().SingleOrDefaultAsync(c => c.Email == email));
    }

    [Fact]
    public async Task Assigning_an_equal_address_is_not_a_change()
    {
        using var context = CreateContext();
        var contact = new Contact { Email = Email.Parse("tiago@example.com") };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();

        // A different instance holding the same address. Without a comparer, change tracking would
        // fall back to reference equality and write an update saying nothing.
        contact.Email = Email.Parse("tiago@example.com");

        Assert.False(context.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Assigning_another_address_is()
    {
        using var context = CreateContext();
        var contact = new Contact { Email = Email.Parse("tiago@example.com") };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();

        contact.Email = Email.Parse("outro@example.com");

        Assert.True(context.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task A_row_holding_something_else_fails_loudly()
    {
        using var context = CreateContext();
        context.Contacts.Add(new Contact { Email = Email.Parse("tiago@example.com") });
        await context.SaveChangesAsync();

        await using (var command = _connection.CreateCommand())
        {
            command.CommandText = "update Contacts set Email = 'not an address'";
            await command.ExecuteNonQueryAsync();
        }

        using var reading = CreateContext();
        var exception = await Record.ExceptionAsync(() => reading.Contacts.ToListAsync());

        // Quietly becoming an Email that never passed a check is the alternative, and it is worse.
        Assert.NotNull(exception);
        Assert.Contains("email", Unwrap(exception).Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Exception Unwrap(Exception exception) =>
        exception.InnerException is { } inner ? Unwrap(inner) : exception;
}
