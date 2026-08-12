# i26.Core

The primitives a domain project can hold without taking a web stack or an ORM with it. This package
references **nothing outside the BCL**, by design and enforced — everything that needs a dependency
lives in a package named after it.

```bash
dotnet add package i26.Core
```

## What is in here

**Typed identifiers** in the [TypeID](https://github.com/jetify-com/typeid) format — a prefix that
names the entity and a UUIDv7 that sorts by creation. Declare one with an attribute; the generator
that ships inside this package writes the rest.

```csharp
[TypedId("crs")]
public readonly partial record struct CourseId;

var id = CourseId.New();          // crs_01h455vb4pex5vsknk084sn02q
```

**A result pattern** that carries a business failure as a value, with an error identified by a code
rather than by a message, so the text is resolved at the boundary and never in the domain.

```csharp
public Result<Course> Publish() =>
    IsPublished ? CourseErrors.AlreadyPublished : this;
```

**Cursor pagination** — the request, the page, the cursor and the keyset predicate. Paging over an
`IQueryable` is here too, over an `IAsyncQueryExecutor`, so an application layer pages without
referencing an ORM.

**Domain events** — `IDomainEvent`, its handler, the `IHasDomainEvents` an entity implements with a
list and two members, and the queue that sits between collecting them and publishing them. No base
entity to inherit from.

**Specifications** — one rule, asked of a row in memory and of a table in SQL, and `Where`/`WhereIf`
to apply it.

**The query seam** — `IAsyncQueryExecutor`, two methods, so `ToListAsync` stops being the reason an
application layer references Entity Framework.

## What it drags in

Nothing. That is the point of this one.

## Documentation

The [README of the repository](https://github.com/tiago-bitten/i26#readme) documents all of it with
examples: [typed identifiers](https://github.com/tiago-bitten/i26#typed-identifiers),
[the result pattern](https://github.com/tiago-bitten/i26#result-pattern),
[domain events](https://github.com/tiago-bitten/i26#domain-events),
[specifications](https://github.com/tiago-bitten/i26#specifications),
[awaiting a query](https://github.com/tiago-bitten/i26#awaiting-a-query-without-an-orm) and
[cursor pagination](https://github.com/tiago-bitten/i26#cursor-pagination).
