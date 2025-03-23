namespace TraefikKobling.Worker.Traefik;

public class HttpRouter
{
    public string[] EntryPoints { get; set; } = [];
    public string[] Middlewares { get; set; } = [];
    public string Service { get; set; } = "";
    public string Rule { get; set; } = "";
    public long Priority { get; set; }
}