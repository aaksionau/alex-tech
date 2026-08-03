namespace Api.RateLimiting;

/// <summary>
/// Resolves the originating client IP from a request's X-Forwarded-For header, which Azure
/// Static Web Apps / Functions front doors set as "client, proxy1, proxy2..." — the first
/// entry is the original client.
/// </summary>
public static class ClientIpResolver
{
    public static string Resolve(IEnumerable<string>? forwardedForValues)
    {
        var first = forwardedForValues?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
        {
            return "unknown";
        }

        return first.Split(',')[0].Trim();
    }
}
