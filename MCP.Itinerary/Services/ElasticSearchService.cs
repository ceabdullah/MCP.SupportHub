using Elastic.Clients.Elasticsearch;
using MCP.Itinerary.Models;

namespace MCP.Itinerary.Services;

/// <summary>
/// Full-text search over itinerary days backed by Elasticsearch. Indexing is driven
/// by the Kafka consumer (KafkaIndexingConsumer); queries fall back to the in-memory
/// store when the cluster is unreachable.
/// </summary>
public class ElasticSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly string _indexName;
    private readonly ILogger<ElasticSearchService> _logger;

    public ElasticSearchService(IConfiguration config, ILogger<ElasticSearchService> logger)
    {
        _logger = logger;
        _indexName = config["Elasticsearch:Index"] ?? "itinerary-days";
        var uri = config["Elasticsearch:Uri"] ?? "http://localhost:9200";
        var settings = new ElasticsearchClientSettings(new Uri(uri))
            .DefaultIndex(_indexName)
            .RequestTimeout(TimeSpan.FromSeconds(5));
        _client = new ElasticsearchClient(settings);
    }

    public async Task IndexDayAsync(ItineraryDay day, CancellationToken ct = default)
    {
        var response = await _client.IndexAsync(day, i => i.Index(_indexName).Id(day.DayNumber), ct);
        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Failed to index day {day.DayNumber}: {response.DebugInformation}");
        _logger.LogInformation("Indexed day {Day} into {Index}", day.DayNumber, _indexName);
    }

    public async Task<IReadOnlyList<ItineraryDay>?> SearchAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.SearchAsync<ItineraryDay>(s => s
                .Indices(_indexName)
                .Size(50)
                .Query(q => q
                    .MultiMatch(m => m
                        .Query(query)
                        .Fields(new[] { "title^3", "location^2", "summary", "highlights" })
                        .Fuzziness(new Fuzziness("AUTO")))), ct);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch query failed: {Info}", response.DebugInformation);
                return null;
            }

            return response.Documents.OrderBy(d => d.DayNumber).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch unreachable; caller should fall back to in-memory search");
            return null;
        }
    }
}
