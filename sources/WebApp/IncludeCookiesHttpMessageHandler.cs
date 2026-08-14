using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace X39.UnitedTacticalForces.WebApp;

public sealed class IncludeCookiesHttpMessageHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        request.Headers.TryAddWithoutValidation("X-UTF-UI-Variant", "classic");
        return base.SendAsync(request, cancellationToken);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        request.Headers.TryAddWithoutValidation("X-UTF-UI-Variant", "classic");
        return base.Send(request, cancellationToken);
    }
}
