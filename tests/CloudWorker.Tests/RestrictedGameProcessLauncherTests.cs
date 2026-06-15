using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

public sealed class RestrictedGameProcessLauncherTests
{
    [Fact]
    public void ProductionIdentity_UsesSetprivBeforeExecutableAndClearsSupplementaryGroups()
    {
        var options = WorkerOptions() with
        {
            GameUserId = 10002,
            GameGroupId = 10001,
            PrivilegeDropPath = "/usr/bin/setpriv",
        };
        var launcher = new RestrictedGameProcessLauncher(options, isLinux: true);

        var startInfo = launcher.Create(
            "/srv/runtime/server-instance/arma3server_x64",
            "/srv/runtime/server-instance",
            ["-port=2302", "-name=UTF Server"]);

        Assert.Equal(Path.GetFullPath("/usr/bin/setpriv"), startInfo.FileName);
        Assert.Equal(
            [
                "--reuid=10002",
                "--regid=10001",
                "--clear-groups",
                "--no-new-privs",
                "/bin/sh",
                "-c",
                "umask 0007; exec \"$@\"",
                "utf-game",
                Path.GetFullPath("/srv/runtime/server-instance/arma3server_x64"),
                "-port=2302",
                "-name=UTF Server",
            ],
            startInfo.ArgumentList);
    }

    [Fact]
    public void MissingHalfOfIdentity_FailsClosed()
    {
        var launcher = new RestrictedGameProcessLauncher(
            WorkerOptions() with
            {
                GameUserId = 10002,
                GameGroupId = null,
                PrivilegeDropPath = "/usr/bin/setpriv",
            },
            isLinux: true);

        Assert.Throws<InvalidOperationException>(() => launcher.Create("game", ".", []));
    }

    [Fact]
    public void SteamCmd_UsesDedicatedUserInsteadOfGameIdentity()
    {
        var options = WorkerOptions() with
        {
            GameUserId = 10002,
            GameGroupId = 10001,
            SteamUserId = 10003,
            PrivilegeDropPath = "/usr/bin/setpriv",
        };
        var launcher = new RestrictedGameProcessLauncher(options, isLinux: true);

        var startInfo = launcher.CreateSteamCmd("/srv/tooling/steamcmd.sh", "/srv/tooling", ["+runscript", "/tmp/run"]);

        Assert.Equal("--reuid=10003", startInfo.ArgumentList[0]);
        Assert.Equal("--regid=10001", startInfo.ArgumentList[1]);
        Assert.Equal("utf-steam", startInfo.ArgumentList[7]);
    }

    [Fact]
    public void RestrictedGameIdentityWithoutDedicatedSteamIdentity_FailsClosed()
    {
        var options = WorkerOptions() with
        {
            GameUserId = 10002,
            GameGroupId = 10001,
            PrivilegeDropPath = "/usr/bin/setpriv",
        };
        var launcher = new RestrictedGameProcessLauncher(options, isLinux: true);

        Assert.Throws<InvalidOperationException>(() => launcher.CreateSteamCmd("steamcmd", ".", []));
    }

    private static Arma3WorkerOptions WorkerOptions()
        => new(
            "steamcmd",
            "anonymous",
            null,
            null,
            "arma3server_x64",
            TimeSpan.FromSeconds(30));
}
