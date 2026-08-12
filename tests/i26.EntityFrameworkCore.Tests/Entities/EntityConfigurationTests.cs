using i26.Core.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace i26.EntityFrameworkCore.Tests.Entities;

/// <summary>
/// Half of this asserts what the base configuration does; the other half asserts what it does not
/// have to, because a convention already did. The second half is why the class is four lines
/// instead of fifteen, and it is what fails if a version of Entity Framework changes its mind.
/// </summary>
public sealed class EntityConfigurationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IModel _model;

    public EntityConfigurationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using var context = new NoteDbContext(
            new DbContextOptionsBuilder<NoteDbContext>().UseSqlite(_connection).Options);

        // Not context.Model: the runtime one is read-optimized and drops what it does not need to
        // run a query, the direction of an index included.
        _model = context.GetService<IDesignTimeModel>().Model;
    }

    public void Dispose() => _connection.Dispose();

    private IEntityType Note => _model.FindEntityType(typeof(Note))!;

    [Fact]
    public void The_conventions_already_settle_most_of_it()
    {
        var id = Note.FindProperty(nameof(Entity<NoteId>.Id))!;

        Assert.Equal([nameof(Entity<NoteId>.Id)], Note.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        Assert.False(id.IsNullable);
        Assert.False(Note.FindProperty(nameof(IEntity.CreatedAt))!.IsNullable);
        Assert.False(Note.FindProperty(nameof(IEntity.UpdatedAt))!.IsNullable);
        Assert.True(Note.FindProperty(nameof(ISoftDeletable.DeletedAt))!.IsNullable);
    }

    [Fact]
    public void The_events_are_still_not_in_the_model()
    {
        Assert.Null(Note.FindProperty("DomainEvents"));
        Assert.Null(Note.FindNavigation("DomainEvents"));
    }

    [Fact]
    public void The_index_a_page_reads_is_the_part_that_does_not_come_for_free()
    {
        var index = Assert.Single(Note.GetIndexes());

        // The instant first and the id to break its ties, both descending. The pair the other way
        // round — which is the one it is easy to write — is an index a page cannot use.
        Assert.Equal(
            [nameof(IEntity.CreatedAt), nameof(Entity<NoteId>.Id)],
            index.Properties.Select(property => property.Name));

        // Entity Framework normalises "every column descending" to an empty list. Null is what
        // means all ascending, so the assertion is that this is not that.
        Assert.NotNull(index.IsDescending);
        Assert.True(index.IsDescending.Count is 0 || index.IsDescending.All(descending => descending));
    }

    [Fact]
    public void A_table_nobody_pages_says_so_and_pays_for_no_index()
    {
        Assert.Empty(_model.FindEntityType(typeof(Tag))!.GetIndexes());
    }

    [Fact]
    public void What_the_entity_configures_for_itself_still_happens()
    {
        Assert.Equal(200, Note.FindProperty(nameof(Entities.Note.Title))!.GetMaxLength());
        Assert.Equal(50, _model.FindEntityType(typeof(Tag))!.FindProperty(nameof(Tag.Name))!.GetMaxLength());
    }
}
