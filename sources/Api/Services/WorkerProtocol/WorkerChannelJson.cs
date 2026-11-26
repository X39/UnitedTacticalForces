using System.Text.Json;
using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.Api.Services.WorkerProtocol;

/// <summary>
/// Provides the JSON serializer options shared by every worker channel frame and payload on the API side.
/// </summary>
/// <remarks>
/// The options match the worker side: web defaults, case-insensitive property names and enums written as strings.
/// </remarks>
internal static class WorkerChannelJson
{
    /// <summary>The maximum JSON nesting depth accepted for a frame and its payload.</summary>
    private const int MaxDepth = 32;

    /// <summary>Gets the serializer options used to read and write worker channel frames and payloads.</summary>
    /// <value>Web defaults with case-insensitive names, string enums and a nesting limit of 32.</value>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            MaxDepth = MaxDepth,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
