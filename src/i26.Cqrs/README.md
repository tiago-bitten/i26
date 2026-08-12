# i26.Cqrs

Commands, queries and their handlers, with no mediator in the middle. A caller asks the container
for the handler of the exact request it means, which the compiler checks and a reader can follow to
its declaration.

```bash
dotnet add package i26.Cqrs
```

---

## Commands and queries

A request is a record, its handler is a class, and the handler answers with a `Result`. There is no
mediator in the middle: a caller asks the container for the handler of the exact request it means,
which the compiler checks and a reader can follow to its declaration.

```csharp
using i26.Cqrs;

public sealed record PublishCourseCommand(CourseId Id) : ICommand;              // no response
public sealed record CreateCourseCommand(string Title) : ICommand<CourseId>;    // with one
public sealed record GetCourseQuery(CourseId Id) : IQuery<CourseResponse>;      // reads only

internal sealed class PublishCourseHandler(ICourseRepository courses)
    : ICommandHandler<PublishCourseCommand>
{
    public async Task<Result> HandleAsync(PublishCourseCommand command, CancellationToken ct = default)
    {
        var course = await courses.FindAsync(command.Id, ct);

        if (course is null)
        {
            return CourseErrors.NotFound;
        }

        return course.Publish();
    }
}
```

The response type lives on the request, so `ICommandHandler<CreateCourseCommand, CourseId>` and
every call site agree on it without anyone restating it.

### Registration

The library cannot see your handlers, so you call this from wherever the application layer wires
itself up:

```csharp
using i26.Cqrs;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddHandlers(typeof(DependencyInjection).Assembly);
        // validators, decorators, services…

        return services;
    }
}
```

Every handler in the assembly is registered scoped, under the interfaces it implements — internal
and private ones included, since a handler is an implementation detail of the application layer and
has no reason to be public. Endpoints then ask for one directly:

```csharp
[FromServices] ICommandHandler<PublishCourseCommand> handler
```

**Two handlers for one request is refused**, naming both, instead of resolving to whichever was
scanned last. That is the failure mode of copying a handler and forgetting to change the request it
handles, and it is silent everywhere else. Scanning the same assembly twice is harmless.

### Decorators

Nothing here is decorated for you — validation, logging, transactions and caching are decisions
about your application, not about a library. The registration is the plain closed-generic kind, so
[Scrutor](https://github.com/khellang/Scrutor) wraps it the usual way:

```csharp
services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
services.Decorate(typeof(ICommandHandler<>), typeof(LoggingDecorator.CommandBaseHandler<>));
```

---

## Domain events

`AddDomainEvents()` registers the queue and a dispatcher that runs the handlers in process, in the
scope that published. The handlers themselves come from `AddHandlers` above, and the events
themselves are [i26.Core](https://github.com/tiago-bitten/i26/blob/main/src/i26.Core/README.md#domain-events).

```csharp
services.AddHandlers(typeof(DependencyInjection).Assembly);
services.AddDomainEvents();
```

Collecting them as entities are saved is
[i26.EntityFrameworkCore](https://github.com/tiago-bitten/i26/blob/main/src/i26.EntityFrameworkCore/README.md#domain-events);
handling them off the request is
[i26.Hosting](https://github.com/tiago-bitten/i26/blob/main/src/i26.Hosting/README.md).

### A dispatcher of your own

`AddDomainEvents` registers one that runs the handlers in process, in the scope that published, and
registers it only if nothing else claimed `IDomainEventDispatcher`. Handing the events to a
background queue instead is a class:

```csharp
internal sealed class BackgroundDomainEventsDispatcher(IBackgroundJobScheduler scheduler)
    : IDomainEventDispatcher
{
    public Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            scheduler.Enqueue<ProcessDomainEventJob>(domainEvent, ct);
        }

        return Task.CompletedTask;
    }
}
```

```csharp
services.AddScoped<IDomainEventDispatcher, BackgroundDomainEventsDispatcher>();
services.AddDomainEvents();
```

---

## What it drags in

`Microsoft.Extensions.DependencyInjection.Abstractions`, and i26.Core.

---

Part of [i26](https://github.com/tiago-bitten/i26#readme).
