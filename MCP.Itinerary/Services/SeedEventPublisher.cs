using MCP.Itinerary.Models;

namespace MCP.Itinerary.Services;

/// <summary>
/// On startup, publishes every seeded itinerary day to Kafka so the indexing
/// consumer populates Elasticsearch without any manual step.
/// </summary>
public class SeedEventPublisher(ItineraryStore store, KafkaEventPublisher publisher) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Runs after startup so a slow or absent Kafka broker never delays serving pages.
        await Task.Yield();
        foreach (var day in store.GetAll())
        {
            await publisher.PublishAsync(new ItineraryDayEvent
            {
                EventType = "day-seeded",
                Day = day
            }, stoppingToken);
        }
    }
}
