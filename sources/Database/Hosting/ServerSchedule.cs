using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Meta;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Database.Hosting;

/// <summary>
/// One weekly schedule rule of a game server: an action that happens at a time of day on selected weekdays in the
/// time zone of the owning <see cref="ServerAutomationSettings" />.
/// </summary>
/// <remarks>
/// The primary key is the rule identifier shown to the user. A rule never fires for occurrences before
/// <see cref="EffectiveFrom" />, so a rule created late in the day does not fire retroactively.
/// </remarks>
[Index(nameof(SettingsFk), nameof(Position))]
public sealed class ServerSchedule : IPrimaryKey<Guid>, ITimeStampCreated
{
    /// <inheritdoc />
    [Key]
    public Guid PrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the settings that own this rule. Deleting the settings deletes the rule.
    /// </summary>
    [ForeignKey(nameof(SettingsFk))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public ServerAutomationSettings? Settings { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the settings that own this rule.
    /// </summary>
    public Guid SettingsFk { get; set; }

    /// <summary>
    /// Gets or sets the server this rule belongs to.
    /// </summary>
    [ForeignKey(nameof(GameServerFk))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public GameServer? GameServer { get; set; }

    /// <summary>
    /// Gets or sets the primary key of the server this rule belongs to.
    /// </summary>
    public Guid GameServerFk { get; set; }

    /// <summary>
    /// Gets or sets the action the rule performs when it comes due.
    /// </summary>
    public EServerScheduleAction Action { get; set; }

    /// <summary>
    /// Gets or sets the weekdays the rule applies to as a bit mask where Monday is 1, Tuesday 2, Wednesday 4,
    /// Thursday 8, Friday 16, Saturday 32 and Sunday 64.
    /// </summary>
    public int DaysMask { get; set; }

    /// <summary>
    /// Gets or sets the local time of day in the schedule time zone as minutes after midnight, from 0 to 1439.
    /// </summary>
    public int MinuteOfDay { get; set; }

    /// <summary>
    /// Gets or sets the display position of the rule within its settings.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the instant from which the rule may fire. Earlier occurrences are never evaluated.
    /// </summary>
    public Instant EffectiveFrom { get; set; }

    /// <inheritdoc />
    public Instant TimeStampCreated { get; set; }

    /// <summary>
    /// Gets or sets the recorded occurrences of this rule. Deleting the rule deletes its runs; the audit trail keeps
    /// the history.
    /// </summary>
    public ICollection<ServerScheduleRun> Runs { get; set; } = [];
}
