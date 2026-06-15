using System.Text.Json;
using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.CloudWorker;

internal static class CloudWorkerJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented               = false,
        Converters                  = { new JsonStringEnumConverter() },
    };
}