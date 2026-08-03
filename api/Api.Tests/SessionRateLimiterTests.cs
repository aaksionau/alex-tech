using Api.RateLimiting;

namespace Api.Tests;

public class SessionRateLimiterTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2 = new(2026, 8, 4, 0, 30, 0, TimeSpan.Zero);

    private static SessionRateLimiter CreateLimiter(
        InMemoryUsageCounterStore store,
        ManualTimeProvider clock,
        int perIpDailyCap = 3,
        int siteWideDailyCap = 10)
        => new(store, clock, perIpDailyCap, siteWideDailyCap);

    [Fact]
    public async Task Allows_requests_up_to_and_including_the_per_ip_cap()
    {
        var store = new InMemoryUsageCounterStore();
        var clock = new ManualTimeProvider(Day1);
        var limiter = CreateLimiter(store, clock, perIpDailyCap: 3, siteWideDailyCap: 100);

        for (var i = 0; i < 3; i++)
        {
            var decision = await limiter.EvaluateAsync("1.2.3.4");
            Assert.True(decision.Allowed, $"attempt {i + 1} should be allowed");
        }
    }

    [Fact]
    public async Task Denies_the_request_one_over_the_per_ip_cap()
    {
        var store = new InMemoryUsageCounterStore();
        var clock = new ManualTimeProvider(Day1);
        var limiter = CreateLimiter(store, clock, perIpDailyCap: 3, siteWideDailyCap: 100);

        for (var i = 0; i < 3; i++)
        {
            await limiter.EvaluateAsync("1.2.3.4");
        }

        var decision = await limiter.EvaluateAsync("1.2.3.4");

        Assert.False(decision.Allowed);
        Assert.Equal(RateLimitReason.PerIpCapExceeded, decision.Reason);
    }

    [Fact]
    public async Task Allows_requests_up_to_and_including_the_site_wide_cap_across_distinct_ips()
    {
        var store = new InMemoryUsageCounterStore();
        var clock = new ManualTimeProvider(Day1);
        var limiter = CreateLimiter(store, clock, perIpDailyCap: 100, siteWideDailyCap: 10);

        for (var i = 0; i < 10; i++)
        {
            var decision = await limiter.EvaluateAsync($"10.0.0.{i}");
            Assert.True(decision.Allowed, $"attempt {i + 1} should be allowed");
        }
    }

    [Fact]
    public async Task Denies_the_request_one_over_the_site_wide_cap_even_from_a_fresh_ip()
    {
        var store = new InMemoryUsageCounterStore();
        var clock = new ManualTimeProvider(Day1);
        var limiter = CreateLimiter(store, clock, perIpDailyCap: 100, siteWideDailyCap: 10);

        for (var i = 0; i < 10; i++)
        {
            await limiter.EvaluateAsync($"10.0.0.{i}");
        }

        var decision = await limiter.EvaluateAsync("10.0.0.99");

        Assert.False(decision.Allowed);
        Assert.Equal(RateLimitReason.SiteWideCapExceeded, decision.Reason);
    }

    [Fact]
    public async Task Per_ip_cap_takes_precedence_when_both_limits_would_be_exceeded()
    {
        var store = new InMemoryUsageCounterStore();
        var clock = new ManualTimeProvider(Day1);
        var limiter = CreateLimiter(store, clock, perIpDailyCap: 2, siteWideDailyCap: 2);

        await limiter.EvaluateAsync("1.2.3.4");
        await limiter.EvaluateAsync("1.2.3.4");

        var decision = await limiter.EvaluateAsync("1.2.3.4");

        Assert.False(decision.Allowed);
        Assert.Equal(RateLimitReason.PerIpCapExceeded, decision.Reason);
    }

    [Fact]
    public async Task Counters_reset_at_utc_day_rollover()
    {
        var store = new InMemoryUsageCounterStore();
        var clock = new ManualTimeProvider(Day1);
        var limiter = CreateLimiter(store, clock, perIpDailyCap: 1, siteWideDailyCap: 1);

        var firstDayDecision = await limiter.EvaluateAsync("1.2.3.4");
        Assert.True(firstDayDecision.Allowed);

        var stillFirstDayDecision = await limiter.EvaluateAsync("1.2.3.4");
        Assert.False(stillFirstDayDecision.Allowed);

        clock.Set(Day2);

        var nextDayDecision = await limiter.EvaluateAsync("1.2.3.4");
        Assert.True(nextDayDecision.Allowed, "the cap should reset once the UTC day rolls over");
    }

    [Fact]
    public async Task Different_ips_are_tracked_independently()
    {
        var store = new InMemoryUsageCounterStore();
        var clock = new ManualTimeProvider(Day1);
        var limiter = CreateLimiter(store, clock, perIpDailyCap: 1, siteWideDailyCap: 100);

        var first = await limiter.EvaluateAsync("1.2.3.4");
        var second = await limiter.EvaluateAsync("5.6.7.8");

        Assert.True(first.Allowed);
        Assert.True(second.Allowed);
    }
}
