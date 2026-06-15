using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Singleton<AuthenticationProviderAvailability>]
public sealed class AuthenticationProviderAvailability : RepositoryBase
{
    public AuthenticationProviderAvailability(IHttpClientFactory httpClientFactory, BaseUrl baseUrl)
        : base(httpClientFactory, baseUrl)
    {
    }

    public bool Steam { get; private set; }

    public bool Discord { get; private set; }

    public bool HasAvailableProvider => Steam || Discord;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Steam   = false;
        Discord = false;

        try
        {
            var response = await Client.Users.AuthenticationProviders.GetAsync(cancellationToken: cancellationToken);
            Steam   = response?.Steam ?? false;
            Discord = response?.Discord ?? false;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                UserFacingError.ConsoleMessage(exception, "Authentication provider availability could not be loaded.")
            );
        }
    }
}
