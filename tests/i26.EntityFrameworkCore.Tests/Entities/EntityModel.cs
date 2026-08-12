using i26.Core.DomainEvents;
using i26.Core.Entities;
using i26.Core.Ids;
using i26.EntityFrameworkCore.Entities;
using i26.EntityFrameworkCore.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.Tests.Entities;

[TypedId("nte")]
public readonly partial record struct NoteId;

[TypedId("tag")]
public readonly partial record struct TagId;

public sealed record NoteWritten(NoteId Id, string Title) : IDomainEvent;

public sealed record NoteRetitled(NoteId Id, string Title) : IDomainEvent;

/// <summary>Everything the base offers at once: typed id, timestamps, events and a soft delete.</summary>
public sealed class Note : DeletableEntity<NoteId>
{
    private Note()
    {
    }

    public string Title { get; private set; } = string.Empty;

    public static Note Write(string title)
    {
        var note = new Note { Title = title };
        note.Raise(new NoteWritten(note.Id, title));

        return note;
    }

    public void Retitle(string title)
    {
        Title = title;
        Raise(new NoteRetitled(Id, title));
    }
}

/// <summary>A table nobody pages, so it pays for no index.</summary>
public sealed class Tag : Entity<TagId>
{
    public string Name { get; set; } = string.Empty;
}

public sealed class NoteConfiguration : EntityConfiguration<Note, NoteId>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Note> builder) =>
        builder.Property(note => note.Title).HasMaxLength(200);
}

public sealed class TagConfiguration : EntityConfiguration<Tag, TagId>
{
    protected override bool IsPaged => false;

    protected override void ConfigureEntity(EntityTypeBuilder<Tag> builder) =>
        builder.Property(tag => tag.Name).HasMaxLength(50);
}

public sealed class NoteDbContext(DbContextOptions<NoteDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();

    public DbSet<Tag> Tags => Set<Tag>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTypedIdConventions(TypedIdStorage.ProviderDefault, typeof(NoteId).Assembly);

        // SQLite has no date type and refuses to order by DateTimeOffset.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new NoteConfiguration());
        modelBuilder.ApplyConfiguration(new TagConfiguration());

        // Last, over whatever the configurations left in the model.
        modelBuilder.ApplySoftDeleteFilter();
    }
}

/// <summary>A clock the test moves by hand.</summary>
public sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
