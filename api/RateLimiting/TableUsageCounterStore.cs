using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace Api.RateLimiting;

/// <summary>
/// Azure Table Storage never exposes an atomic increment, so this retries a
/// read-modify-write with optimistic concurrency (ETag) until it wins the race.
/// </summary>
public sealed class TableUsageCounterStore : IUsageCounterStore
{
    private const string TableName = "SessionUsageCounters";
    private const string CountPropertyName = "Count";

    private readonly TableServiceClient _serviceClient;
    private readonly Lazy<Task<TableClient>> _tableClient;

    public TableUsageCounterStore(IConfiguration configuration)
    {
        var connectionString = configuration["AzureBlogStorage"]
            ?? throw new InvalidOperationException("AzureBlogStorage is not configured.");

        _serviceClient = new TableServiceClient(connectionString);
        _tableClient = new Lazy<Task<TableClient>>(EnsureTableExistsAsync);
    }

    private async Task<TableClient> EnsureTableExistsAsync()
    {
        await _serviceClient.CreateTableIfNotExistsAsync(TableName);
        return _serviceClient.GetTableClient(TableName);
    }

    public async Task<long> IncrementAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var tableClient = await _tableClient.Value;

        while (true)
        {
            TableEntity? existing;
            try
            {
                var response = await tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);
                existing = response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                existing = null;
            }

            if (existing is null)
            {
                var newEntity = new TableEntity(partitionKey, rowKey) { [CountPropertyName] = 1L };
                try
                {
                    await tableClient.AddEntityAsync(newEntity, cancellationToken);
                    return 1;
                }
                catch (RequestFailedException ex) when (ex.Status == 409)
                {
                    // Another request created the row first — loop and increment it instead.
                    continue;
                }
            }

            var next = existing.GetInt64(CountPropertyName).GetValueOrDefault() + 1;
            existing[CountPropertyName] = next;
            try
            {
                await tableClient.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Replace, cancellationToken);
                return next;
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                // Lost the race to a concurrent update — retry with the latest value.
            }
        }
    }
}
