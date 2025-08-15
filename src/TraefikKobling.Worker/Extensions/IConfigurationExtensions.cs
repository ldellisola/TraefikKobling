using TraefikKobling.Worker.Configuration;

namespace TraefikKobling.Worker.Extensions;

public static class IConfigurationExtensions
{
    public static KoblingOptions AddKoblingOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var servers = new List<Server>();
        foreach (var child in configuration.GetSection("servers").GetChildren())
        {
            var server = new Server
            {
                Name = child.GetValue<string>(nameof(Server.Name)) ?? throw new ArgumentException($"{nameof(Server.Name)} is required"),
                ApiAddress = new Uri(child.GetValue<string>(nameof(Server.ApiAddress)) ?? throw new ArgumentException($"{nameof(Server.ApiAddress)} is required")),
                DestinationAddress = new Uri(child.GetValue<string>(nameof(Server.DestinationAddress))?? throw new ArgumentException($"{nameof(Server.DestinationAddress)} is required")),
                ApiHost = child.GetValue<string>(nameof(Server.ApiHost)),
                ForwardMiddlewares = child.GetValue<bool?>(nameof(Server.ForwardMiddlewares)),
                ForwardServices = child.GetValue<bool?>(nameof(Server.ForwardServices))
            };
            
            if (child.GetSection("entryPoints").Get<Dictionary<string,string>>() is { Count: > 0 } entryPoints)
                server.EntryPoints = entryPoints;

            servers.Add(server);
        }
        
        var options = new KoblingOptions
        {
            Servers = servers.ToArray(),
            RunEvery = configuration.GetValue<int>("RUN_EVERY"),
            ForwardMiddlewares = configuration.GetValue<bool?>(nameof(KoblingOptions.ForwardMiddlewares)),
            ForwardServices = configuration.GetValue<bool?>(nameof(KoblingOptions.ForwardMiddlewares))
        };

        services.AddSingleton(options);
        return options;
    }
}