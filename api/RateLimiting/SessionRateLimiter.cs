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
    /// Checks run in order and stop at the first exceeded cap, so a client already denied by its own
    /// IP quota never consumes site-wide quota too.
    /// </summary>
    public async Task<RateLimitDecision> EvaluateAsync(string clientIp, CancellationToken cancellationToken = default)
    {
        var partitionKey = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd");

        var caps = new (string RowKey, int Cap, RateLimitReason Reason)[]
        {
            (IpRowKeyPrefix + clientIp, _perIpDailyCap, RateLimitReason.PerIpCapExceeded),
            (SiteWideRowKey, _siteWideDailyCap, RateLimitReason.SiteWideCapExceeded),
        };

        foreach (var (rowKey, cap, reason) in caps)
        {
            var count = await _store.IncrementAsync(partitionKey, rowKey, cancellationToken);
            if (count > cap)
            {
                return RateLimitDecision.Deny(reason);
            }
        }

        return RateLimitDecision.Allow();
    }
}
