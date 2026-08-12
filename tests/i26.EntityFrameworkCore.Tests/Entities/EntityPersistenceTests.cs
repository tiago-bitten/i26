using i26.Core.DomainEvents;
using i26.Core.Pagination;
using i26.EntityFrameworkCore.DomainEvents;
using i26.EntityFrameworkCore.Entities;
using i26.EntityFrameworkCore.Pagination;
using i26.EntityFrameworkCore.Tests.DomainEvents;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace i26.EntityFrameworkCore.Tests.Entities;

/// <summary>
/// The base entity against a real database, which is where the parts meet: the id is a column, the
/// timestamps are written by something other than the entity, and a delete is an update.
/// </summary>
public sealed class EntityPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly FixedTime _time = new(Noon);
    private readonly RecordingDispatcher _dispatcher = new();
    private readonly DomainEventQueue _queue;

    public EntityPersistenceTests()
    {
        _queue = new DomainEventQueue(_dispatcher);
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private NoteDbContext CreateContext()
    {
        // Two steps rather than one chain: the extensions answer with the builder they were given,
        // which is the untyped one, and DbContextOptions<TContext> is what the constructor wants.
        var builder = new DbContextOptionsBuilder<NoteDbContext>().UseSqlite(_connection);
        builder.UseEntityTimestamps(_time).UseDomainEvents(_queue);

        return new NoteDbContext(builder.Options);
    }

    [Fact]
    public async Task An_entity_round_trips_through_its_typed_id()
    {
        var written = Note.Write("first");

        using (var context = CreateContext())
        {
            context.Notes.Add(written);
            await context.SaveChangesAsync();
        }

        using var reading = CreateContext();
        var read = await reading.Notes.SingleAsync(note => note.Id == written.Id);

        Assert.Equal(written.Id, read.Id);
        Assert.Equal(written, read);
        Assert.Equal("first", read.Title);
    }

    [Fact]
    public async Task Saving_stamps_the_instant_the_row_was_written()
    {
        using var context = CreateContext();
        context.Notes.Add(Note.Write("first"));

        await context.SaveChangesAsync();

        var note = await context.Notes.SingleAsync();
        Assert.Equal(Noon, note.CreatedAt);
        Assert.Equal(Noon, note.UpdatedAt);
    }

    [Fact]
    public async Task Changing_a_row_moves_only_the_one_that_means_changed()
    {
        using var context = CreateContext();
        var note = Note.Write("first");
        context.Notes.Add(note);
        await context.SaveChangesAsync();

        _time.Now = Noon.AddHours(1);
        note.Retitle("second");
        await context.SaveChangesAsync();

        Assert.Equal(Noon, note.CreatedAt);
        Assert.Equal(Noon.AddHours(1), note.UpdatedAt);
    }

    [Fact]
    public async Task Deleting_stamps_when_and_leaves_the_row_where_it_was()
    {
        using var context = CreateContext();
        var note = Note.Write("first");
        context.Notes.Add(note);
        await context.SaveChangesAsync();

        _time.Now = Noon.AddHours(2);
        Assert.True(note.Delete().IsSuccess);
        await context.SaveChangesAsync();

        Assert.Equal(Noon.AddHours(2), note.DeletedAt);
        Assert.Single(await context.Notes.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task A_deleted_row_is_not_there_any_more_as_far_as_a_query_is_concerned()
    {
        using var seeding = CreateContext();
        var note = Note.Write("first");
        seeding.Notes.Add(note);
        seeding.Notes.Add(Note.Write("second"));
        await seeding.SaveChangesAsync();

        note.Delete();
        await seeding.SaveChangesAsync();

        using var reading = CreateContext();

        Assert.Equal(["second"], (await reading.Notes.ToListAsync()).Select(row => row.Title));
        Assert.Equal(2, await reading.Notes.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Restoring_puts_it_back_in_the_queries()
    {
        using var context = CreateContext();
        var note = Note.Write("first");
        context.Notes.Add(note);
        await context.SaveChangesAsync();

        note.Delete();
        await context.SaveChangesAsync();
        Assert.Empty(await context.Notes.ToListAsync());

        Assert.True(note.Restore().IsSuccess);
        await context.SaveChangesAsync();

        Assert.Single(await context.Notes.ToListAsync());
        Assert.Null(note.DeletedAt);
    }

    [Fact]
    public async Task What_the_entity_raised_is_published_by_the_save()
    {
        using var context = CreateContext();
        var note = Note.Write("first");
        context.Notes.Add(note);

        await context.SaveChangesAsync();

        var written = Assert.IsType<NoteWritten>(Assert.Single(_dispatcher.Dispatched));
        Assert.Equal(note.Id, written.Id);
        Assert.Empty(note.DomainEvents);
    }

    [Fact]
    public async Task An_entity_pages_by_cursor_without_a_projection()
    {
        using var context = CreateContext();

        for (var minute = 0; minute < 3; minute++)
        {
            _time.Now = Noon.AddMinutes(minute);
            context.Notes.Add(Note.Write($"note {minute}"));
            await context.SaveChangesAsync();
        }

        // Entity<TId> is ICursorPageable<TId> already, so the entity itself is the row type.
        var page = await context.Notes.ToPagedResponseAsync<Note, NoteId>(
            new CursorPageRequest { Limit = 2, IncludeTotal = true });

        Assert.Equal(["note 2", "note 1"], page.Value.Items.Select(note => note.Title));
        Assert.Equal(3, page.Value.Total);
        Assert.True(page.Value.HasNext);
    }
}
