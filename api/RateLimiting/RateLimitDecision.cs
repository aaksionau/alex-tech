namespace Api.RateLimiting;

public enum RateLimitReason
{
    None,
    PerIpCapExceeded,
    SiteWideCapExceeded,
}

public sealed record RateLimitDecision(RateLimitReason Reason)
{
    public bool Allowed => Reason == RateLimitReason.None;

    public static RateLimitDecision Allow() => new(RateLimitReason.None);

    public static RateLimitDecision Deny(RateLimitReason reason) => new(reason);
}
