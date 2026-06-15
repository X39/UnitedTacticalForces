using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using X39.UnitedTacticalForces.WebApp.UtfApi;

namespace X39.UnitedTacticalForces.WebApp.Services;

public class RepositoryBase
{
    protected static JsonSerializerOptions JsonSerializerOptions { get; } = CreateJsonSerializerOptions();

    protected BaseUrl BaseUrl { get; }
    protected ApiClient Client { get; }
    protected HttpClient HttpClient { get; }
    protected IRequestAdapter RequestAdapter { get; }

    public RepositoryBase(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
    {
        BaseUrl = baseUrl;
        HttpClient = httpClientFactory.CreateClient("API");
        var authenticationProvider = new AnonymousAuthenticationProvider();
        var requestAdapter = new HttpClientRequestAdapter(authenticationProvider, httpClient: HttpClient);
        requestAdapter.BaseUrl = baseUrl.ApiUri.ToString();
        RequestAdapter = requestAdapter;
        Client = new ApiClient(requestAdapter);
    }

    protected Task<TValue?> GetFromJsonAsync<TValue>(
        string route,
        CancellationToken cancellationToken = default
    )
        => HttpClient.GetFromJsonAsync<TValue>(
            BaseUrl.ResolveApiUrl(route),
            JsonSerializerOptions,
            cancellationToken
        );

    protected static void PreferJsonResponse<TQueryParameters>(
        RequestConfiguration<TQueryParameters> requestConfiguration
    )
        where TQueryParameters : class, new()
    {
        requestConfiguration.Headers.TryAdd("Accept", "application/json, text/plain;q=0.9");
    }

    protected Task<HttpResponseMessage> PostAsJsonAsync<TValue>(
        string route,
        TValue value,
        CancellationToken cancellationToken = default
    )
        => HttpClient.PostAsJsonAsync(
            BaseUrl.ResolveApiUrl(route),
            value,
            JsonSerializerOptions,
            cancellationToken
        );

    protected Task<HttpResponseMessage> PutAsJsonAsync<TValue>(
        string route,
        TValue value,
        CancellationToken cancellationToken = default
    )
        => HttpClient.PutAsJsonAsync(
            BaseUrl.ResolveApiUrl(route),
            value,
            JsonSerializerOptions,
            cancellationToken
        );

    protected static Task<TValue?> ReadFromJsonAsync<TValue>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default
    )
        => response.Content.ReadFromJsonAsync<TValue>(
            JsonSerializerOptions,
            cancellationToken
        );

    private static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new KiotaComposedTypeJsonConverterFactory());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
