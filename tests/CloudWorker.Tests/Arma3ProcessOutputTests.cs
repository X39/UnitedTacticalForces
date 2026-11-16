using X39.UnitedTacticalForces.CloudWorker;
using Xunit;

namespace CloudWorker.Tests;

/// <summary>Checks the privilege and argument boundaries of durable Linux diagnostic capture.</summary>
public sealed class Arma3ProcessOutputTests
{
    /// <summary>Only the restricted child opens the log and untrusted values never become shell source.</summary>
    [Fact]
    public void LinuxOutput_RedirectionFollowsPrivilegeDropAndPreservesLiteralArguments()
    {
        using var directory = new TemporaryDirectory();
        var paths = new Arma3RuntimePaths(Path.Combine(directory.Path, "runtime with spaces"),
            Path.Combine(directory.Path, "state"), Guid.NewGuid(), "arma3server_x64");
        var server = Guid.NewGuid();
        var deployment = Guid.NewGuid();
        var options = new Arma3WorkerOptions("steamcmd", "anonymous", null, null, "arma3server_x64",
            TimeSpan.FromSeconds(30), 1001, 1002, "/usr/bin/setpriv", 1003);
        var controller = new Arma3ProcessController(server, deployment, paths,
            new Arma3ProcessIdentityStore(server, deployment, paths),
            new RestrictedGameProcessLauncher(options, isLinux: true));
        const string password = "-password=literal $(touch unwanted); `command` ' \" space";

        var launch = controller.CreateStartInfo([password, "-name=UTFHC1"], isLinux: true);

        Assert.EndsWith("setpriv", launch.FileName);
        var arguments = launch.ArgumentList.ToArray();
        Assert.Equal("--reuid=1001", arguments[0]);
        Assert.Contains("--no-new-privs", arguments);
        var child = Array.IndexOf(arguments, "utf-arma-output");
        Assert.True(child > Array.IndexOf(arguments, "--no-new-privs"));
        Assert.Equal(Path.GetFullPath("/bin/sh"), arguments[child - 3]);
        Assert.Equal("-c", arguments[child - 2]);
        Assert.DoesNotContain(password, arguments[child - 1]);
        Assert.Contains("exec \"$@\"", arguments[child - 1]);
        Assert.Equal(paths.RuntimeLogPath, arguments[child + 1]);
        Assert.Equal(paths.ExecutablePath, arguments[child + 2]);
        Assert.Equal(password, arguments[child + 3]);
        Assert.False(launch.RedirectStandardOutput);
        Assert.False(launch.RedirectStandardError);
    }
}
