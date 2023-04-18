using TraefikCompanion.Worker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddHttpClient("home",t =>
        {
            t.BaseAddress = new Uri("https://monitor.lud.ar/api");
        });
    })
    .Build();

host.Run();
