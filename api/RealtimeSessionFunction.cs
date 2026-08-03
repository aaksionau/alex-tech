using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api;

public class RealtimeSessionFunction
{
    private readonly ILogger<RealtimeSessionFunction> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public RealtimeSessionFunction(
        ILogger<RealtimeSessionFunction> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [Function("realtime-session")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "realtime-session")] HttpRequestData req)
    {
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
            region
        });
        return response;
    }
}
