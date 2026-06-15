using X39.UnitedTacticalForces.Api.Services.HostingRuntime;
using Xunit;

namespace Api.Tests.GameServerHosting;

public sealed class SafeServerPathTests
{
    [Theory]
    [InlineData("../outside")]
    [InlineData("folder/../../outside")]
    [InlineData("C:/Windows/system.ini")]
    [InlineData("/etc/passwd")]
    [InlineData(".ssh/id_rsa")]
    [InlineData("config/appsettings.secret.json")]
    public void Normalize_RejectsHostAndSecretPaths(string path)
        => Assert.ThrowsAny<ArgumentException>(() => SafeServerPath.Normalize(path));

    [Theory]
    [InlineData("profiles/user.vars", "profiles/user.vars")]
    [InlineData("profiles\\user.vars", "profiles/user.vars")]
    [InlineData(" saves / current ", "saves/current")]
    public void Normalize_ReturnsPortableServerRelativePath(string path, string expected)
        => Assert.Equal(expected, SafeServerPath.Normalize(path));
}
