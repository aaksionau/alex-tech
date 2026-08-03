namespace Api.RateLimiting;

/// <summary>
/// Durable, atomically-incrementing daily usage counters keyed by an arbitrary partition
/// (typically a UTC day) and row (e.g. a client IP or a site-wide bucket).
/// </summary>
public interface IUsageCounterStore
{
    /// <summary>
    /// Increments the counter identified by <paramref name="partitionKey"/>/<paramref name="rowKey"/>
    /// and returns the value after the increment. Creates the counter starting at 1 if it doesn't exist yet.
    /// </summary>
    Task<long> IncrementAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);
}
