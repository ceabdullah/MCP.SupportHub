using System.Text.Json;
using Confluent.Kafka;
using MCP.Itinerary.Models;

namespace MCP.Itinerary.Services;

/// <summary>
/// Background consumer that reads itinerary day events from Kafka and projects them
/// into the Elasticsearch index, keeping search eventually consistent with the store.
/// </summary>
public class KafkaIndexingConsumer(
    IConfiguration config,
    ElasticSearchService elastic,
    ILogger<KafkaIndexingConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

    private async Task ConsumeLoop(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = config["Kafka:GroupId"] ?? "itinerary-indexer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };
        var topic = config["Kafka:Topic"] ?? "itinerary.day-events";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
                consumer.Subscribe(topic);
                logger.LogInformation("Kafka indexing consumer subscribed to {Topic}", topic);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (result is null)
                        continue;

                    await HandleMessageAsync(result.Message.Value, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Kafka consumer error; retrying in 10s");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(string payload, CancellationToken ct)
    {
        try
        {
            var dayEvent = JsonSerializer.Deserialize<ItineraryDayEvent>(payload);
            if (dayEvent is null)
            {
                logger.LogWarning("Skipping unreadable event payload");
                return;
            }
            await elastic.IndexDayAsync(dayEvent.Day, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to index event; message skipped");
        }
    }
}
