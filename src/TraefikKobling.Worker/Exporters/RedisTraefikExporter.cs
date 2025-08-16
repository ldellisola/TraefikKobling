using StackExchange.Redis;
using TraefikKobling.Worker.Extensions;

namespace TraefikKobling.Worker.Exporters;

public class RedisTraefikExporter : ITraefikExporter
{
    private readonly IConnectionMultiplexer _redis;

    public RedisTraefikExporter(ILogger<RedisTraefikExporter> logger, IConnectionMultiplexer redis)
    {
        _redis = redis;
        logger.LogInformation("Exporting Traefik configuration to redis");
    }

    public async Task ExportTraefikEntries(IDictionary<string, string> oldEntries, IDictionary<string, string> newEntries, CancellationToken cancellationToken)
    {
        var entriesToRemove = oldEntries.Keys.Except(newEntries.Keys);

        var db = _redis.GetDatabase();

        foreach (var key in entriesToRemove)
        {
            await db.KeyDeleteAsync(key);
        }

        foreach (var (key, value) in newEntries)
        {
            await db.StringUpdateIfChanged(key, value);
        }
    }
}