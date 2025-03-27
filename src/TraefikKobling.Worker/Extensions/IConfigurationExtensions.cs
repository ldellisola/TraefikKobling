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
                Name = child.GetRequiredValue<string>(nameof(Server.Name)),
                ApiAddress = new Uri(child.GetRequiredValue<string>(nameof(Server.ApiAddress))),
                DestinationAddress = new Uri(child.GetRequiredValue<string>(nameof(Server.DestinationAddress))),
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
    
    public static T GetRequiredValue<T>(this IConfiguration section, string key)
    {
        var value = section.GetValue<T>(key);
        if (value is null)
            throw new ArgumentException($"{key} is required");
        return value;
    }
}