using Dapper;
using i26.Core.Ids;
using i26.Dapper.Ids;
using Microsoft.Data.Sqlite;

namespace i26.Dapper.Tests.Ids;

/// <summary>
/// Without the handler, a query selecting an id column into a typed id property fails while
/// materializing. These read and write it against a real database.
/// </summary>
public sealed class TypedIdTypeHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public TypedIdTypeHandlerTests()
    {
        TypedIdDapperExtensions.AddTypedIdHandlers(typeof(TypedIdTypeHandlerTests).Assembly);

        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _connection.Execute(
            """
            CREATE TABLE courses (
                "Id"      TEXT NOT NULL PRIMARY KEY,
                "OwnerId" TEXT NULL,
                "Title"   TEXT NOT NULL
            );
            """);
    }

    public void Dispose() => _connection.Dispose();

    private void Insert(CourseId id, TeacherId? ownerId = null, string title = "Algebra") =>
        _connection.Execute(
            """INSERT INTO courses ("Id", "OwnerId", "Title") VALUES (@Id, @OwnerId, @Title);""",
            new { Id = id, OwnerId = ownerId, Title = title });

    [Fact]
    public void An_id_is_written_as_the_prefixed_string()
    {
        var id = CourseId.New();

        Insert(id);

        Assert.Equal(id.ToString(), _connection.QuerySingle<string>("""SELECT "Id" FROM courses"""));
    }

    [Fact]
    public void An_id_is_read_back_into_the_typed_property()
    {
        var id = CourseId.New();

        Insert(id);

        var row = _connection.QuerySingle<CourseRow>("""SELECT "Id", "OwnerId", "Title" FROM courses""");

        Assert.Equal(id, row.Id);
    }

    [Fact]
    public void An_id_is_read_as_a_scalar_too()
    {
        var id = CourseId.New();

        Insert(id);

        Assert.Equal(id, _connection.QuerySingle<CourseId>("""SELECT "Id" FROM courses"""));
    }

    [Fact]
    public void An_id_works_as_a_query_parameter()
    {
        var wanted = CourseId.New();

        Insert(wanted, title: "wanted");
        Insert(CourseId.New(), title: "other");

        var title = _connection.QuerySingle<string>(
            """SELECT "Title" FROM courses WHERE "Id" = @Id""",
            new { Id = wanted });

        Assert.Equal("wanted", title);
    }

    [Fact]
    public void A_nullable_id_reads_back_as_null()
    {
        Insert(CourseId.New());

        var row = _connection.QuerySingle<CourseRow>("""SELECT "Id", "OwnerId", "Title" FROM courses""");

        Assert.Null(row.OwnerId);
    }

    [Fact]
    public void A_nullable_id_reads_back_as_the_id_it_holds()
    {
        var ownerId = TeacherId.New();

        Insert(CourseId.New(), ownerId);

        var row = _connection.QuerySingle<CourseRow>("""SELECT "Id", "OwnerId", "Title" FROM courses""");

        Assert.Equal(ownerId, row.OwnerId);
    }

    [Fact]
    public void An_id_of_another_entity_in_the_column_fails_loudly()
    {
        _connection.Execute(
            """INSERT INTO courses ("Id", "Title") VALUES (@Id, 'wrong');""",
            new { Id = TeacherId.New().ToString() });

        Assert.ThrowsAny<Exception>(
            () => _connection.QuerySingle<CourseRow>("""SELECT "Id", "OwnerId", "Title" FROM courses"""));
    }

    [Fact]
    public void Corrupted_text_in_the_column_fails_loudly()
    {
        _connection.Execute("""INSERT INTO courses ("Id", "Title") VALUES ('not an id', 'wrong');""");

        Assert.ThrowsAny<Exception>(
            () => _connection.QuerySingle<CourseRow>("""SELECT "Id", "OwnerId", "Title" FROM courses"""));
    }

    [Fact]
    public void A_column_that_still_holds_a_raw_uuid_is_read_too()
    {
        var handler = new TypedIdTypeHandler<CourseId>();
        var guid = Uuid7.New();

        Assert.Equal(CourseId.FromGuid(guid), handler.Parse(guid));
    }

    [Fact]
    public void A_column_holding_something_else_says_so()
    {
        var handler = new TypedIdTypeHandler<CourseId>();

        Assert.Throws<InvalidCastException>(() => handler.Parse(42));
    }

    private sealed record CourseRow
    {
        public required CourseId Id { get; init; }

        public TeacherId? OwnerId { get; init; }

        public required string Title { get; init; }
    }
}

public readonly record struct CourseId(Guid Value) : ITypedId<CourseId>
{
    public static string Prefix => "crs";

    public static CourseId FromGuid(Guid value) => new(value);

    public static CourseId New() => TypedId.New<CourseId>();

    public override string ToString() => TypedId.Format(this);

    public static CourseId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<CourseId>(s);

    public static bool TryParse(string? s, IFormatProvider? _, out CourseId result)
        => TypedId.TryParse(s, out result);

    public int CompareTo(CourseId other) => TypedId.Compare(this, other);
}

public readonly record struct TeacherId(Guid Value) : ITypedId<TeacherId>
{
    public static string Prefix => "tch";

    public static TeacherId FromGuid(Guid value) => new(value);

    public static TeacherId New() => TypedId.New<TeacherId>();

    public override string ToString() => TypedId.Format(this);

    public static TeacherId Parse(string s, IFormatProvider? _ = null) => TypedId.Parse<TeacherId>(s);

    public static bool TryParse(string? s, IFormatProvider? _, out TeacherId result)
        => TypedId.TryParse(s, out result);

    public int CompareTo(TeacherId other) => TypedId.Compare(this, other);
}
