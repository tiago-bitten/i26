using System.Collections.Concurrent;
using System.Linq.Expressions;
using i26.Core.DomainEvents;
using Microsoft.Extensions.DependencyInjection;

namespace i26.Cqrs;

/// <summary>Runs the handlers of each event in the scope that published it.</summary>
internal sealed class InProcessDomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    // Keyed on the runtime type of the event, which is what the closed handler interface is built
    // from. Static: the reflection behind a domain event is the same for the life of the process.
    private static readonly ConcurrentDictionary<Type, Handling> Handlings = new();

    public async Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            var handling = Handlings.GetOrAdd(domainEvent.GetType(), Handling.For);

            foreach (var handler in serviceProvider.GetServices(handling.HandlerType))
            {
                if (handler is not null)
                {
                    await handling.Invoke(handler, domainEvent, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>What it takes to reach the handlers of one event type.</summary>
    private sealed record Handling(Type HandlerType, Func<object, IDomainEvent, CancellationToken, Task> Invoke)
    {
        public static Handling For(Type domainEventType)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEventType);

            // A compiled call rather than MethodInfo.Invoke: reflection would wrap whatever a
            // handler throws in a TargetInvocationException, and the caller wants its own exception.
            var handler = Expression.Parameter(typeof(object), "handler");
            var domainEvent = Expression.Parameter(typeof(IDomainEvent), "domainEvent");
            var cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var call = Expression.Call(
                Expression.Convert(handler, handlerType),
                handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!,
                Expression.Convert(domainEvent, domainEventType),
                cancellationToken);

            var invoke = Expression
                .Lambda<Func<object, IDomainEvent, CancellationToken, Task>>(
                    call, handler, domainEvent, cancellationToken)
                .Compile();

            return new Handling(handlerType, invoke);
        }
    }
}
