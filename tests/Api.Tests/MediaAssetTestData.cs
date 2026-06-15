using X39.UnitedTacticalForces.Database.Media;

namespace Api.Tests;

internal static class MediaAssetTestData
{
    public static MediaAsset Create(Guid primaryKey, string fileName = "test-image.png")
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        return new MediaAsset
        {
            PrimaryKey       = primaryKey,
            OriginalFileName = fileName,
            ContentType      = "image/png",
            Size             = 1,
            StorageFileName  = $"{primaryKey:N}.png",
            Sha256Hash       = primaryKey.ToString("N"),
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
    }
}
