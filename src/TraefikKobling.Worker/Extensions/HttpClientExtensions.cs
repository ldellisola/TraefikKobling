using System.Net.Http.Headers;
using System.Text;
using TraefikKobling.Worker.Configuration;

namespace TraefikKobling.Worker.Extensions;

public static class HttpClientExtensions
{
    public static void SetUpHttpClient(this HttpClient httpClient, Server server)
    {
        httpClient.BaseAddress = server.ApiAddress;
        if (!server.ApiHost.IsNullOrWhiteSpace())
            httpClient.DefaultRequestHeaders.Host = server.ApiHost;
        
        if (!server.ApiAddress.UserInfo.IsNullOrWhiteSpace())
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(server.ApiAddress.UserInfo)));
        
    }
}