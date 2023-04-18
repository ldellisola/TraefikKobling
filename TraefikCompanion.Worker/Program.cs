using StackExchange.Redis;
using TraefikCompanion.Worker;

var redisConnection = ConnectionMultiplexer.Connect("localhost:6379");

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddHttpClient("home",t =>
        {
            t.BaseAddress = new Uri("http://monitor.lud.ar:8000/");
        });
        services.AddSingleton<IConnectionMultiplexer>(redisConnection);
    })
    .Build();

host.Run();
