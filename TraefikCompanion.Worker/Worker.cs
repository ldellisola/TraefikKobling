using System.Net.Http.Json;
using StackExchange.Redis;
using Traefik.Contracts.TcpConfiguration;

namespace TraefikCompanion.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly HttpClient _httpClient;
    private const string _service = "remote";

    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis;
        _httpClient = httpClientFactory.CreateClient("home");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
            
            var response = await _httpClient.GetAsync("api/http/routers", stoppingToken);
            var content = await response.Content.ReadFromJsonAsync<Traefik.Contracts.TcpConfiguration.TcpRouter[]>(cancellationToken: stoppingToken);

            var db = _redis.GetDatabase();

            db.StringSet($"traefik/http/services/{_service}/loadbalancer/servers/0/url", "http://127.0.0.1:8000");
            
            foreach(var router in content ?? Enumerable.Empty<TcpRouter>())
            {
                var name = router.Service;
                
                if (name.Contains('@'))
                    continue;
                
                db.StringSet($"traefik/http/routers/{name}/rule", router.Rule);
                db.StringSet($"traefik/http/routers/{name}/service", _service);
                db.StringSet($"traefik/http/routers/{name}/tls/passthrough", true);
            }
            
            
            await Task.Delay(1000, stoppingToken);
        }
    }
}
