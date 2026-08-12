using System.Data;
using System.Globalization;
using Dapper;

namespace i26.Dapper.Tests.Pagination;

/// <summary>
/// Teaches Dapper how SQLite stores the two types the paging cares about.
/// </summary>
/// <remarks>
/// SQLite has neither a uuid nor a timestamp type, so both arrive as text and Dapper has nothing to
/// convert them with. Postgres — what the paging is written for — has both, and Npgsql maps them
/// without any of this. The format matters as much as the parsing: the cursor bound travels as a
/// parameter and is compared against the stored text, so both sides have to be written the same
/// sortable way.
/// </remarks>
internal static class SqliteTypeHandlers
{
    private const string Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    private static bool _registered;

    internal static void Register()
    {
        if (_registered)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new GuidHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        _registered = true;
    }

    internal static string ToText(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);

    private sealed class GuidHandler : SqlMapper.TypeHandler<Guid>
    {
        public override Guid Parse(object value) => value switch
        {
            Guid guid => guid,
            string text => Guid.Parse(text, CultureInfo.InvariantCulture),
            byte[] bytes => new Guid(bytes),
            _ => throw new InvalidCastException($"Cannot read a Guid out of {value?.GetType().Name ?? "null"}."),
        };

        public override void SetValue(IDbDataParameter parameter, Guid value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("D", CultureInfo.InvariantCulture);
        }
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value) => value switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            string text => DateTimeOffset.Parse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            _ => throw new InvalidCastException($"Cannot read a DateTimeOffset out of {value?.GetType().Name ?? "null"}."),
        };

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = ToText(value);
        }
    }
}
