namespace X39.UnitedTacticalForces.Api.Services.HostingRuntime;

internal static class RuntimeInboundNetworkPlanValidator
{
    internal const int MaximumRequirementCount = 32;
    internal const int MaximumDescriptionLength = 100;

    public static string? Validate(IReadOnlyList<RuntimeInboundNetworkRequirement>? requirements)
    {
        if (requirements is null || requirements.Count == 0)
            return "At least one public inbound network requirement is required.";
        if (requirements.Count > MaximumRequirementCount)
        {
            return $"No more than {MaximumRequirementCount} public inbound network requirements are allowed.";
        }

        for (var index = 0; index < requirements.Count; index++)
        {
            var requirement = requirements[index];
            if (requirement is null)
                return $"Inbound network requirement {index + 1} is missing.";
            if (requirement.Protocol is not ERuntimeNetworkProtocol.Tcp and not ERuntimeNetworkProtocol.Udp)
                return $"Inbound network requirement {index + 1} uses an unsupported protocol.";
            if (requirement.StartPort is < 1 or > 65535)
                return $"Inbound network requirement {index + 1} has an invalid start port.";
            if (requirement.EndPort is < 1 or > 65535 || requirement.EndPort < requirement.StartPort)
                return $"Inbound network requirement {index + 1} has an invalid end port.";
            if (string.IsNullOrWhiteSpace(requirement.Description))
                return $"Inbound network requirement {index + 1} requires a description.";
            if (requirement.Description.Length > MaximumDescriptionLength
                || requirement.Description.Any(char.IsControl))
            {
                return $"Inbound network requirement {index + 1} has an invalid description.";
            }
        }

        return null;
    }
}
