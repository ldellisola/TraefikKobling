using System.Net.Http.Json;
using StackExchange.Redis;

namespace TraefikCompanion.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IRedisAsync _redis;
    private readonly HttpClient _httpClient;

    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory, IRedisAsync redis)
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
            
            var response = await _httpClient.GetAsync("/http/routers", stoppingToken);
            var content = await response.Content.ReadFromJsonAsync<Traefik.Contracts.TcpConfiguration.TcpRouter[]>(cancellationToken: stoppingToken);
            
            content?.ToList().ForEach(t =>
            {
                _logger.LogInformation("Router: {Rule}", t.Rule);
                _logger.LogInformation("Service: {Service}", t.Service);
            });
            
            
            await Task.Delay(1000, stoppingToken);
        }
    }
}
