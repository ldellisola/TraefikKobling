namespace TraefikKobling.Worker.Configuration;

public class Server
{
    public required string Name { get; set; }
    public required Uri ApiAddress { get; set; }
    public required Uri DestinationAddress { get; set; }
}