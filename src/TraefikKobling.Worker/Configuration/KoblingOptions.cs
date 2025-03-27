namespace TraefikKobling.Worker.Configuration;

public class KoblingOptions
{
    public required Server[] Servers { get; set; }
    public int? RunEvery { get; set; }
    public bool? ForwardMiddlewares { get; set; }
    public bool? ForwardServices { get; set; }
}
