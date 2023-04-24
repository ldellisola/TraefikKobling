namespace TraefikKobling.Worker.Configuration;

public class Server
{
    public required string Name { get; set; }
    public required Uri ApiAddress { get; set; }
    public string? ApiHost { get; set; }
    public required Uri DestinationAddress { get; set; }
    public Dictionary<string, string> EntryPoints { get; set; } = new()
    {
        {"http", "http"}
    };
}