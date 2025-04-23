using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using X39.UnitedTacticalForces.WebApp.Api;

namespace X39.UnitedTacticalForces.WebApp.Services;

public class RepositoryBase
{
    protected BaseUrl BaseUrl { get; }
    protected ApiClient Client { get; }

    public RepositoryBase(HttpClient httpClient, BaseUrl baseUrl)
    {
        BaseUrl = baseUrl;
        var authenticationProvider = new AnonymousAuthenticationProvider();
        var requestAdapter = new HttpClientRequestAdapter(authenticationProvider, httpClient: httpClient);
        Client = new ApiClient(requestAdapter);
    }
}
