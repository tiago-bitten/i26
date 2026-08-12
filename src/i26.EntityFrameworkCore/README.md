# i26.EntityFrameworkCore

Everything the i26 primitives need from Entity Framework, so that the layer holding them does not
need Entity Framework.

```bash
dotnet add package i26.EntityFrameworkCore
```

## What is in here

**Typed id conventions.** One call maps every `[TypedId]` in an assembly to its column — converter,
comparer, and the `"C"` collation that keeps a text id sorting by its bytes on Postgres.

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ApplyTypedIdConventions(typeof(Course).Assembly);
```

**Cursor paging over an `IQueryable`**, ordered for you, answering with a `Result` so a cursor that
did not come from this API reaches the caller as a 400 rather than a 500.

```csharp
var page = await db.Courses
    .Where(course => course.TenantId == tenantId)
    .Select(course => new CourseRow { Id = course.Id, CreatedAt = course.CreatedAt })
    .ToPagedResponseAsync<CourseRow, CourseId>(request, cancellationToken: ct);
```

**Domain event collection.** A `SaveChanges` interceptor takes the events off the entities as they
are saved and publishes them once the save succeeded — unless a transaction is open, in which case
whoever began it publishes after committing.

```csharp
services.AddDbContext<AppDbContext>((provider, options) => options
    .UseNpgsql(connectionString)
    .UseDomainEvents(provider));
```

**The async query backend.** Teaches `IAsyncQueryExecutor` how to await an Entity Framework query,
which is what lets an application layer write LINQ and await it with no reference to this package.

```csharp
services.AddEfCoreAsyncQueries();
```

## What it drags in

`Microsoft.EntityFrameworkCore.Relational`, and i26.Core. Pinned per target framework: 8.0.x on
net8.0, 9.0.x on net9.0, 10.0.x on net10.0.

## Documentation

[Typed ids in Entity Framework Core](https://github.com/tiago-bitten/i26#entity-framework-core),
[cursor pagination](https://github.com/tiago-bitten/i26#cursor-pagination),
[domain events](https://github.com/tiago-bitten/i26#domain-events) and
[awaiting a query without an ORM](https://github.com/tiago-bitten/i26#awaiting-a-query-without-an-orm).
