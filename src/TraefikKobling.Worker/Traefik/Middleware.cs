namespace TraefikKobling.Worker.Traefik;

public class Middleware
{
    public required string Status { get; set; }
    public string[] UsedBy { get; set; } = [];
    public required string Name { get; set; }
    public required string Provider { get; set; }
}