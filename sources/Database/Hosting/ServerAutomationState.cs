using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Evaluator-owned runtime state of the automation of one game server.
/// </summary>
/// <remarks>
/// Only the automation evaluator writes this row. It is kept apart from <see cref="ServerAutomationSettings" /> so
/// that evaluation passes never change the settings <see cref="ServerAutomationSettings.Version" /> and therefore
/// never make an open settings editor stale.
/// </remarks>
[Index(nameof(GameServerFk), IsUnique = true)]
public sealed class ServerAutomationState : IPrimaryKey<Guid>
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the server this state belongs to.
    /// </summary>
    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the server this state belongs to.
    /// </summary>
    public Guid GameServerFk { get; set; }

    /// <summary>
    /// Gets or sets the deployment the idle timer currently measures, or <see langword="null" /> when no idle timer
    /// runs. A new deployment restarts the timer.
    /// </summary>
    [ForeignKey(nameof(IdleDeploymentFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServerDeployment? IdleDeployment { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the deployment the idle timer currently measures, or
    /// <see langword="null" /> when no idle timer runs.
    /// </summary>
    public Guid? IdleDeploymentFk { get; set; }

    /// <summary>
    /// Gets or sets the instant from which the server was observed without connected players, or
    /// <see langword="null" /> when players are connected or the idle timer does not run.
    /// </summary>
    public Instant? IdleSince { get; set; }

    /// <summary>
    /// Gets or sets the instant from which the idle timer is held because player information is unavailable, or
    /// <see langword="null" /> when the timer is not held.
    /// </summary>
    public Instant? IdleHeldSince { get; set; }

    /// <summary>
    /// Gets or sets the instant up to which schedule occurrences were evaluated, or <see langword="null" /> before
    /// the first evaluation.
    /// </summary>
    public Instant? SchedulesEvaluatedThrough { get; set; }

    /// <summary>
    /// Gets or sets the instant the state changed last.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }
}
