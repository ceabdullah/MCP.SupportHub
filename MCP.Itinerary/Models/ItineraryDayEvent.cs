namespace MCP.Itinerary.Models;

/// <summary>
/// Event published to Kafka whenever an itinerary day is created or updated.
/// The indexing consumer projects these events into Elasticsearch.
/// </summary>
public class ItineraryDayEvent
{
    public string EventType { get; set; } = "day-upserted";
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public ItineraryDay Day { get; set; } = new();
}
