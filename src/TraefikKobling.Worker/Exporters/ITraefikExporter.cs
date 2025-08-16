namespace TraefikKobling.Worker.Exporters;

public interface ITraefikExporter
{
    Task ExportTraefikEntries(IDictionary<string, string> oldEntries, IDictionary<string, string> newEntries, CancellationToken cancellationToken);
}