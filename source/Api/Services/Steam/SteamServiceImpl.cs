using System.Collections.ObjectModel;
using SteamKit2;
using X39.Util;
using X39.Util.DependencyInjection.Attributes;
using X39.Util.Threading;
using X39.Util.Threading.Tasks;

namespace X39.UnitedTacticalForces.Api.Services.Steam;

[Singleton<SteamServiceImpl, ISteamService>]
public class SteamServiceImpl : ISteamService
{
    private readonly HttpClient                _httpClient;
    private readonly IConfiguration            _configuration;
    private readonly ILogger<SteamServiceImpl> _logger;
    private readonly SteamClient               _steamClient;
    private readonly CallbackManager           _callbacks;
    private          bool                      _connected;
    private readonly SteamUser                 _steamUser;
    private readonly SteamApps                 _steamApps;
    private readonly SteamCloud                _steamCloud;

    private readonly Dictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo> _appInfoCache     = new();
    private readonly Dictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo> _packageInfoCache = new();

    private readonly SemaphoreSlim                                               _loginSemaphore       = new(1, 1);
    private readonly SemaphoreSlim                                               _appInfoSemaphore     = new(1, 1);
    private readonly SemaphoreSlim                                               _packageInfoSemaphore = new(1, 1);
    private          Promise?                                                    _logInPromise;
    private          IReadOnlyCollection<SteamApps.LicenseListCallback.License>? _licenses;

    public SteamServiceImpl(
        HttpClient                httpClient,
        IHttpClientFactory        httpClientFactory,
        IConfiguration            configuration,
        ILogger<SteamServiceImpl> logger
    )
    {
        _httpClient    = httpClient;
        _configuration = configuration;
        _logger        = logger;
        var apiKey = configuration[Constants.Configuration.Steam.ApiKey];
        _steamClient = new SteamClient(
            SteamConfiguration.Create(
                (builder) =>
                {
                    builder.WithHttpClientFactory(httpClientFactory.CreateClient);
                    if (apiKey.IsNotNullOrWhiteSpace())
                        builder.WithWebAPIKey(apiKey);
                }
            )
        );
        _callbacks = new CallbackManager(_steamClient);
        _callbacks.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _callbacks.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbacks.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _callbacks.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        _callbacks.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);

        _steamUser = _steamClient.GetHandler<SteamUser>()
                     ?? throw new InvalidOperationException("Failed to get SteamUser handler");
        _steamApps = _steamClient.GetHandler<SteamApps>()
                     ?? throw new InvalidOperationException("Failed to get SteamApps handler");
        _steamCloud = _steamClient.GetHandler<SteamCloud>()
                      ?? throw new InvalidOperationException("Failed to get SteamCloud handler");
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

    private void OnConnected(SteamClient.ConnectedCallback obj)
    {
        _logger.LogInformation("Connected to Steam (JobID: {@SteamJobId})", obj.JobID);
        _connected = true;
    }

    private async ValueTask LoginAsync(string username, string password, CancellationToken cancellationToken = default)
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

    private async ValueTask LoginAsync(CancellationToken cancellationToken = default)
    {
        var username = _configuration[Constants.Configuration.Steam.Username];
        var password = _configuration[Constants.Configuration.Steam.Password];
        if (username.IsNullOrWhiteSpace() || password.IsNullOrWhiteSpace())
        {
            _logger.LogError("Steam username or password is not set");
            throw new InvalidOperationException("Steam username or password is not set");
            return;
        }

        await LoginAsync(username, password, cancellationToken);
    }

    private async ValueTask<SteamCloud.UGCDetailsCallback> GetUserGeneratedContentDetailsAsync(
        ulong             ugcId,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Requesting details for UGC {@UgcId}", ugcId);
        var result = await _steamCloud.RequestUGCDetails(123);
        return result;
    }

