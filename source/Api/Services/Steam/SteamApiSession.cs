using System.Collections.Immutable;
using System.Collections.ObjectModel;
using SteamKit2;
using X39.Util;
using X39.Util.Threading;
using X39.Util.Threading.Tasks;

namespace X39.UnitedTacticalForces.Api.Services.Steam;

public sealed class SteamApiSession : IAsyncDisposable
{
    /// <summary>
    /// Wrapping structure for DI usage with <see cref="SteamApiKey"/>
    /// </summary>
    public record struct SteamApiKey(string Value)
    {
        /// <summary>
        /// Implicit operator for <see cref="string"/> to <see cref="SteamApiKey"/>.
        /// </summary>
        public static implicit operator SteamApiKey(string value) => new(value);

        /// <summary>
        /// Implicit operator for <see cref="SteamApiKey"/> to <see cref="string"/>.
        /// </summary>
        public static implicit operator string(SteamApiKey value) => value.Value;
    }

    private readonly ILogger<SteamApiSession>                                   _logger;
    private readonly IHttpClientFactory                                         _httpClientFactory;
    private readonly string                                                     _apiKey;
    private readonly SteamClient                                                _steamClient;
    private          CallbackManager                                            _callbacks;
    private readonly SteamUser                                                  _steamUser;
    private readonly SteamApps                                                  _steamApps;
    private readonly SteamCloud                                                 _steamCloud;
    private          bool                                                       _connected;
    private          Promise?                                                   _logInPromise;
    private          ReadOnlyCollection<SteamApps.LicenseListCallback.License>? _licenses;
    private readonly SemaphoreSlim                                              _loginSemaphore = new(1, 1);

    /// <summary>
    /// Gets the collection of Steam licenses associated with the current session.
    /// </summary>
    /// <remarks>
    /// The licenses are retrieved as a read-only collection. If there are no licenses
    /// available, an empty collection is returned.
    /// </remarks>
    public IReadOnlyCollection<SteamApps.LicenseListCallback.License> Licenses
        => _licenses as IReadOnlyCollection<SteamApps.LicenseListCallback.License> ??
           Array.Empty<SteamApps.LicenseListCallback.License>();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public SteamApiSession(ILogger<SteamApiSession> logger, IHttpClientFactory httpClientFactory, SteamApiKey apiKey)
    {
        _logger            = logger;
        _httpClientFactory = httpClientFactory;
        _apiKey            = apiKey;
        _steamClient       = new SteamClient(SteamConfiguration.Create(SteamClientConfigurator));
        _callbacks         = new CallbackManager(_steamClient);
        _callbacks.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _callbacks.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbacks.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _callbacks.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        _callbacks.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);

