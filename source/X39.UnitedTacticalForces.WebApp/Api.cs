using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace X39.UnitedTacticalForces.WebApp;

public partial class Client
{
    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
    }

    partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.ReferenceHandler = ReferenceHandler.Preserve;
    }
}