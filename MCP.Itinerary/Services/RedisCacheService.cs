using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace MCP.Itinerary.Services;

/// <summary>
/// Cache-aside wrapper over IDistributedCache (Redis). If Redis is unreachable the
/// factory is invoked directly so the site keeps working without the cache.
/// </summary>
public class RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
{
    private static readonly DistributedCacheEntryOptions DefaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public async Task<T> GetOrSetAsync<T>(string key, Func<T> factory, CancellationToken ct = default)
    {
        try
        {
            var cached = await cache.GetStringAsync(key, ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<T>(cached)!;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis read failed for {Key}; falling back to store", key);
            return factory();
        }

        var value = factory();
        try
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(value), DefaultOptions, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis write failed for {Key}", key);
        }
        return value;
    }

    public async Task InvalidateAsync(params string[] keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await cache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis invalidation failed for {Key}", key);
            }
        }
    }
}
