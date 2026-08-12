# i26.Dapper

The same cursor pages and the same typed ids as the rest of i26, over a query you wrote by hand.

```bash
dotnet add package i26.Dapper
```

## What is in here

**Typed id handlers.** Dapper keeps its type handlers in static state, so this is one call at
startup and never again — after it, a `CourseId` is a parameter and a column like any other.

```csharp
TypedIdDapperExtensions.AddTypedIdHandlers(typeof(Course).Assembly);
```

**Cursor paging over SQL.** You write the query; the extension adds the keyset predicate, the
ordering and the one extra row that answers "is there a next page". No `OFFSET`, so page one and
page nine hundred cost the same.

```csharp
var page = await connection.ToPagedResponseAsync<CourseRow, CourseId>(
    "select id, created_at, title from courses where tenant_id = @tenantId",
    request,
    new { tenantId },
    cancellationToken: ct);
```

The cursor and the `PagedResponse<T>` are the ones from i26.Core, so a Dapper endpoint and an Entity
Framework endpoint page identically and a cursor from one is readable by the other.

## What it drags in

`Dapper`, and i26.Core.

## Documentation

[Cursor pagination with Dapper](https://github.com/tiago-bitten/i26#dapper) and
[typed identifiers](https://github.com/tiago-bitten/i26#typed-identifiers).
