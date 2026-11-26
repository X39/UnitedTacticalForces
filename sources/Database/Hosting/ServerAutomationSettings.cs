using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Database.Meta;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// Owner-editable automation settings of one game server: idle shutdown, maximum runtime, the schedule time zone,
/// the weekly schedule rules and the consent that allows scheduled starts to cost money.
/// </summary>
/// <remarks>
/// <see cref="Version" /> is the optimistic concurrency token of the settings editor. Only a settings save
/// increments it. Evaluator-owned runtime facts live in <see cref="ServerAutomationState" /> so that automatic
/// evaluation never invalidates an open editor and never causes a conflict response. A server without a settings row
/// has no automation.
/// </remarks>
[Index(nameof(GameServerFk), IsUnique = true)]
public sealed class ServerAutomationSettings : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the server these settings belong to.
    /// </summary>
    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the server these settings belong to.
    /// </summary>
    public Guid GameServerFk { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a running server is stopped after it stayed without connected
    /// players for <see cref="IdleShutdownMinutes" />.
    /// </summary>
    public bool IdleShutdownEnabled { get; set; }

    /// <summary>
    /// Gets or sets the number of minutes without connected players after which the idle shutdown applies.
    /// </summary>
    public int IdleShutdownMinutes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a session is stopped after it ran for <see cref="MaxRuntimeHours" />.
    /// </summary>
    public bool MaxRuntimeEnabled { get; set; }

    /// <summary>
    /// Gets or sets the number of hours after which the maximum runtime limit applies.
    /// </summary>
    public int MaxRuntimeHours { get; set; }

    /// <summary>
    /// Gets or sets the IANA time zone identifier in which the weekly schedule rules are interpreted, or
    /// <see langword="null" /> when no schedule time zone was chosen.
    /// </summary>
    [MaxLength(64)]
    public string? TimeZone { get; set; }

    /// <summary>
    /// Gets or sets the revision counter that only a settings save increments, for optimistic concurrency of the
    /// settings editor.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the cost owner that confirmed scheduled starts, or <see langword="null" /> when no consent was
    /// given. The consent is valid only while this owner still pays for the server.
    /// </summary>
    [ForeignKey(nameof(StartConsentCostOwnerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public CostOwner? StartConsentCostOwner { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the cost owner that confirmed scheduled starts, or <see langword="null" />
    /// when no consent was given.
    /// </summary>
    public Guid? StartConsentCostOwnerFk { get; set; }

    /// <summary>
    /// Gets or sets the user who confirmed scheduled starts.
    /// </summary>
    [ForeignKey(nameof(StartConsentByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? StartConsentBy { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the user who confirmed scheduled starts, or <see langword="null" /> when no
    /// consent was given.
    /// </summary>
    public Guid? StartConsentByFk { get; set; }

    /// <summary>
    /// Gets or sets the instant scheduled starts were confirmed, or <see langword="null" /> when no consent was
    /// given.
    /// </summary>
    public Instant? StartConsentAt { get; set; }

    /// <summary>
    /// Gets or sets the hourly cost estimate in EUR that was shown to the user when the consent was given, or
    /// <see langword="null" /> when no consent was given or no estimate existed.
    /// </summary>
    [Column(TypeName = "numeric(18,6)")]
    public decimal? StartConsentHourlyEstimateEur { get; set; }

    /// <summary>
    /// Gets or sets the user who saved the settings last.
    /// </summary>
    [ForeignKey(nameof(UpdatedByFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public User? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the user who saved the settings last, or <see langword="null" /> when no
    /// user is recorded.
    /// </summary>
    public Guid? UpdatedByFk { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Gets or sets the instant the settings were saved last.
    /// </summary>
    public Instant TimeStampUpdated { get; set; }

    /// <summary>
    /// Gets or sets the weekly schedule rules of the server. Deleting the settings deletes the rules.
    /// </summary>
    public ICollection<ServerSchedule> Schedules { get; set; } = [];
}
