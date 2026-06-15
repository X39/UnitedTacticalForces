using X39.UnitedTacticalForces.Common;
using Xunit;

namespace Api.Tests.Common;

public class TS3AddressBuilderTests
{
    [Fact]
    public void ToStringEncodesQueryValues()
    {
        var address = new TS3AddressBuilder("ts.example.test")
            .WithPassword("pass word&role=admin")
            .WithChannel("Alpha / Bravo", "channel pass+value");

        Assert.Equal(
            "ts3server://ts.example.test?port=9987&password=pass%20word%26role%3Dadmin"
            + "&channel=Alpha%20%2F%20Bravo&channelpassword=channel%20pass%2Bvalue",
            address.ToString());
    }
}
