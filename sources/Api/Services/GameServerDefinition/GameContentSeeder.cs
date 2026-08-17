using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Core;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

public static class GameContentSeeder
{
    public const string CounterStrike2Dust2Identifier = "de_dust2";
    public const string CounterStrike2CompetitiveIdentifier = "competitive";

    public static async Task SeedAsync(DatabaseContext dbContext, CancellationToken cancellationToken = default)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var dust2 = await dbContext.Terrains
            .Include(q => q.Versions)
            .SingleOrDefaultAsync(
                q => q.GameIdentifier == CounterStrike2GameServerDefinitionRegistration.GameIdentifier
                     && q.Title == "Dust II",
                cancellationToken)
            .ConfigureAwait(false);
        if (dust2 is null)
        {
            dust2 = new Terrain
            {
                GameIdentifier = CounterStrike2GameServerDefinitionRegistration.GameIdentifier,
                Title = "Dust II",
                IsActive = true,
            };
            dbContext.Terrains.Add(dust2);
        }
        if (dust2.Versions.Count == 0)
        {
            dust2.Versions.Add(new TerrainVersion
            {
                PrimaryKey = Guid.NewGuid(),
                VersionNumber = 1,
                VersionLabel = "Built-in",
                SourceKind = EContentSourceKind.BuiltIn,
                SourceIdentifier = CounterStrike2Dust2Identifier,
                Metadata = "{\"mapIdentifier\":\"de_dust2\"}",
                TimeStampCreated = now,
            });
        }

        foreach (var mode in Modes)
        {
            var scenario = await dbContext.Scenarios
                .Include(q => q.Versions)
                .SingleOrDefaultAsync(
                    q => q.GameIdentifier == CounterStrike2GameServerDefinitionRegistration.GameIdentifier
                         && q.Title == mode.Title,
                    cancellationToken)
                .ConfigureAwait(false);
            if (scenario is null)
            {
                scenario = new Scenario
                {
                    PrimaryKey = Guid.NewGuid(),
                    GameIdentifier = CounterStrike2GameServerDefinitionRegistration.GameIdentifier,
                    Title = mode.Title,
                    Description = mode.Description,
                    IsActive = true,
                    TimeStampCreated = now,
                };
                dbContext.Scenarios.Add(scenario);
            }
            if (scenario.Versions.Count == 0)
            {
                scenario.Versions.Add(new ScenarioVersion
                {
                    PrimaryKey = Guid.NewGuid(),
                    VersionNumber = 1,
                    VersionLabel = "Built-in",
                    SourceKind = EContentSourceKind.BuiltIn,
                    SourceIdentifier = mode.Identifier,
                    Metadata = $"{{\"gameType\":{mode.GameType},\"gameMode\":{mode.GameMode}}}",
                    TimeStampCreated = now,
                });
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static readonly (string Identifier, string Title, string Description, int GameType, int GameMode)[] Modes =
    [
        (CounterStrike2CompetitiveIdentifier, "Competitive", "Classic competitive rules.", 0, 1),
        ("casual", "Casual", "Classic casual rules.", 0, 0),
        ("wingman", "Wingman", "Two-versus-two competitive rules.", 0, 2),
        ("deathmatch", "Deathmatch", "Free-form deathmatch rules.", 1, 2),
        ("arms-race", "Arms Race", "Progressive weapon rules.", 1, 0),
    ];
}
