# MCP.Itinerary

A travel itinerary site built on **.NET 10** that shows each day of a trip with a picture
and a summary, wired into **Kafka**, **Elasticsearch**, and **Redis**.

## Architecture

```
Browser ──▶ ASP.NET Core minimal API (.NET 10)
              │
              ├─ GET /api/days ───── Redis (cache-aside, 5 min TTL)
              │                        │ miss
              │                        ▼
              │                     ItineraryStore (seeded from seed/itinerary.json)
              │
              ├─ POST /api/days ──▶ store upsert
              │                        ├─ Redis cache invalidation
              │                        └─ Kafka producer ──▶ topic `itinerary.day-events`
              │                                                  │
              │                             KafkaIndexingConsumer (BackgroundService)
              │                                                  │
              │                                                  ▼
              └─ GET /api/search ◀──────────────────── Elasticsearch `itinerary-days`
```

- **Redis** caches day reads (cache-aside with invalidation on write).
- **Kafka** carries `day-upserted` / `day-seeded` events; on startup every seeded day is
  published so Elasticsearch fills itself with no manual step.
- **Elasticsearch** powers fuzzy full-text search over titles, locations, summaries, and
  highlights.
- Every dependency degrades gracefully: with Redis/Kafka/Elasticsearch down, the site
  still renders and search falls back to an in-memory scan (the UI shows which backend
  answered).

## Run everything with Docker

```bash
cd MCP.Itinerary
docker compose up --build
```

Then open http://localhost:8080.

## Run just the app (no infrastructure)

```bash
cd MCP.Itinerary
dotnet run
```

The site works fully; search responses report `in-memory-fallback` instead of
`elasticsearch`.

## API

| Method | Route                  | Description                                    |
|--------|------------------------|------------------------------------------------|
| GET    | `/api/days`            | All days (Redis-cached)                        |
| GET    | `/api/days/{n}`        | One day (Redis-cached)                         |
| GET    | `/api/search?q=...`    | Full-text search (Elasticsearch, with fallback)|
| POST   | `/api/days`            | Upsert a day → invalidates cache, emits Kafka event |
| GET    | `/health`              | Liveness probe                                 |
