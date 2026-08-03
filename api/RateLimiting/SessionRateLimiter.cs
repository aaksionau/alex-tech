namespace Api.RateLimiting;

/// <summary>
/// Enforces a per-IP-per-day quota and a site-wide-per-day cap on realtime voice sessions.
/// Both counters live in the same UTC-day partition, so usage resets automatically at day rollover.
/// </summary>
public sealed class SessionRateLimiter
{
    private const string SiteWideRowKey = "site-wide";
    private const string IpRowKeyPrefix = "ip:";

    private readonly IUsageCounterStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly int _perIpDailyCap;
    private readonly int _siteWideDailyCap;

    public SessionRateLimiter(
        IUsageCounterStore store,
        TimeProvider timeProvider,
        int perIpDailyCap,
        int siteWideDailyCap)
    {
        _store = store;
        _timeProvider = timeProvider;
        _perIpDailyCap = perIpDailyCap;
        _siteWideDailyCap = siteWideDailyCap;
    }

    /// <summary>
    /// Records an attempted session for <paramref name="clientIp"/> and reports whether it's allowed.
    /// Counters are incremented regardless of outcome so repeated attempts by a capped-out client
    /// don't need to be tracked separately.
    /// </summary>
    public async Task<RateLimitDecision> EvaluateAsync(string clientIp, CancellationToken cancellationToken = default)
    {
        var partitionKey = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd");

        var ipCount = await _store.IncrementAsync(partitionKey, IpRowKeyPrefix + clientIp, cancellationToken);
        if (ipCount > _perIpDailyCap)
        {
            return RateLimitDecision.Deny(RateLimitReason.PerIpCapExceeded);
        }

        var siteCount = await _store.IncrementAsync(partitionKey, SiteWideRowKey, cancellationToken);
        if (siteCount > _siteWideDailyCap)
        {
            return RateLimitDecision.Deny(RateLimitReason.SiteWideCapExceeded);
        }

        return RateLimitDecision.Allow();
    }
}
