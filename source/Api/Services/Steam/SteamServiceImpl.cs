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
    private          bool                      _connected;

    private readonly Dictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo> _appInfoCache     = new();
    private readonly Dictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo> _packageInfoCache = new();

    private readonly SemaphoreSlim                                               _appInfoSemaphore     = new(1, 1);
    private readonly SemaphoreSlim                                               _packageInfoSemaphore = new(1, 1);
    private readonly SteamApiSession                                             _apiSession;

    public SteamServiceImpl(
        HttpClient                httpClient,
        IHttpClientFactory        httpClientFactory,
        IConfiguration            configuration,
        ILogger<SteamServiceImpl> logger,
        ILoggerFactory loggerFactory
    )
    {
        _httpClient    = httpClient;
        _configuration = configuration;
        _logger        = logger;
        var apiKey = configuration[Constants.Configuration.Steam.ApiKey] ?? string.Empty;
        _apiSession = new SteamApiSession(loggerFactory.CreateLogger<SteamApiSession>(), httpClientFactory, apiKey);
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

        await _apiSession.LoginAsync(username, password, cancellationToken);
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
        var details = await _apiSession.GetUserGeneratedContentDetailsAsync(ugcId);
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
                    _apiSession.Licenses
                               .Select((q) => (true, q.PackageID, AccessToken: (ulong?) q.AccessToken))
                             .Where((q) => !_packageInfoCache.ContainsKey(q.PackageID))
                             .Append((false, PackageID: packageId, AccessToken: default(ulong?)))
                             .ToArray();

                _logger.LogDebug(
                    "Requesting product info for packages {PackageIds}",
                    allPackageIds.Select((q) => q.PackageID)
                );
                var result = await _apiSession.GetProductInformationAsync(allPackageIds, cancellationToken);

                foreach (var picsProductInfoCallback in result)
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
