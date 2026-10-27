using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.Middleware;

/// <summary>Prevents stale clients and external workers from mutating paused hosting runtime state.</summary>
/// <param name="next">The next request handler after the pause boundary.</param>
public sealed class HostingControlPlanePauseMiddleware(RequestDelegate next)
{
    /// <summary>Returns an explicit unavailable response instead of accepting deferred hosting work.</summary>
    /// <param name="context">The current API request.</param>
    /// <param name="state">The same immutable startup state used to register hosting processors.</param>
    /// <returns>A task completing after rejection or the downstream request handler.</returns>
    public async Task InvokeAsync(HttpContext context, HostingControlPlaneState state)
    {
        var path = context.Request.Path;
        var isWorkerRequest = path.StartsWithSegments("/lifecycle-workers", StringComparison.OrdinalIgnoreCase);
        var isManagementRequest = path.StartsWithSegments("/game-servers", StringComparison.OrdinalIgnoreCase)
                                  || path.StartsWithSegments("/jobs", StringComparison.OrdinalIgnoreCase);
        var isRead = HttpMethods.IsGet(context.Request.Method)
                     || HttpMethods.IsHead(context.Request.Method)
                     || HttpMethods.IsOptions(context.Request.Method);
        if (state.IsPaused && (isWorkerRequest || isManagementRequest && !isRead))
        {
            context.Response.Headers.CacheControl = "no-store";
            await Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: HostingControlPlaneState.PausedReason,
                    extensions: new Dictionary<string, object?> { ["code"] = "hosting_control_plane_paused" })
                .ExecuteAsync(context).ConfigureAwait(false);
            return;
        }
        await next(context).ConfigureAwait(false);
    }
}
