namespace TraefikKobling.Worker.Configuration;

public class KoblingOptions
{
    public required Server[] Servers { get; set; }
    public int? RunEvery { get; set; }
}