        _steamUser = _steamClient.GetHandler<SteamUser>() ??
                     throw new InvalidOperationException("Failed to get SteamUser handler");
        _steamApps = _steamClient.GetHandler<SteamApps>() ??
                     throw new InvalidOperationException("Failed to get SteamApps handler");
        _steamCloud = _steamClient.GetHandler<SteamCloud>() ??
                      throw new InvalidOperationException("Failed to get SteamCloud handler");
    }

    private void SteamClientConfigurator(ISteamConfigurationBuilder builder)
    {
        builder.WithHttpClientFactory(_httpClientFactory.CreateClient);
        if (_apiKey.IsNotNullOrWhiteSpace())
            builder.WithWebAPIKey(_apiKey);
    }

    #region Callbacks

    private void OnConnected(SteamClient.ConnectedCallback obj)
    {
        _logger.LogInformation("Connected to Steam (JobID: {@SteamJobId})", obj.JobID);
        _connected = true;
    }

    private void OnLicenseList(SteamApps.LicenseListCallback obj)
    {
        _logger.LogTrace("Received license list");
        _licenses = obj.LicenseList;
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback obj)
    {
        _logger.LogWarning("Logged off from Steam: {@Result} (JobID: {@SteamJobId})", obj.Result, obj.JobID);
        if (_logInPromise is { } promise)
        {
            try
            {
                throw new Exception("Logging in to Steam failed (Logged off)");
            }
            catch (Exception e)
            {
                promise.Complete(e);
            }
        }
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback obj)
    {
        if (obj.Result == EResult.OK)
        {
            _logger.LogInformation(
                "Logged in to Steam as {@Username} (JobID: {@SteamJobId})",
                obj.ClientSteamID,
                obj.JobID
            );
            if (_logInPromise is { } promise1)
            {
                promise1.Complete();
            }

            return;
        }

        _logger.LogError(
            "Failed to log in to Steam: {@Result} (Extended: {@ExtendedResult}, JobID: {@SteamJobId})",
            obj.Result,
            obj.ExtendedResult,
            obj.JobID
        );
        if (_logInPromise is { } promise2)
        {
            try
            {
                throw new Exception("Logging in to Steam failed: {Result} (Extended: {ExtendedResult})");
            }
            catch (Exception e)
            {
                promise2.Complete(e);
            }
        }
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback obj)
    {
        // obj.UserInitiated, obj.JobID.Value
        if (obj.UserInitiated)
            _logger.LogInformation(
                "The connection to Steam has been closed by the user (JobID: {@SteamJobId})",
                obj.JobID
            );
        else
            _logger.LogWarning(
                "The connection to Steam has been closed unexpectedly (JobID: {@SteamJobId})",
                obj.JobID
            );
        _connected = false;
        if (_logInPromise is { } promise)
        {
            try
            {
                throw new Exception("Logging in to Steam failed");
            }
            catch (Exception e)
            {
                promise.Complete(e);
            }
        }
    }

    #endregion

    /// <summary>
    /// Asynchronously logs in to the Steam service with the given username and password.
    /// </summary>
    /// <param name="username">The username used for logging in to Steam.</param>
    /// <param name="password">The password used for logging in to Steam.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A ValueTask representing the asynchronous login operation.</returns>
    public async ValueTask LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (_connected)
        {
            _logger.LogDebug("Skipping login to Steam as already connected");
            return;
        }

        await _loginSemaphore.LockedAsync(
            async () =>
            {
                // Repeat check after acquiring lock
                if (_connected)
                {
                    _logger.LogDebug("Skipping login to Steam as already connected");
                    return;
                }


                _logger.LogInformation("Logging in to Steam as {@Username}", username);

                _logInPromise = new Promise();
                _steamUser.LogOn(
                    new SteamUser.LogOnDetails
                    {
                        Username = username, Password = password, ShouldRememberPassword = true,
                    }
                );

                await _logInPromise;
                _logInPromise = null;
            },
            cancellationToken
        );
    }


    /// <summary>
    /// Asynchronously retrieves the details of user-generated content (UGC) identified by a given ID.
    /// </summary>
    /// <param name="ugcId">The identifier of the user-generated content.</param>
    /// <returns>A ValueTask representing the asynchronous operation, containing the details of the user-generated content.</returns>
    public async ValueTask<SteamCloud.UGCDetailsCallback> GetUserGeneratedContentDetailsAsync(ulong ugcId)
    {
        _logger.LogDebug("Requesting details for UGC {@UgcId}", ugcId);
        var result =await _steamCloud.RequestUGCDetails(ugcId);
        return result;
    }

    /// <summary>
    /// Retrieves detailed product information for a collection of Steam apps and packages asynchronously.
    /// </summary>
    /// <param name="requests">
    /// A collection of tuples, each indicating whether the request is for an app or package,
    /// along with the respective PackageId and an optional AccessToken.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional. A token to monitor for cancellation requests.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation, containing a readonly collection of PICS product information callbacks.
    /// </returns>
    public async Task<IReadOnlyCollection<SteamApps.PICSProductInfoCallback>> GetProductInformationAsync(
        IReadOnlyCollection<(bool isApp, uint PackageId, ulong? AccessToken)> requests,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Requesting product information for {@Requests}", requests);
        var appRequests = requests.Where(e => e.isApp)
                                  .Select((q) => new SteamApps.PICSRequest(q.PackageId, q.AccessToken ?? default))
                                  .ToArray();
        var packageRequests = requests.Where(e => !e.isApp)
                                      .Select((q) => new SteamApps.PICSRequest(q.PackageId, q.AccessToken ?? default))
                                      .ToArray();
        var result = await _steamApps.PICSGetProductInfo(appRequests, packageRequests);
        while (result.Complete is false)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        if (result.Results is not null)
            return result.Results;

        _logger.LogError("Failed to get product info for packages {PackageIds}", requests);
        throw new InvalidOperationException("Failed to get product info");
    }
}