using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Media;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.Helpers;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.Media;

/// <summary>
/// Default implementation for reusable gallery image storage and metadata queries.
/// </summary>
/// <param name="apiDbContext">API database context.</param>
/// <param name="webHostEnvironment">Optional web host environment for storage root resolution.</param>
/// <param name="mediaAssetVariantService">Optional image representation converter.</param>
[Scoped<MediaAssetService, IMediaAssetService>]
public sealed class MediaAssetService(
    DatabaseContext apiDbContext,
    IWebHostEnvironment? webHostEnvironment = null,
    IMediaAssetVariantService? mediaAssetVariantService = null) : IMediaAssetService
{
    private const long MaximumImageFileSizeBytes = 10L * 1024L * 1024L;
    private const string StorageDirectoryName = "media-assets";

    /// <inheritdoc />
    public string StorageRootPath
    {
        get
        {
            var contentRootPath = string.IsNullOrWhiteSpace(webHostEnvironment?.ContentRootPath)
                ? AppContext.BaseDirectory
                : webHostEnvironment.ContentRootPath;
            return Path.Combine(contentRootPath, "App_Data", StorageDirectoryName);
        }
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto[]> ListAsync(
        MediaAssetFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (filter.Skip < 0 || filter.Take is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(filter), "Invalid media asset paging parameters.");

        var query = apiDbContext.MediaAssets
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.CreatedBy)
            .Include(q => q.Tags)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(q =>
                q.OriginalFileName.ToLower().Contains(search)
                || q.ContentType.ToLower().Contains(search)
                || q.Tags!.Any(tag => tag.Value.ToLower().Contains(search))
            );
        }

        var includeTags = NormalizeTags(filter.IncludeTags);
        foreach (var tag in includeTags)
            query = query.Where(q => q.Tags!.Any(assetTag => assetTag.Value == tag));

        var excludeTags = NormalizeTags(filter.ExcludeTags);
        foreach (var tag in excludeTags)
            query = query.Where(q => !q.Tags!.Any(assetTag => assetTag.Value == tag));

        var assets = await query
            .OrderByDescending(q => q.TimeStampCreated)
            .ThenBy(q => q.OriginalFileName)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var usageMap = await GetUsageMapAsync(assets, cancellationToken).ConfigureAwait(false);
        var filteredAssets = assets
            .Where(asset => MatchesUsageFilters(asset.PrimaryKey, usageMap, filter))
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToArray();

        return filteredAssets
            .Select(asset => ToDto(asset, usageMap.GetValueOrDefault(asset.PrimaryKey) ?? []))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto?> GetAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var asset = await apiDbContext.MediaAssets
            .AsNoTracking()
            .AsSplitQuery()
            .Include(q => q.CreatedBy)
            .Include(q => q.Tags)
            .SingleOrDefaultAsync(q => q.PrimaryKey == assetId, cancellationToken)
            .ConfigureAwait(false);
        if (asset is null)
            return null;

        var usageMap = await GetUsageMapAsync([asset], cancellationToken).ConfigureAwait(false);
        return ToDto(asset, usageMap.GetValueOrDefault(asset.PrimaryKey) ?? []);
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> UploadAsync(
        IFormFile file,
        IEnumerable<string> tags,
        Guid? createdByFk,
        CancellationToken cancellationToken = default)
    {
        ValidateImageFile(file);
        await using var imageStream = file.OpenReadStream();
        return await ImportAsync(
                imageStream,
                file.FileName,
                file.ContentType,
                tags,
                createdByFk,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto> ImportAsync(
        Stream imageStream,
        string originalFileName,
        string contentType,
        IEnumerable<string> tags,
        Guid? createdByFk,
        CancellationToken cancellationToken = default)
    {
        if (imageStream is null || !imageStream.CanRead)
            throw new ArgumentException("Image stream is required.", nameof(imageStream));

        var normalizedContentType = ImageMimeType.Normalize(contentType)
                                    ?? throw new ArgumentException("Image file type is not supported.", nameof(contentType));

        Directory.CreateDirectory(StorageRootPath);
        var tempFilePath = Path.Combine(StorageRootPath, $"{Guid.NewGuid():N}.upload");
        var (sha256Hash, size) = await WriteTempFileAndHashAsync(imageStream, tempFilePath, cancellationToken)
            .ConfigureAwait(false);
        if (size <= 0)
        {
            System.IO.File.Delete(tempFilePath);
            throw new ArgumentException("Image file is required.", nameof(imageStream));
        }
        if (size > MaximumImageFileSizeBytes)
        {
            System.IO.File.Delete(tempFilePath);
            throw new ArgumentException("Image file is too large.", nameof(imageStream));
        }
        var detectedContentType = await DetectContentTypeAsync(tempFilePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(detectedContentType, normalizedContentType, StringComparison.Ordinal))
        {
            System.IO.File.Delete(tempFilePath);
            throw new ArgumentException("Image file content does not match its declared type.", nameof(imageStream));
        }

        try
        {
            var now = NodaSystemClock.Instance.GetCurrentInstant();
            var existingAsset = await apiDbContext.MediaAssets
                .Include(q => q.CreatedBy)
                .Include(q => q.Tags)
                .SingleOrDefaultAsync(q => q.Sha256Hash == sha256Hash, cancellationToken)
                .ConfigureAwait(false);
            if (existingAsset is not null)
            {
                System.IO.File.Delete(tempFilePath);
                if (AddMissingTags(existingAsset, tags, createdByFk, now))
                {
                    existingAsset.TimeStampUpdated = now;
                    await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                var existingUsageMap = await GetUsageMapAsync([existingAsset], cancellationToken).ConfigureAwait(false);
                return ToDto(existingAsset, existingUsageMap.GetValueOrDefault(existingAsset.PrimaryKey) ?? []);
            }

            var assetId = Guid.NewGuid();
            var storedFileName = $"{assetId:N}{GetStoredImageExtension(normalizedContentType)}";
            var finalFilePath = Path.Combine(StorageRootPath, storedFileName);
            System.IO.File.Move(tempFilePath, finalFilePath);

            var asset = new MediaAsset
            {
                PrimaryKey       = assetId,
                OriginalFileName = NormalizeOriginalFileName(originalFileName),
                ContentType      = normalizedContentType,
                Size             = size,
                StorageFileName  = storedFileName,
                Sha256Hash       = sha256Hash,
                TimeStampCreated = now,
                TimeStampUpdated = now,
                CreatedByFk      = createdByFk,
                Tags             = [],
            };
            AddMissingTags(asset, tags, createdByFk, now);
            await apiDbContext.MediaAssets.AddAsync(asset, cancellationToken).ConfigureAwait(false);
            await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return ToDto(asset, []);
        }
        catch
        {
            if (System.IO.File.Exists(tempFilePath))
                System.IO.File.Delete(tempFilePath);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<MediaAssetDto?> ReplaceTagsAsync(
        Guid assetId,
        IEnumerable<string> tags,
        Guid? createdByFk,
        CancellationToken cancellationToken = default)
    {
        var asset = await apiDbContext.MediaAssets
            .Include(q => q.CreatedBy)
            .Include(q => q.Tags)
            .SingleOrDefaultAsync(q => q.PrimaryKey == assetId, cancellationToken)
            .ConfigureAwait(false);
        if (asset is null)
            return null;

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var normalizedTags = NormalizeTags(tags).ToHashSet(StringComparer.Ordinal);
        foreach (var existingTag in (asset.Tags ?? []).Where(q => !normalizedTags.Contains(q.Value)).ToArray())
            apiDbContext.MediaAssetTags.Remove(existingTag);

        AddMissingTags(asset, normalizedTags, createdByFk, now);
        asset.TimeStampUpdated = now;
        await apiDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var usageMap = await GetUsageMapAsync([asset], cancellationToken).ConfigureAwait(false);
        return ToDto(asset, usageMap.GetValueOrDefault(asset.PrimaryKey) ?? []);
    }

    /// <inheritdoc />
    public async Task<StoredMediaAssetFile?> GetStoredFileAsync(
        Guid assetId,
        string? preferredContentType = null,
        CancellationToken cancellationToken = default)
    {
        var asset = await apiDbContext.MediaAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.PrimaryKey == assetId, cancellationToken)
            .ConfigureAwait(false);
        if (asset is null || !IsSafeStoredFileName(asset.StorageFileName))
            return null;

        var filePath = Path.GetFullPath(Path.Combine(StorageRootPath, asset.StorageFileName));
        if (!IsPathInDirectory(StorageRootPath, filePath) || !System.IO.File.Exists(filePath))
            return null;

        var source = new StoredMediaAssetFile(filePath, asset.ContentType, asset.Sha256Hash);
        if (preferredContentType is null
            || string.Equals(preferredContentType, asset.ContentType, StringComparison.Ordinal)
            || mediaAssetVariantService is null)
            return source;

        return await mediaAssetVariantService
            .GetOrCreateAsync(source, preferredContentType, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, MediaAssetUsageDto[]>> GetUsageMapAsync(
        IEnumerable<MediaAsset> assets,
        CancellationToken cancellationToken = default)
    {
        var assetIds = assets
            .Select(asset => asset.PrimaryKey)
            .Distinct()
            .ToArray();
        if (assetIds.Length == 0)
            return new Dictionary<Guid, MediaAssetUsageDto[]>();

        var usageByAsset = assetIds.ToDictionary(assetId => assetId, _ => new List<MediaAssetUsageDto>());

        var terrainUsages = await apiDbContext.Terrains
            .AsNoTracking()
            .Where(q => assetIds.Contains(q.ImageAssetFk))
            .Select(q => new
            {
                q.PrimaryKey,
                q.Title,
                q.ImageAssetFk,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var usage in terrainUsages)
        {
            usageByAsset[usage.ImageAssetFk].Add(new MediaAssetUsageDto
            {
                Domain      = "terrain",
                DomainKey   = usage.PrimaryKey.ToString(),
                DisplayName = usage.Title,
                Path        = $"command/terrains/{usage.PrimaryKey}",
            });
        }

        var eventUsages = await apiDbContext.Events
            .AsNoTracking()
            .Where(q => assetIds.Contains(q.ImageAssetFk))
            .Select(q => new
            {
                q.PrimaryKey,
                q.Title,
                q.ImageAssetFk,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var usage in eventUsages)
        {
            usageByAsset[usage.ImageAssetFk].Add(new MediaAssetUsageDto
            {
                Domain      = "event",
                DomainKey   = usage.PrimaryKey.ToString("D"),
                DisplayName = usage.Title,
                Path        = $"events/{usage.PrimaryKey:D}",
            });
        }

        var fieldReportUsages = await apiDbContext.FieldReports
            .AsNoTracking()
            .Where(q => q.HeroImageAssetFk != null && assetIds.Contains(q.HeroImageAssetFk.Value))
            .Select(q => new
            {
                q.PrimaryKey,
                q.Slug,
                q.Title,
                q.HeroImageAssetFk,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var usage in fieldReportUsages)
        {
            if (usage.HeroImageAssetFk is not { } assetId)
                continue;

            usageByAsset[assetId].Add(new MediaAssetUsageDto
            {
                Domain      = "field-report",
                DomainKey   = usage.PrimaryKey.ToString("D"),
                DisplayName = usage.Title,
                Path        = $"command/field-reports/{usage.PrimaryKey:D}",
            });
        }

        var publicPageUsages = await apiDbContext.PublicPageRevisions
            .AsNoTracking()
            .Include(q => q.Page)
            .Where(q => assetIds.Contains(q.HeroImageAssetFk))
            .Select(q => new
            {
                q.PrimaryKey,
                q.Version,
                q.Title,
                q.HeroImageAssetFk,
                PageSlug = q.Page!.Slug,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var usage in publicPageUsages)
        {
            usageByAsset[usage.HeroImageAssetFk].Add(new MediaAssetUsageDto
            {
                Domain      = "public-page",
                DomainKey   = usage.PrimaryKey.ToString(),
                DisplayName = $"{usage.Title} v{usage.Version}",
                Path        = $"command/content/pages/{usage.PageSlug}",
            });
        }

        var siteImageUsages = await apiDbContext.SiteImageSettings
            .AsNoTracking()
            .Where(q => assetIds.Contains(q.ImageAssetFk))
            .Select(q => new
            {
                q.PrimaryKey,
                q.Title,
                q.Group,
                q.ImageAssetFk,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var usage in siteImageUsages)
        {
            usageByAsset[usage.ImageAssetFk].Add(new MediaAssetUsageDto
            {
                Domain      = "site-image",
                DomainKey   = usage.PrimaryKey,
                DisplayName = usage.Title,
                Path        = $"command/content/site-images/{usage.Group}",
            });
        }

        return usageByAsset.ToDictionary(
            q => q.Key,
            q => q.Value
                .OrderBy(usage => usage.Domain)
                .ThenBy(usage => usage.DisplayName)
                .ToArray()
        );
    }

    private static bool MatchesUsageFilters(
        Guid assetId,
        IReadOnlyDictionary<Guid, MediaAssetUsageDto[]> usageMap,
        MediaAssetFilter filter)
    {
        var usages = usageMap.GetValueOrDefault(assetId) ?? [];
        var isUsed = usages.Length > 0;

        if (NormalizeUsageStatus(filter.IncludeUsageStatus) is { } includeUsageStatus
            && !MatchesUsageStatus(isUsed, includeUsageStatus))
            return false;
        if (NormalizeUsageStatus(filter.ExcludeUsageStatus) is { } excludeUsageStatus
            && MatchesUsageStatus(isUsed, excludeUsageStatus))
            return false;

        var includeDomains = NormalizeDomains(filter.IncludeUsageDomains);
        if (includeDomains.Length > 0 && !includeDomains.Any(domain => usages.Any(usage => usage.Domain == domain)))
            return false;

        var excludeDomains = NormalizeDomains(filter.ExcludeUsageDomains);
        return excludeDomains.Length == 0 || !excludeDomains.Any(domain => usages.Any(usage => usage.Domain == domain));
    }

    private static bool MatchesUsageStatus(bool isUsed, string usageStatus)
        => usageStatus switch
        {
            "used"   => isUsed,
            "unused" => !isUsed,
            _        => true,
        };

    private static string? NormalizeUsageStatus(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "used"   => "used",
            "unused" => "unused",
            _        => null,
        };

    private static string[] NormalizeDomains(IEnumerable<string> values)
        => values
            .SelectMany(SplitTagValue)
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => value is "terrain" or "event" or "field-report" or "public-page" or "site-image")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string[] NormalizeTags(IEnumerable<string> tags)
        => tags
            .SelectMany(SplitTagValue)
            .Select(NormalizeTag)
            .Where(tag => tag is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> SplitTagValue(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static string? NormalizeTag(string value)
    {
        var characters = value
            .Trim()
            .ToLowerInvariant()
            .Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                    ? character
                    : '-')
            .ToArray();
        var normalized = string.Join(
                "-",
                new string(characters).Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            )
            .Trim('-');
        return normalized.Length is < 1 or > 64 ? null : normalized;
    }

    private static bool AddMissingTags(
        MediaAsset asset,
        IEnumerable<string> tags,
        Guid? createdByFk,
        Instant now)
    {
        asset.Tags ??= [];
        var existingTags = asset.Tags
            .Select(q => q.Value)
            .ToHashSet(StringComparer.Ordinal);
        var changed = false;
        foreach (var tag in NormalizeTags(tags))
        {
            if (!existingTags.Add(tag))
                continue;

            asset.Tags.Add(new MediaAssetTag
            {
                MediaAssetFk     = asset.PrimaryKey,
                Value            = tag,
                CreatedByFk      = createdByFk,
                TimeStampCreated = now,
            });
            changed = true;
        }

        return changed;
    }

    private static void ValidateImageFile(IFormFile? file)
    {
        if (file is null)
            throw new ArgumentException("Image file is required.", nameof(file));
        if (file.Length <= 0)
            throw new ArgumentException("Image file is required.", nameof(file));
        if (file.Length > MaximumImageFileSizeBytes)
            throw new ArgumentException("Image file is too large.", nameof(file));
        if (ImageMimeType.Normalize(file.ContentType) is null)
            throw new ArgumentException("Image file type is not supported.", nameof(file));
    }

    private static async Task<(string Sha256Hash, long Size)> WriteTempFileAndHashAsync(
        Stream input,
        string tempFilePath,
        CancellationToken cancellationToken)
    {
        await using var output = System.IO.File.Create(tempFilePath);
        using var sha256 = SHA256.Create();
        var buffer = new byte[81920];
        long size = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(false);
            if (read <= 0)
                break;

            size += read;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
            sha256.TransformBlock(buffer, 0, read, null, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);
        return (Convert.ToHexString(sha256.Hash ?? []).ToLowerInvariant(), size);
    }

    private static async Task<string?> DetectContentTypeAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var header = new byte[16];
        await using var stream = System.IO.File.OpenRead(filePath);
        var bytesRead = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
        return ImageMimeType.Detect(header.AsSpan(0, bytesRead));
    }

    private static MediaAssetDto ToDto(MediaAsset asset, MediaAssetUsageDto[] usages)
        => new()
        {
            PrimaryKey       = asset.PrimaryKey,
            OriginalFileName = asset.OriginalFileName,
            ContentType      = asset.ContentType,
            Size             = asset.Size,
            ImagePath        = IMediaAssetService.CreateImagePath(asset.PrimaryKey),
            TimeStampCreated = asset.TimeStampCreated,
            TimeStampUpdated = asset.TimeStampUpdated,
            CreatedByName    = asset.CreatedBy?.Nickname,
            Tags = (asset.Tags ?? [])
                .Select(q => q.Value)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Usages = usages,
        };

    private static string NormalizeOriginalFileName(string? fileName)
    {
        var normalized = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return "image";
        return normalized.Length <= 256 ? normalized : normalized[..256];
    }

    private static string GetStoredImageExtension(string contentType)
        => contentType switch
        {
            "image/avif" => ".avif",
            "image/bmp"  => ".bmp",
            "image/gif"  => ".gif",
            "image/jpeg" => ".jpg",
            "image/png"  => ".png",
            "image/webp" => ".webp",
            _            => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null),
        };

    private static bool IsSafeStoredFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;
        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            return false;
        var identifier = Path.GetFileNameWithoutExtension(fileName);
        return identifier.Length == 32 && identifier.All(IsLowerHexCharacter);
    }

    private static bool IsLowerHexCharacter(char value)
        => value is >= '0' and <= '9' or >= 'a' and <= 'f';

    private static bool IsPathInDirectory(string directoryPath, string filePath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedDirectory = Path.GetFullPath(directoryPath)
                                      .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                  + Path.DirectorySeparatorChar;
        var normalizedFilePath = Path.GetFullPath(filePath);
        return normalizedFilePath.StartsWith(normalizedDirectory, comparison);
    }
}
