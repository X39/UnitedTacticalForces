using Xunit;

namespace X39.BI.Config.Parsing.Tests;

public class MalformedConfigTests
{
    [Theory]
    [InlineData("class ;")]
    [InlineData("= 1;")]
    [InlineData("valid = 1; truncated =")]
    public void ParseOrThrowRejectsMalformedInput(string input)
    {
        Assert.Throws<FormatException>(() => ConfigParser.ParseOrThrow(input));
    }
}
