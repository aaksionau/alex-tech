namespace Api.RateLimiting;

public enum RateLimitReason
{
    None,
    PerIpCapExceeded,
    SiteWideCapExceeded,
}

public sealed record RateLimitDecision(bool Allowed, RateLimitReason Reason)
{
    public static RateLimitDecision Allow() => new(true, RateLimitReason.None);

    public static RateLimitDecision Deny(RateLimitReason reason) => new(false, reason);
}
