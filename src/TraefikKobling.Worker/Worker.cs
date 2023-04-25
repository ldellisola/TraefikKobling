using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Traefik.Contracts.TcpConfiguration;
using TraefikKobling.Worker.Configuration;
using TraefikKobling.Worker.Extensions;
using Server = TraefikKobling.Worker.Configuration.Server;

namespace TraefikKobling.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConnectionMultiplexer _redis;
    
    private readonly Server[] _servers;
    private readonly Dictionary<string, HttpClient> _clients = new();
    private readonly int _runEvery;
    
    private readonly IDictionary<string,string> _oldEntries = new Dictionary<string, string>();

    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory, IConnectionMultiplexer redis, KoblingOptions options)
    {
        _logger = logger;
        _redis = redis;
        _runEvery = options.RunEvery ?? 60;
        _servers = options.Servers;

        foreach (var server in _servers)
        {
            var client = httpClientFactory.CreateClient(server.Name);
            
            _clients.Add(server.Name, client);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
            
            var entries = new Dictionary<string, string>();
            foreach (var server in _servers)
            {
                try
                {
                    entries.Merge(await GetHttpEntries(server, stoppingToken));
                    entries.Merge(await GetTcpEntries(server, stoppingToken));
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not generate redis entries for {Server}", server.Name);
                }
            }
            
            var entriesToRemove = _oldEntries.Keys.Except(entries.Keys);
            
            var db = _redis.GetDatabase();

            foreach (var key in entriesToRemove)
            {
                await db.KeyDeleteAsync(key);
            }
            
            foreach (var (key, value) in entries)
            {
                await db.StringUpdateIfChanged(key, value);
            }

            _oldEntries.Clear();
            _oldEntries.Merge(entries);
            
            
            await Task.Delay(_runEvery * 1000, stoppingToken);
        }
    }
    
    private async Task<IDictionary<string,string>> GetTcpEntries(Server server, CancellationToken token)
    {
        return await GetEntries("tcp","api/tcp/routers", server, token);
    }
    private async Task<IDictionary<string,string>> GetHttpEntries(Server server, CancellationToken token)
    {
        return await GetEntries("http","api/http/routers", server, token);
    }

    private async Task<IDictionary<string, string>> GetEntries(string protocol, string endpoint, Server server, CancellationToken token)
    {
        var entries = new Dictionary<string, string>();
        
        _logger.LogInformation("Attempting to retrieve {Protocol} routers from {Server}",protocol, server.Name);
        var response = await _clients[server.Name].GetAsync(endpoint, token);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Could not connect to {Server}", server.Name);
            return entries;
        }
        
        _logger.LogInformation("Successfully connected to {Server}", server.Name);
        
        _logger.LogInformation("Retrieving {Protocol} routers from {Server}", protocol, server.Name);
        var routers = await response.Content.ReadFromJsonAsync<TcpRouter[]>(cancellationToken: token);

        if (routers is null)
        {
            _logger.LogError("Could not get {Protocol} routers from {Server}",protocol, server.Name);
            return entries;
        }

        if (routers.IsEmpty())
        {
            _logger.LogInformation("No {Protocol} routers found on {Server}",protocol, server.Name);
            return entries;
        }
        
        _logger.LogInformation("Successfully retrieved {Number} {Protocl} routers from {Server}",routers.Length,protocol, server.Name);
        
        entries[$"traefik/{protocol}/services/{server.Name}/loadbalancer/servers/0/url"] = server.DestinationAddress.ToString();
        
        foreach (var router in routers)
        {
            var name = router.Service;

            if (name.Contains('@'))
                name = $"{name.Split('@')[0]}_{server.Name}";

            var registeredEntryPoints = 0;
            foreach (var (global,local) in server.EntryPoints)
            {
                if (router.EntryPoints.Any(t=> t == local))
                    entries[$"traefik/{protocol}/routers/{name}/entrypoints/{registeredEntryPoints++}"] = global;
            }

            if (registeredEntryPoints > 0)
            {
                entries[$"traefik/{protocol}/routers/{name}/rule"] = router.Rule;
                entries[$"traefik/{protocol}/routers/{name}/service"] = server.Name;
            }
        }

        return entries;
    }
}
