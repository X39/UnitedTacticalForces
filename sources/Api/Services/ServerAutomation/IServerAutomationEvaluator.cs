namespace X39.UnitedTacticalForces.Api.Services.ServerAutomation;

/// <summary>
/// Evaluates the automatic stop rules of every server that has automation switched on: it opens, withdraws and
/// executes automatic stop warnings.
/// </summary>
/// <remarks>
/// The evaluator keeps no state between passes. Every decision is recomputed from PostgreSQL and the injected
/// instant, so a restart of the API or a second instance neither loses nor repeats a stop.
/// </remarks>
internal interface IServerAutomationEvaluator
{
    /// <summary>
    /// Runs one evaluation pass over all servers with automation.
    /// </summary>
    /// <param name="now">The instant the pass is evaluated at; the evaluator never reads a clock itself.</param>
    /// <param name="cancellationToken">A token that cancels the pass between and within servers.</param>
    /// <returns>
    /// A task resolving to the alerts that committed transactions raised and withdrew, to be published after the
    /// pass. A failure of one server is logged and does not stop the other servers.
    /// </returns>
    Task<ServerAutomationEvaluationResult> EvaluateAsync(Instant now, CancellationToken cancellationToken);
}
