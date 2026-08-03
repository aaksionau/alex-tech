using Api.RateLimiting;

namespace Api.Tests;

/// <summary>In-memory stand-in for Azure Table Storage, sufficient to test rate-limiter decisions
/// without needing Azurite or a live storage account.</summary>
public sealed class InMemoryUsageCounterStore : IUsageCounterStore
{
    private readonly Dictionary<(string PartitionKey, string RowKey), long> _counts = new();

    public Task<long> IncrementAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var key = (partitionKey, rowKey);
        var next = _counts.GetValueOrDefault(key) + 1;
        _counts[key] = next;
        return Task.FromResult(next);
    }
}
