using MCP.Itinerary.Models;
using MCP.Itinerary.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Configuration"] ?? "localhost:6379";
    options.InstanceName = "itinerary:";
});

builder.Services.AddSingleton<ItineraryStore>();
builder.Services.AddSingleton<RedisCacheService>();
builder.Services.AddSingleton<KafkaEventPublisher>();
builder.Services.AddSingleton<ElasticSearchService>();
builder.Services.AddHostedService<KafkaIndexingConsumer>();
builder.Services.AddHostedService<SeedEventPublisher>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/days", async (ItineraryStore store, RedisCacheService cache, CancellationToken ct) =>
    Results.Ok(await cache.GetOrSetAsync("days:all", store.GetAll, ct)));

api.MapGet("/days/{dayNumber:int}", async (int dayNumber, ItineraryStore store, RedisCacheService cache, CancellationToken ct) =>
{
    var day = await cache.GetOrSetAsync($"days:{dayNumber}", () => store.Get(dayNumber), ct);
    return day is null ? Results.NotFound() : Results.Ok(day);
});

api.MapGet("/search", async (string q, ElasticSearchService elastic, ItineraryStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.Ok(new { source = "store", results = store.GetAll() });

    var results = await elastic.SearchAsync(q, ct);
    return results is not null
        ? Results.Ok(new { source = "elasticsearch", results })
        : Results.Ok(new { source = "in-memory-fallback", results = store.Search(q) });
});

api.MapPost("/days", async (ItineraryDay day, ItineraryStore store, RedisCacheService cache, KafkaEventPublisher publisher, CancellationToken ct) =>
{
    if (day.DayNumber <= 0)
        return Results.BadRequest(new { error = "dayNumber must be a positive integer" });

    var saved = store.Upsert(day);
    await cache.InvalidateAsync("days:all", $"days:{day.DayNumber}");
    await publisher.PublishAsync(new ItineraryDayEvent { Day = saved }, ct);
    return Results.Ok(saved);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
