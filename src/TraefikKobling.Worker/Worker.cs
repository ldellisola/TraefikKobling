using System.Net.Http.Json;
using TraefikKobling.Worker.Configuration;
using TraefikKobling.Worker.Exporters;
using TraefikKobling.Worker.Extensions;
using TraefikKobling.Worker.Traefik;
using Server = TraefikKobling.Worker.Configuration.Server;

namespace TraefikKobling.Worker;

public class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    ITraefikExporter exporter,
    KoblingOptions options)
    : BackgroundService
{
    private readonly Server[] _servers = options.Servers;
    private readonly int _runEvery = options.RunEvery ?? 60;

    private readonly IDictionary<string,string> _oldEntries = new Dictionary<string, string>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);

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
                    logger.LogError(e, "Could not generate redis entries for {Server}", server.Name);
                }
            }

            await exporter.ExportTraefikEntries(_oldEntries, entries, stoppingToken);

            _oldEntries.Clear();
            _oldEntries.Merge(entries);
            
            await Task.Delay(_runEvery * 1000, stoppingToken);
        }
    }

    private async Task<IDictionary<string,string>> GetTcpEntries(Server server, CancellationToken token)
    {
        var entries = new Dictionary<string, string>();

        logger.LogInformation("Attempting to retrieve tcp routers from {Server}", server.Name);
        using var client = httpClientFactory.CreateClient(server.Name);
        using var response = await client.GetAsync("api/tcp/routers", token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Could not connect to {Server}", server.Name);
            return entries;
        }

        logger.LogInformation("Successfully connected to {Server}", server.Name);
        logger.LogInformation("Retrieving tcp routers from {Server}", server.Name);
        var routers = await response.Content.ReadFromJsonAsync<TcpRouter[]>(GeneratedJsonInfo.Default.TcpRouterArray, token);

        if (routers is null)
        {
            logger.LogError("Could not get tcp routers from {Server}", server.Name);
            return entries;
        }

        if (routers.IsEmpty())
        {
            logger.LogInformation("No tcp routers found on {Server}", server.Name);
            return entries;
        }

        logger.LogInformation("Successfully retrieved {Number} tcp routers from {Server}",routers.Length, server.Name);

        entries[$"traefik/tcp/services/{server.Name}/loadbalancer/servers/0/url"] = server.DestinationAddress.ToString();

        foreach (var router in routers)
        {
            var name = router.Service;

            if (name.Contains('@'))
                name = $"{name.Split('@')[0]}_{server.Name}";

            var registeredEntryPoints = 0;
            foreach (var (global,local) in server.EntryPoints)
            {
                if (router.EntryPoints.Any(t=> t == local))
                    entries[$"traefik/tcp/routers/{name}/entrypoints/{registeredEntryPoints++}"] = global;
            }

            if (registeredEntryPoints == 0) continue;
            
            entries[$"traefik/tcp/routers/{name}/rule"] = router.Rule;
            entries[$"traefik/tcp/routers/{name}/service"] = server.Name;
            
        }

        return entries;
    }
    private async Task<IDictionary<string,string>> GetHttpEntries(Server server, CancellationToken token)
    {
        var entries = new Dictionary<string, string>();

        logger.LogInformation("Attempting to retrieve http routers from {Server}", server.Name);
        using var client = httpClientFactory.CreateClient(server.Name);
        using var response = await client.GetAsync("api/http/routers", token);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Could not connect to {Server}", server.Name);
            return entries;
        }

        logger.LogInformation("Successfully connected to {Server}", server.Name);
        logger.LogInformation("Retrieving http routers from {Server}", server.Name);
        var routers = await response.Content.ReadFromJsonAsync<HttpRouter[]>(GeneratedJsonInfo.Default.HttpRouterArray,token);

        if (routers is null)
        {
            logger.LogError("Could not get http routers from {Server}", server.Name);
            return entries;
        }

        if (routers.IsEmpty())
        {
            logger.LogInformation("No http routers found on {Server}", server.Name);
            return entries;
        }

        logger.LogInformation("Successfully retrieved {Number} http routers from {Server}",routers.Length, server.Name);
        entries[$"traefik/http/services/{server.Name}/loadbalancer/servers/0/url"] = server.DestinationAddress.ToString();

        var middlewareNames = await GetMiddlewares(server, token);
        var serviceNames = await GetServices(server, token);
        foreach (var router in routers)
        {
            var name = router.Name;

            if (name.Contains('@'))
                name = $"{name.Split('@')[0]}_{server.Name}";

            var registeredEntryPoints = 0;
            foreach (var (global,local) in server.EntryPoints)
            {
                if (router.EntryPoints.Any(t=> t == local))
                    entries[$"traefik/http/routers/{name}/entrypoints/{registeredEntryPoints++}"] = global;
            }

            if (registeredEntryPoints == 0) continue;

            entries[$"traefik/http/routers/{name}/rule"] = router.Rule;
            entries[$"traefik/http/routers/{name}/service"] = server.Name;

            if (server.ForwardServices ?? options.ForwardServices ?? false)
            {
                if (!serviceNames.Contains(router.Name, StringComparer.OrdinalIgnoreCase) &&
                    !serviceNames.Contains(router.Service, StringComparer.OrdinalIgnoreCase))
                {
                    entries[$"traefik/http/routers/{name}/service"] = router.Service;
                }
            }

            if (server.ForwardMiddlewares ?? options.ForwardMiddlewares ?? false)
            {
              var registeredMiddlewares = 0;
              foreach (var middleware in router.Middlewares)
              {
                if (middlewareNames.Any(t=> t == middleware)) {
                  entries[$"traefik/http/routers/{name}/middlewares/{registeredMiddlewares}"] = middleware;
                  registeredMiddlewares++;
                }
              }
            }
        }

        return entries;
    }


    private async Task<string[]> GetServices(Server server, CancellationToken token)
    {
        using var client = httpClientFactory.CreateClient(server.Name);
        using var response = await client.GetAsync("/api/http/services", token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Could not connect to {Server}. Error: {StatusCode}", server.Name, response.StatusCode);
            return [];
        }

        var services = await response.Content.ReadFromJsonAsync<Service[]>(GeneratedJsonInfo.Default.ServiceArray,token);
        return services?.Select(t=> t.Name).ToArray() ?? [];
    }

    private async Task<string[]> GetMiddlewares(Server server, CancellationToken token)
    {
        using var client = httpClientFactory.CreateClient(server.Name);
        using var response = await client.GetAsync("api/http/middlewares", token);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Could not connect to {Server}", server.Name);
            return [];
        }

        var middlewares = await response.Content.ReadFromJsonAsync<Middleware[]>(GeneratedJsonInfo.Default.MiddlewareArray,token);
        return middlewares?.Select(t=> t.Name).ToArray() ?? [];
    }
}
