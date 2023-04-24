using StackExchange.Redis;

namespace TraefikKobling.Worker.Extensions;

internal static class RedisExtensions
{
    public static async Task StringUpdateIfChanged(this IDatabase database, string key, string value)
    {
        var currentValue =  await database.StringGetAsync(key);
        
        if (!currentValue.HasValue || currentValue != value)
        {
            await database.StringSetAsync(key, value, when: When.Always);
        }
    }

    public static void FlushDatabase(this ConnectionMultiplexer redis, string connectionString)
    {
        var server = redis.GetServer(connectionString);
        var database = redis.GetDatabase();
        foreach (var key in server.Keys())
        {
            database.KeyDelete(key);
        }
    }
}