    public async Task DownloadUserGeneratedContentAsync(
        ulong             appId,
        ulong             ugcId,
        string            downloadPath,
        string            branch            = "public",
        string            os                = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!Path.Exists(downloadPath))
            throw new DirectoryNotFoundException($"The download path '{downloadPath}' does not exist");
        await LoginAsync(cancellationToken);
        var details = await GetUserGeneratedContentDetailsAsync(ugcId, cancellationToken);
        if (details.URL.IsNullOrWhiteSpace())
        {
            cdnPool = new CDNClientPool(steam3, appId);
            if (!await CanAccessAsync(appId, cancellationToken))
            {
                if (steam3.RequestFreeAppLicense(appId))
                {
                    Console.WriteLine("Obtained FreeOnDemand license for app {0}", appId);

                    // Fetch app info again in case we didn't get it fully without a license.
                    steam3.RequestAppInfo(appId, true);
                }
                else
                {
                    var contentName = GetAppName(appId);
                    throw new ContentDownloaderException(
                        string.Format("App {0} ({1}) is not available from this account.", appId, contentName)
                    );
                }
            }

            var hasSpecificDepots = depotManifestIds.Count > 0;
            var depotIdsFound     = new List<uint>();
            var depotIdsExpected  = depotManifestIds.Select(x => x.depotId).ToList();
            var depots            = GetSteam3AppSection(appId, EAppInfoSection.Depots);

            if (isUgc)
            {
                var workshopDepot = depots["workshopdepot"].AsUnsignedInteger();
                if (workshopDepot != 0 && !depotIdsExpected.Contains(workshopDepot))
                {
                    depotIdsExpected.Add(workshopDepot);
                    depotManifestIds = depotManifestIds.Select(pair => (workshopDepot, pair.manifestId)).ToList();
                }

                depotIdsFound.AddRange(depotIdsExpected);
            }
            else
            {
                Console.WriteLine("Using app branch: '{0}'.", branch);

                if (depots != null)
                {
                    foreach (var depotSection in depots.Children)
                    {
                        var id = INVALID_DEPOT_ID;
                        if (depotSection.Children.Count == 0)
                            continue;

                        if (!uint.TryParse(depotSection.Name, out id))
                            continue;

                        if (hasSpecificDepots && !depotIdsExpected.Contains(id))
                            continue;

                        if (!hasSpecificDepots)
                        {
                            var depotConfig = depotSection["config"];
                            if (depotConfig != KeyValue.Invalid)
                            {
                                if (!Config.DownloadAllPlatforms
                                    && depotConfig["oslist"] != KeyValue.Invalid
                                    && !string.IsNullOrWhiteSpace(depotConfig["oslist"].Value))
                                {
                                    var oslist = depotConfig["oslist"].Value.Split(',');
                                    if (Array.IndexOf(oslist, os ?? Util.GetSteamOS()) == -1)
                                        continue;
                                }

                                if (depotConfig["osarch"] != KeyValue.Invalid
                                    && !string.IsNullOrWhiteSpace(depotConfig["osarch"].Value))
                                {
                                    var depotArch = depotConfig["osarch"].Value;
                                    if (depotArch != (arch ?? Util.GetSteamArch()))
                                        continue;
                                }

                                if (!Config.DownloadAllLanguages
                                    && depotConfig["language"] != KeyValue.Invalid
                                    && !string.IsNullOrWhiteSpace(depotConfig["language"].Value))
                                {
                                    var depotLang = depotConfig["language"].Value;
                                    if (depotLang != (language ?? "english"))
                                        continue;
                                }

                                if (!lv
                                    && depotConfig["lowviolence"] != KeyValue.Invalid
                                    && depotConfig["lowviolence"].AsBoolean())
                                    continue;
                            }
                        }

                        depotIdsFound.Add(id);

                        if (!hasSpecificDepots)
                            depotManifestIds.Add((id, INVALID_MANIFEST_ID));
                    }
                }

                if (depotManifestIds.Count == 0 && !hasSpecificDepots)
                {
                    throw new ContentDownloaderException(
                        string.Format("Couldn't find any depots to download for app {0}", appId)
                    );
                }

                if (depotIdsFound.Count < depotIdsExpected.Count)
                {
                    var remainingDepotIds = depotIdsExpected.Except(depotIdsFound);
                    throw new ContentDownloaderException(
                        string.Format("Depot {0} not listed for app {1}", string.Join(", ", remainingDepotIds), appId)
                    );
                }
            }

            var infos = new List<DepotDownloadInfo>();

            foreach (var (depotId, manifestId) in depotManifestIds)
            {
                var info = GetDepotInfo(depotId, appId, manifestId, branch);
                if (info != null)
                {
                    infos.Add(info);
                }
            }

            try
            {
                await DownloadSteam3Async(infos).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("App {0} was not completely downloaded.", appId);
                throw;
            }
        }
        else
        {
            await DownloadFileFromUrlAsync(ugcId, downloadPath, details, cancellationToken);
        }
    }

    private async Task<SteamApps.PICSProductInfoCallback.PICSProductInfo> GetProductInfoAsync(
        uint              packageId,
        CancellationToken cancellationToken
    )
    {
        if (_packageInfoCache.TryGetValue(packageId, out var cachedInfo))
            return cachedInfo;
        return await _packageInfoSemaphore.LockedAsync(
            async () =>
            {
                if (_packageInfoCache.TryGetValue(packageId, out var cachedInfo2))
                    return cachedInfo2;
                var allPackageIds =
                    _licenses?.Select((q) => (q.PackageID, AccessToken: (ulong?) q.AccessToken))
                             .Where((q) => !_packageInfoCache.ContainsKey(q.PackageID))
                             .Append((PackageID: packageId, AccessToken: default(ulong?)))
                             .ToArray()
                    ?? (PackageID: packageId, AccessToken: default(ulong?)).MakeArray();

                _logger.LogDebug(
                    "Requesting product info for packages {PackageIds}",
                    allPackageIds.Select((q) => q.PackageID)
                );
                var requests = allPackageIds.Select(
                    (q) => new SteamApps.PICSRequest(q.PackageID, q.AccessToken ?? default)
                );
                var result = await _steamApps.PICSGetProductInfo(Enumerable.Empty<SteamApps.PICSRequest>(), requests);
                while (!result.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }

                if (result.Results == null)
                {
                    _logger.LogError("Failed to get product info for packages {PackageIds}", allPackageIds);
                    throw new InvalidOperationException("Failed to get product info");
                }

                foreach (var picsProductInfoCallback in result.Results)
                {
                    foreach (var picsProductInfo in picsProductInfoCallback.Packages.Values)
                    {
                        _packageInfoCache[picsProductInfo.ID] = picsProductInfo;
                    }
                }
                
                if (_packageInfoCache.TryGetValue(packageId, out var info))
                    return info;
                _logger.LogError("Failed to get product info for package {PackageId}", packageId);
                throw new InvalidOperationException("Failed to get product info");
            },
            cancellationToken
        );
    }

    private async Task<bool> CanAccessAsync(ulong appId, CancellationToken cancellationToken)
    {
        var licenseQuery = _licenses?.Select((q) => q.PackageID).ToArray() ?? [17906 /* Magic number */];
        _pa
    }

    private async Task DownloadFileFromUrlAsync(
        ulong                         ugcId,
        string                        downloadPath,
        SteamCloud.UGCDetailsCallback details,
        CancellationToken             cancellationToken = default
    )
    {
        var filePath = Path.Combine(downloadPath, details.FileName);
        _logger.LogInformation("Downloading UGC {UgcId} from {Url} to {FilePath}", ugcId, details.URL, filePath);
        if (Path.Exists(filePath))
            _logger.LogWarning("File {FilePath} already exists and will be overwritten", filePath);
        try
        {
            await using var file   = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var stream = await _httpClient.GetStreamAsync(details.URL, cancellationToken);
            await stream.CopyToAsync(file, cancellationToken);
        }
        catch
        {
            _logger.LogError("Failed to download UGC {UgcId} from {Url} to {FilePath}", ugcId, details.URL, filePath);
            if (File.Exists(filePath))
                File.Delete(filePath);
            throw;
        }
    }
}
