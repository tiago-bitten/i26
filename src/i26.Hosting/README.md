# i26.Hosting

Domain events handled off the request, with no Redis, no table and no broker.

```bash
dotnet add package i26.Hosting
```

Running the handlers in process runs them **in the request**: the caller waits for them, and one
that throws surfaces out of whatever published. This is the same handlers, behind an in-memory queue
read by a hosted service.

```csharp
services.AddHandlers(typeof(DependencyInjection).Assembly);
services.AddBackgroundDomainEvents();
```

That is the whole setup. Publishing writes to the queue and returns; the hosted service runs the
handlers one event at a time.

## The part worth knowing

**Each event gets a scope of its own**, because the scope that raised it ended with the request.
That scope has its own database context and no user or tenant resolved in it — whatever a handler
needs to know about who did what, the event has to carry. It is also the decision that survives the
day this becomes a broker: on the other side of a queue there is no scope left to restore.

**The queue is in memory.** Stopping the host stops accepting events and hands over what is already
queued, within whatever `ShutdownTimeout` allows, but what it holds when the process dies is lost.
Fine for a notification or a projection refresh; not for something that has to happen. When it has
to, `IDomainEventDispatcher` is still the seam and an outbox goes behind it.

| | |
| --- | --- |
| `Capacity` | How many events may be waiting. Publishing waits while the queue is full, rather than dropping. Default 1024, `null` for no limit. |
| `Concurrency` | How many are handled at once. Default 1, which keeps them in the order they were raised. |

A handler that throws is logged and the queue keeps moving.

## What it drags in

`Microsoft.Extensions.Hosting.Abstractions` and `Microsoft.Extensions.Logging.Abstractions`, plus
i26.Core and i26.Cqrs.

## Documentation

[Domain events](https://github.com/tiago-bitten/i26#domain-events) in the repository README, and
[handling them in the background](https://github.com/tiago-bitten/i26#handling-them-in-the-background).
