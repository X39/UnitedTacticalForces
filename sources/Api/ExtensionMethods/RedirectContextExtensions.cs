using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace X39.UnitedTacticalForces.Api.ExtensionMethods;

public static class RedirectContextExtensions
{
    public static void UpdateRedirectUrlStateQueryParameter<T>(this RedirectContext<T> self) where T : OAuthOptions
    {
        var uri = new UriBuilder(self.RedirectUri);
        var collection = HttpUtility.ParseQueryString(uri.Query);
        collection.Remove("state");
        collection.Add("state", self.Options.StateDataFormat.Protect(self.Properties));
        uri.Query = collection.ToString();
        self.RedirectUri = uri.Uri.ToString();
    }
}