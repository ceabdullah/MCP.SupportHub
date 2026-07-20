using System.Collections.Concurrent;
using System.Text.Json;
using MCP.Itinerary.Models;

namespace MCP.Itinerary.Services;

/// <summary>
/// Source of truth for itinerary days. Seeded from seed/itinerary.json at startup.
/// Reads are served through the Redis cache (see RedisCacheService); writes publish
/// a Kafka event that the Elasticsearch indexing consumer picks up.
/// </summary>
public class ItineraryStore
{
    private readonly ConcurrentDictionary<int, ItineraryDay> _days = new();

    public ItineraryStore(IWebHostEnvironment env, ILogger<ItineraryStore> logger)
    {
        var seedPath = Path.Combine(env.ContentRootPath, "seed", "itinerary.json");
        if (File.Exists(seedPath))
        {
            var days = JsonSerializer.Deserialize<List<ItineraryDay>>(
                File.ReadAllText(seedPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            foreach (var day in days)
                _days[day.DayNumber] = day;
            logger.LogInformation("Seeded {Count} itinerary days from {Path}", days.Count, seedPath);
        }
        else
        {
            logger.LogWarning("Seed file not found at {Path}; starting with an empty itinerary", seedPath);
        }
    }

    public IReadOnlyList<ItineraryDay> GetAll() =>
        _days.Values.OrderBy(d => d.DayNumber).ToList();

    public ItineraryDay? Get(int dayNumber) =>
        _days.TryGetValue(dayNumber, out var day) ? day : null;

    public ItineraryDay Upsert(ItineraryDay day)
    {
        _days[day.DayNumber] = day;
        return day;
    }

    public IReadOnlyList<ItineraryDay> Search(string query)
    {
        var q = query.Trim();
        return _days.Values
            .Where(d =>
                d.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.Location.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.Summary.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.Highlights.Any(h => h.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(d => d.DayNumber)
            .ToList();
    }
}
