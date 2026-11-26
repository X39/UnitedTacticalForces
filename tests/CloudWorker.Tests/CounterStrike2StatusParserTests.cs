using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Verifies how the Counter-Strike 2 RCON status response is turned into a human player count.</summary>
public sealed class CounterStrike2StatusParserTests
{
    /// <summary>Verifies that a players summary line yields the humans and the capacity.</summary>
    /// <param name="text">The status response.</param>
    /// <param name="expectedHumans">The expected human count.</param>
    /// <param name="expectedCapacity">The expected capacity.</param>
    [Theory]
    [InlineData("players  : 3 humans, 1 bots (16 max) (not hibernating)", 3, 16)]
    [InlineData("players:0 humans,0 bots(0 max)", 0, 0)]
    [InlineData("hostname: UTF\nplayers : 12 humans, 4 bots (24 max)\nspawn_group 1", 12, 24)]
    [InlineData("PLAYERS \t: 1 human", 1, null)]
    [InlineData("players : 7 humans, 0 bots", 7, null)]
    [InlineData("players : 1024 humans, 0 bots (1024 max)", 1024, 1024)]
    public void TryParse_PlayersLine_ReturnsHumansAndCapacity(string text, int expectedHumans, int? expectedCapacity)
    {
        var parsed = CounterStrike2StatusParser.TryParse(text, out var humans, out var capacity);

        Assert.True(parsed);
        Assert.Equal(expectedHumans, humans);
        Assert.Equal(expectedCapacity, capacity);
    }

    /// <summary>Verifies that anything that is not a players summary line never yields a number.</summary>
    /// <param name="text">The status response.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hostname: UTF\nversion : 1.0.0")]
    [InlineData("players : many humans, 0 bots (16 max)")]
    [InlineData("players : -1 humans, 0 bots (16 max)")]
    [InlineData("players : 1025 humans, 0 bots (2000 max)")]
    [InlineData("players : 3 humans, 0 bots (1025 max)")]
    [InlineData("players : 99999999999 humans")]
    [InlineData("players : 3 bots")]
    public void TryParse_NotAPlayersLine_ReturnsFalseWithoutNumber(string? text)
    {
        var parsed = CounterStrike2StatusParser.TryParse(text, out var humans, out var capacity);

        Assert.False(parsed);
        Assert.Equal(0, humans);
        Assert.Null(capacity);
    }

    /// <summary>Verifies that a hostile oversized reply is rejected within the match timeout and without throwing.</summary>
    [Fact]
    public void TryParse_HugeHostileInput_DoesNotThrow()
    {
        var text = string.Concat(Enumerable.Repeat("players : 1 ", 6000));

        var parsed = CounterStrike2StatusParser.TryParse(text, out var humans, out _);

        Assert.False(parsed);
        Assert.Equal(0, humans);
    }
}
