using StackExchange.Redis;
using TraefikKobling.Worker;
using TraefikKobling.Worker.Exporters;
using TraefikKobling.Worker.Extensions;


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(t =>
    {
        var file = Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "/config.yml";
        t.AddYamlFile(file, optional: false);
    })
    .ConfigureServices((builder,services) =>
    {
        var options = services.AddKoblingOptions(builder.Configuration);
        
        if (options.Servers.IsEmpty())
            throw new ArgumentException("No servers configured.");

        foreach (var server in options.Servers)
        {
            services.AddHttpClient(server.Name,t => t.SetUpHttpClient(server));
        }

        var exporter = builder.Configuration.GetValue<string>("TRAEFIK_EXPORTER");

        switch (exporter?.ToLowerInvariant())
        {
            case "redis":
                var redisConnectionString = builder.Configuration.GetValue<string>("REDIS_URL") ?? throw new ArgumentException("REDIS_URL is required.");
                var redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
                redisConnection.GetServers().First().Ping();
                redisConnection.FlushDatabase(redisConnectionString);
                services.AddSingleton<IConnectionMultiplexer>(redisConnection);
                services.AddSingleton<ITraefikExporter, RedisTraefikExporter>();
                break;
            case "file":
                var dynamicFilePath = builder.Configuration.GetValue<string>("TRAEFIK_DYNAMIC_CONFIG_PATH") 
                                      ?? "/dynamic-kobling.yml";
                services.AddSingleton<ITraefikExporter, FileTraefikExporter>(t => new(
                        t.GetRequiredService<ILogger<FileTraefikExporter>>(),
                        dynamicFilePath
                    )
                );
                break;
            default:
                throw new NotSupportedException($"Unknown exporter '{exporter}'.");
        }
        
        services.AddHostedService<Worker>();
    })
    .Build();

host.Run();
