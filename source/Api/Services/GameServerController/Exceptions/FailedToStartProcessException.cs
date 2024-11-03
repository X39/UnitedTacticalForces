using System.Diagnostics;
using JetBrains.Annotations;

namespace X39.UnitedTacticalForces.Api.Services.GameServerController;

internal class FailedToStartProcessException : Exception
{
    [PublicAPI]
    public ProcessStartInfo StartInfo { get; }

    public FailedToStartProcessException(ProcessStartInfo startInfo) : base(
        "A process was attempted to be started but the starting failed.")
    {
        StartInfo = startInfo;
    }
}