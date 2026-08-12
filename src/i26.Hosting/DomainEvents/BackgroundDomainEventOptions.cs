namespace i26.Hosting.DomainEvents;

/// <summary>How the background dispatcher is sized.</summary>
public sealed class BackgroundDomainEventOptions
{
    /// <summary>How many events may be waiting. <see langword="null"/> for no limit.</summary>
    /// <remarks>
    /// Publishing waits while the queue is full, so handlers that cannot keep up slow the code that
    /// raised the event down rather than losing what it raised.
    /// </remarks>
    public int? Capacity { get; set; } = 1024;

    /// <summary>How many events are handled at a time.</summary>
    /// <remarks>One, the default, keeps them in the order they were raised.</remarks>
    public int Concurrency { get; set; } = 1;

    internal void Validate()
    {
        if (Capacity is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Capacity), Capacity, "Capacity is a number of events, or null for no limit.");
        }

        if (Concurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Concurrency), Concurrency, "At least one event has to be handled at a time.");
        }
    }
}
