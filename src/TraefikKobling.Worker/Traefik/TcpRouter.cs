namespace TraefikKobling.Worker.Traefik;

public class TcpRouter
{
    public string[] EntryPoints { get; set; } = [];
    public string Service { get; set; } = "";
    public string Rule { get; set; } = "";
}