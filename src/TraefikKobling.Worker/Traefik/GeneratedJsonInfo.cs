using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraefikKobling.Worker.Traefik;

[JsonSerializable(typeof(HttpRouter[]))]
[JsonSerializable(typeof(TcpRouter[]))]
[JsonSerializable(typeof(Middleware[]))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
public partial class GeneratedJsonInfo : JsonSerializerContext;