using StackExchange.Redis;
using TraefikCompanion.Worker;
using TraefikCompanion.Worker.Configuration;


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(t =>
    {
        var file = Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "/config.yml";
        t.AddYamlFile(file, optional:false);
    })
    .ConfigureServices((builder,services) =>
    {
        var servers = builder.Configuration.GetSection("servers").Get<Server[]>();
        
        if (servers is null || servers.Length == 0)
            throw new ArgumentException("No servers configured.");

        foreach (var server in servers)
        {
            services.AddHttpClient(server.Name,t =>
            {
                t.BaseAddress = server.ApiAddress;
            });
        }

        var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_URL") 
                                    ?? throw new ArgumentException("REDIS_URL is not set");
        
        var redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(redisConnection);
        services.AddHostedService<Worker>();
    })
    .Build();

host.Run();
