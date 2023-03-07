using Xunit;

namespace X39.BI.Config.Parsing.Tests;

public class MissionSqmTests
{

    [Theory]
    [InlineData(@"Resources\mission-1.sqm")]
    [InlineData(@"Resources\borked-string-1.sqm")]
    public void CanParseFile(string file)
    {
        using var streamReader = new StreamReader(file);
        _ = ConfigParser.ParseOrThrow(streamReader);
    }
}