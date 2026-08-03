using System.Net;
using System.Text;
using System.Text.Json;
using Api.RateLimiting;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api;

public class RealtimeSessionFunction
{
    /// <summary>Max realtime conversation length; enforced client-side once connected, since the
    /// browser talks WebRTC directly to Azure AI Foundry and this Function never sees the media stream.</summary>
    public const int MaxSessionSeconds = 180;

    private readonly ILogger<RealtimeSessionFunction> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly SessionRateLimiter _rateLimiter;

    public RealtimeSessionFunction(
        ILogger<RealtimeSessionFunction> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        SessionRateLimiter rateLimiter)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _rateLimiter = rateLimiter;
    }

    [Function("realtime-session")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "realtime-session")] HttpRequestData req)
    {
        var clientIp = GetClientIp(req);
        var decision = await _rateLimiter.EvaluateAsync(clientIp);
        if (!decision.Allowed)
        {
            _logger.LogWarning("Realtime session request capped for {ClientIp}: {Reason}", clientIp, decision.Reason);
            var cappedResponse = req.CreateResponse((HttpStatusCode)429);
            await cappedResponse.WriteAsJsonAsync(new
            {
                error = "capped",
                message = "You've reached today's limit for live conversations — check back tomorrow, or browse the Experience/Projects pages.",
            });
            return cappedResponse;
        }

        var endpoint = _configuration["Foundry:Endpoint"];
        var apiKey = _configuration["Foundry:ApiKey"];
        var deployment = _configuration["Foundry:Deployment"] ?? "gpt-realtime-mini";
        var apiVersion = _configuration["Foundry:ApiVersion"] ?? "2025-04-01-preview";
        var region = _configuration["Foundry:Region"] ?? "eastus2";
        var voice = _configuration["Foundry:Voice"] ?? "verse";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("Foundry:Endpoint or Foundry:ApiKey is not configured.");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Realtime session service is not configured." });
            return errorResponse;
        }

        var client = _httpClientFactory.CreateClient();
        var sessionUrl = $"{endpoint.TrimEnd('/')}/openai/realtimeapi/sessions?api-version={apiVersion}";

        using var upstreamRequest = new HttpRequestMessage(HttpMethod.Post, sessionUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { model = deployment, voice }),
                Encoding.UTF8,
                "application/json")
        };
        upstreamRequest.Headers.Add("api-key", apiKey);

        using var upstreamResponse = await client.SendAsync(upstreamRequest);
        var upstreamBody = await upstreamResponse.Content.ReadAsStringAsync();

        if (!upstreamResponse.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Foundry realtime session mint failed: {StatusCode} {Body}",
                upstreamResponse.StatusCode,
                upstreamBody);
            var errorResponse = req.CreateResponse(HttpStatusCode.BadGateway);
            await errorResponse.WriteAsJsonAsync(new { error = "Failed to mint realtime session token." });
            return errorResponse;
        }

        using var upstreamJson = JsonDocument.Parse(upstreamBody);
        var clientSecret = upstreamJson.RootElement.GetProperty("client_secret");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            value = clientSecret.GetProperty("value").GetString(),
            expiresAt = clientSecret.GetProperty("expires_at").GetInt64(),
            endpoint,
            deployment,
            // The WebRTC connect URL is a region-specific host (<region>.realtimeapi-preview.ai.azure.com),
            // independent of the Foundry resource's own endpoint, so the client needs it separately.
            region,
            maxSessionSeconds = MaxSessionSeconds
        });
        return response;
    }

    /// <summary>
    /// Azure Static Web Apps / Functions front doors set X-Forwarded-For to "client, proxy1, proxy2...";
    /// the first entry is the original client.
    /// </summary>
    private static string GetClientIp(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Forwarded-For", out var forwardedFor))
        {
            var first = forwardedFor.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first.Split(',')[0].Trim();
            }
        }

        return "unknown";
    }
}
