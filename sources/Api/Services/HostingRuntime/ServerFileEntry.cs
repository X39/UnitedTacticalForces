using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

public sealed record ServerFileEntry(
    EFileContext Context,
    string RelativePath,
    string Name,
    bool IsDirectory,
    long? Size,
    DateTimeOffset ModifiedAt,
    EFileClassification Classification,
    bool IsWritable);