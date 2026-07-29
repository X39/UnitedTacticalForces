using System.ComponentModel.DataAnnotations;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

public sealed record GameServerRuntimeStorageUpdatePayload
{
    [EnumDataType(typeof(ERuntimeStorageStrategy))]
    public ERuntimeStorageStrategy Strategy { get; init; }

    public long? ExternalStorageSizeGiB { get; init; }
}
