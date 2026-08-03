using Api.RateLimiting;

namespace Api.Tests;

public class ClientIpResolverTests
{
    [Fact]
    public void Returns_unknown_when_header_is_absent()
    {
        Assert.Equal("unknown", ClientIpResolver.Resolve(null));
    }

    [Fact]
    public void Returns_unknown_when_header_values_are_empty()
    {
        Assert.Equal("unknown", ClientIpResolver.Resolve(Array.Empty<string>()));
    }

    [Fact]
    public void Returns_the_single_ip_when_only_one_is_present()
    {
        Assert.Equal("203.0.113.5", ClientIpResolver.Resolve(new[] { "203.0.113.5" }));
    }

    [Fact]
    public void Returns_the_first_ip_and_trims_whitespace_from_a_proxy_chain()
    {
        Assert.Equal("203.0.113.5", ClientIpResolver.Resolve(new[] { "203.0.113.5,  10.0.0.1, 10.0.0.2" }));
    }
}
