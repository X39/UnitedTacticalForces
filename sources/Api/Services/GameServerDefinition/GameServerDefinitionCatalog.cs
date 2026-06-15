using X39.Util.DependencyInjection.Attributes;
using X39.UnitedTacticalForces.Api.Services.HostingRuntime;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

[Singleton<GameServerDefinitionCatalog, IGameServerDefinitionCatalog>]
public sealed class GameServerDefinitionCatalog : IGameServerDefinitionCatalog
{
    private readonly IReadOnlyList<GameServerDefinition> _definitions;

    public GameServerDefinitionCatalog(IEnumerable<IGameServerDefinitionRegistration> registrations)
    {
        var materialized = registrations.ToArray();
        var duplicate = materialized
            .GroupBy(q => q.Definition.GameIdentifier, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(q => q.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Game identifier '{duplicate.Key}' has more than one definition registration.");
        }

        foreach (var registration in materialized)
        {
            var actionIds = registration.Definition.Actions
                .Select(q => q.ActionId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!actionIds.Contains(GameServerActionIds.Start) || !actionIds.Contains(GameServerActionIds.Stop))
            {
                throw new InvalidOperationException(
                    $"Game definition '{registration.Definition.GameIdentifier}' must expose Start and Stop actions.");
            }

            var networkFailure = RuntimeInboundNetworkPlanValidator.Validate(
                registration.Definition.InboundNetworkRequirements);
            if (networkFailure is not null)
            {
                throw new InvalidOperationException(
                    $"Game definition '{registration.Definition.GameIdentifier}' has an invalid inbound network "
                    + $"plan: {networkFailure}");
            }

            if (registration.Definition.PrimaryConnection is { } connection
                && !registration.Definition.InboundNetworkRequirements.Any(q =>
                    q.Protocol == connection.Protocol
                    && connection.Port >= q.StartPort
                    && connection.Port <= q.EndPort))
            {
                throw new InvalidOperationException(
                    $"Game definition '{registration.Definition.GameIdentifier}' has a primary connection outside "
                    + "its inbound network plan.");
            }
        }

        _definitions = materialized
            .Select(q => q.Definition)
            .OrderBy(q => q.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<GameServerDefinition> GetDefinitions() => _definitions;
}