using Xunit;

namespace X39.BI.Config.Parsing.Tests;

public class MissionSqmTests
{

    [Theory]
    [InlineData("mission-1.sqm")]
    [InlineData("borked-string-1.sqm")]
    public void CanParseFile(string fileName)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
        using var streamReader = new StreamReader(file);
        _ = ConfigParser.ParseOrThrow(streamReader);
    }
}
