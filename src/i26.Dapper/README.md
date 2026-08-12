# i26.Dapper

The same cursor pages and the same typed ids as the rest of i26, over a query you wrote by hand.

```bash
dotnet add package i26.Dapper
```

---

## Typed identifiers

Dapper keeps its type handlers in static state, so this is one call at startup and never again —
after it, a `CourseId` is a parameter and a column like any other.

```csharp
TypedIdDapperExtensions.AddTypedIdHandlers(typeof(Course).Assembly);
```

---

## Cursor pagination

Typed ids need one registration, at startup, since Dapper keeps its handlers in static state:

```csharp
TypedIdDapperExtensions.AddTypedIdHandlers(typeof(CourseId).Assembly);
```

Without it, a query selecting an id column into a typed id property fails while materializing —
Dapper has no conversion to fall back on. Paging is the same cursor and the same response as the
ORM side, for the query that outgrew it:

```csharp
using i26.Dapper.Pagination;

var page = await connection.ToPagedResponseAsync<CourseRow, CourseId>(
    """
    SELECT c."Id", c."Title", c."CreatedAt"
    FROM courses c
    JOIN enrollments e ON e."CourseId" = c."Id"
    WHERE c."TenantId" = @TenantId
    """,
    request,
    new { TenantId = tenantId },
    cancellationToken: ct);
```

The registration above is what turns the cursor's id back into the prefixed text the column holds,
so it is not optional once the tie-breaker is a typed id.

Your query is wrapped as a derived table and the keyset predicate, the ordering and the limit go
around it, so it only has to select the two ordering columns and filter. The column names are
arguments — `createdAtColumn`, `idColumn` — because they are written into the statement as
identifiers, which no parameter can stand in for; everything else travels as a parameter.

The paging clause is `LIMIT`, which Postgres, SQLite and MySQL take. On SQL Server, write the outer
query yourself and build the page with `CursorPage.From` — the cursor and the response are the same
either way.

---

## What it drags in

`Dapper`, and i26.Core.

---

Part of [i26](https://github.com/tiago-bitten/i26#readme).
