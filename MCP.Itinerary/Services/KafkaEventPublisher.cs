using System.Text.Json;
using Confluent.Kafka;
using MCP.Itinerary.Models;

namespace MCP.Itinerary.Services;

/// <summary>
/// Publishes itinerary day events to the Kafka topic configured under Kafka:Topic.
/// Failures are logged but never bubble up to the request pipeline, so the site
/// works even when Kafka is down (search indexing just lags behind).
/// </summary>
public sealed class KafkaEventPublisher : IDisposable
{
    private readonly IProducer<string, string>? _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IConfiguration config, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;
        _topic = config["Kafka:Topic"] ?? "itinerary.day-events";
        var bootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092";
        try
        {
            _producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                MessageTimeoutMs = 5000
            }).Build();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka producer could not be created; events will be dropped");
        }
    }

    public async Task PublishAsync(ItineraryDayEvent dayEvent, CancellationToken ct = default)
    {
        if (_producer is null)
            return;

        try
        {
            var message = new Message<string, string>
            {
                Key = dayEvent.Day.DayNumber.ToString(),
                Value = JsonSerializer.Serialize(dayEvent)
            };
            var result = await _producer.ProduceAsync(_topic, message, ct);
            _logger.LogInformation("Published {EventType} for day {Day} to {TopicPartitionOffset}",
                dayEvent.EventType, dayEvent.Day.DayNumber, result.TopicPartitionOffset);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish event for day {Day}", dayEvent.Day.DayNumber);
        }
    }

    public void Dispose() => _producer?.Dispose();
}
