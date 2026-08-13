using i26.Core.Entities;
using i26.Core.Ids;
using i26.Core.Results;
using i26.Core.ValueObjects;
using i26.EntityFrameworkCore.Ids;
using i26.EntityFrameworkCore.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.Tests.ValueObjects;

/// <summary>
/// A value object written outside this library, in the assembly a consumer's domain would be. It
/// implements the interface and nothing else — no converter, no comparer, no line in i26.
/// </summary>
public sealed record Slug : IStringValueObject<Slug>
{
    private Slug(string value) => Value = value;

    public static int MaxLength => 80;

    public string Value { get; }

    public static Result<Slug> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Error.Validation("slug.required");
        }

        var slug = value.Trim().ToLowerInvariant();

        if (slug.Length > MaxLength)
        {
            return Error.Validation("slug.tooLong", MaxLength);
        }

        return slug.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
            ? new Slug(slug)
            : Error.Validation("slug.invalid");
    }

    public static Slug Parse(string s, IFormatProvider? provider)
    {
        var created = Create(s);

        return created.IsSuccess ? created.Value : throw new FormatException($"'{s}' is not a slug.");
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out Slug result)
    {
        var created = Create(s);
        result = created.IsSuccess ? created.Value : null!;

        return created.IsSuccess;
    }

    public override string ToString() => Value;
}

[TypedId("pst")]
public readonly partial record struct PostId;

public sealed class Post : Entity<PostId>
{
    public Slug Slug { get; set; } = null!;

    public Email Author { get; set; } = null!;
}

public sealed class PostDbContext(DbContextOptions<PostDbContext> options) : DbContext(options)
{
    public DbSet<Post> Posts => Set<Post>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTypedIdConventions(TypedIdStorage.ProviderDefault, typeof(PostId).Assembly);

        // The consumer's assembly, and nothing about Email — which lives somewhere else and is
        // mapped anyway.
        configurationBuilder.ApplyValueObjectConventions(typeof(Slug).Assembly);

        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}

/// <summary>
/// The reason the shape is generic: a value object this library has never heard of is mapped by the
/// same call, with the same converter and the same comparer, and none of it was written twice.
/// </summary>
public sealed class ConsumerValueObjectTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ConsumerValueObjectTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private PostDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PostDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public void A_value_object_of_the_consumer_is_mapped_by_the_same_call()
    {
        using var context = CreateContext();

        var slug = context.Model.FindEntityType(typeof(Post))!.FindProperty(nameof(Post.Slug))!;

        Assert.Equal(typeof(string), slug.GetValueConverter()!.ProviderClrType);
        Assert.Equal(Slug.MaxLength, slug.GetMaxLength());
        Assert.NotNull(slug.GetValueComparer());
    }

    [Fact]
    public void And_so_are_the_ones_that_came_with_the_library()
    {
        using var context = CreateContext();

        var author = context.Model.FindEntityType(typeof(Post))!.FindProperty(nameof(Post.Author))!;

        Assert.Equal(Email.MaxLength, author.GetMaxLength());
    }

    [Fact]
    public async Task Both_round_trip()
    {
        using (var context = CreateContext())
        {
            context.Posts.Add(new Post
            {
                Slug = Slug.Parse("the-first-one", provider: null),
                Author = Email.Parse("tiago@example.com"),
            });

            await context.SaveChangesAsync();
        }

        using var reading = CreateContext();
        var article = await reading.Posts.SingleAsync();

        Assert.Equal("the-first-one", article.Slug.Value);
        Assert.Equal("tiago@example.com", article.Author.Value);
    }

    [Fact]
    public async Task Change_tracking_knows_two_equal_ones_are_not_a_change()
    {
        using var context = CreateContext();
        var article = new Post
        {
            Slug = Slug.Parse("the-first-one", provider: null),
            Author = Email.Parse("tiago@example.com"),
        };

        context.Posts.Add(article);
        await context.SaveChangesAsync();

        article.Slug = Slug.Parse("the-first-one", provider: null);

        Assert.False(context.ChangeTracker.HasChanges());
    }
}
