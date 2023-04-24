using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TraefikKobling.Worker;
using TraefikKobling.Worker.Configuration;
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
            services.AddHttpClient(server.Name,t =>
            {
                t.BaseAddress = server.ApiAddress;
            });
        }

        var redisConnectionString = builder.Configuration.GetValue<string>("REDIS_URL") 
                                    ?? throw new ArgumentException("REDIS_URL is not set");
        
        var redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(redisConnection);
        services.AddHostedService<Worker>();
    })
    .Build();

host.Run();
