using Api.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IUsageCounterStore, TableUsageCounterStore>();
        services.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var perIpDailyCap = configuration.GetValue("RateLimit:PerIpDailyCap", 3);
            var siteWideDailyCap = configuration.GetValue("RateLimit:SiteWideDailyCap", 10);
            return new SessionRateLimiter(
                provider.GetRequiredService<IUsageCounterStore>(),
                provider.GetRequiredService<TimeProvider>(),
                perIpDailyCap,
                siteWideDailyCap);
        });
    })
    .Build();

host.Run();
