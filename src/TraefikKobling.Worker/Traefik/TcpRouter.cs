namespace TraefikKobling.Worker.Traefik;

public class TcpRouter
{
    public string Name { get; set; } = "";
    public string[] EntryPoints { get; set; } = [];
    public string Service { get; set; } = "";
    public string Rule { get; set; } = "";
    public TcpTls? Tls { get; set; }
}

public class TcpTls
{
    public bool Passthrough { get; set; }
}
