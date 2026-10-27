namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>Checks the effective hosting pause without constructing execution services or accessing a database.</summary>
public static class HostingControlPlaneConfigurationCheck
{
    /// <summary>Emits only the verified paused state; invalid or enabled configurations fail without output.</summary>
    /// <param name="configuration">The API's fully loaded configuration with its normal provider precedence.</param>
    /// <param name="output">The destination for the single nonsecret success response.</param>
    /// <returns>Zero if hosting is paused; otherwise one.</returns>
    public static int Run(IConfiguration configuration, TextWriter output)
    {
        HostingControlPlaneState state;
        try
        {
            state = new HostingControlPlaneState(configuration);
        }
        catch (InvalidOperationException)
        {
            return 1;
        }
        if (!state.IsPaused)
            return 1;
        output.WriteLine("{\"paused\":true}");
        return 0;
    }
}
