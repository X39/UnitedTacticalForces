using X39.UnitedTacticalForces.Api.Services.GameServerDefinition;
using X39.UnitedTacticalForces.Api.Services.ServerAutomation;
using Xunit;

namespace Api.Tests.ServerAutomation;

/// <summary>
/// Covers validation of the <c>Hosting:Automation</c> options and the player count capability of the game
/// definitions.
/// </summary>
public sealed class ServerAutomationOptionsTests
{
    /// <summary>The defaults are usable as shipped.</summary>
    [Fact]
    public void Validate_DefaultOptions_Passes()
    {
        var options = new ServerAutomationOptions();

        options.Validate();

        Assert.Equal("Hosting:Automation", ServerAutomationOptions.SectionName);
        Assert.Equal(TimeSpan.FromSeconds(15), options.EvaluationInterval);
        Assert.Equal(TimeSpan.FromMinutes(10), options.WarningLead);
        Assert.Equal(TimeSpan.FromMinutes(15), options.StartGraceWindow);
    }

    /// <summary>The short values used by manual acceptance runs are admitted.</summary>
    [Fact]
    public void Validate_UatValues_Passes()
    {
        var options = new ServerAutomationOptions
        {
            IdleMinutesMin = 1,
            WarningLead = TimeSpan.FromMinutes(1),
            EvaluationInterval = TimeSpan.FromSeconds(5),
        };

        options.Validate();
    }

    /// <summary>Each unusable setting is rejected.</summary>
    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Validate_InvalidOptions_Throws(string name, Action<ServerAutomationOptions> mutate)
    {
        var options = new ServerAutomationOptions();
        mutate(options);

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Hosting:Automation contains invalid settings.", exception.Message);
        Assert.False(string.IsNullOrEmpty(name));
    }

    /// <summary>Supplies the unusable option variations.</summary>
    /// <returns>Pairs of a case name and a mutation that makes the options invalid.</returns>
    public static TheoryData<string, Action<ServerAutomationOptions>> InvalidOptions() => new()
    {
        { "interval zero", o => o.EvaluationInterval = TimeSpan.Zero },
        { "interval negative", o => o.EvaluationInterval = TimeSpan.FromSeconds(-1) },
        { "lead below 30 s", o => o.WarningLead = TimeSpan.FromSeconds(29) },
        { "lead above 60 min", o => o.WarningLead = TimeSpan.FromMinutes(61) },
        { "grace negative", o => o.StartGraceWindow = TimeSpan.FromSeconds(-1) },
        { "idle min below 1", o => o.IdleMinutesMin = 0 },
        { "idle min above max", o => o.IdleMinutesMin = o.IdleMinutesMax + 1 },
        { "runtime min below 1", o => o.MaxRuntimeHoursMin = 0 },
        { "runtime min above max", o => o.MaxRuntimeHoursMin = o.MaxRuntimeHoursMax + 1 },
        { "schedules zero", o => o.MaxSchedules = 0 },
        { "schedules above 32", o => o.MaxSchedules = 33 },
        { "default idle below min", o => o.DefaultIdleMinutes = o.IdleMinutesMin - 1 },
        { "default idle above max", o => o.DefaultIdleMinutes = o.IdleMinutesMax + 1 },
        { "default runtime below min", o => o.DefaultMaxRuntimeHours = o.MaxRuntimeHoursMin - 1 },
        { "default runtime above max", o => o.DefaultMaxRuntimeHours = o.MaxRuntimeHoursMax + 1 },
    };

    /// <summary>Every shipped game declares that its worker reports a player count.</summary>
    [Fact]
    public void Definitions_AllShippedGames_ReportPlayerCount()
    {
        Assert.True(new Arma3GameServerDefinitionRegistration().Definition.ReportsPlayerCount);
        Assert.True(new AskaGameServerDefinitionRegistration().Definition.ReportsPlayerCount);
        Assert.True(new CounterStrike2GameServerDefinitionRegistration().Definition.ReportsPlayerCount);
    }
}
