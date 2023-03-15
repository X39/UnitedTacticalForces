﻿using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JetBrains.Annotations;
using X39.UnitedTacticalForces.Api.Data.Core;

namespace X39.UnitedTacticalForces.Api.Data.Hosting;

[PublicAPI]
public class GameServer
{
    /// <summary>
    /// The primary key of this entity.
    /// </summary>
    [Key]
    public long PrimaryKey { get; set; }

    /// <summary>
    /// The human-readable title of this entity.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>f
    /// The timestamp this entity got created.
    /// </summary>
    public DateTimeOffset TimeStampCreated { get; set; }

    /// <summary>
    /// The timestamp this entity last was upgraded (that is: The application itself got updated).
    /// </summary>
    public DateTimeOffset TimeStampUpgraded { get; set; }

    /// <summary>
    /// The active mod-pack of this entity.
    /// </summary>
    /// <remarks>
    /// This is supposed to be only changed by a controlling service, representing the currently active mod pack only.
    /// Use <see cref="SelectedModPack"/> for choosing a <see cref="ModPack"/>.
    /// </remarks>
    [ForeignKey(nameof(ActiveModPackFk))]
    public ModPack? ActiveModPack { get; set; }

    /// <summary>
    /// Foreign key of <see cref="ActiveModPack"/>.
    /// </summary>
    public long? ActiveModPackFk { get; set; }

    /// <summary>
    /// The <see cref="ModPack"/> that is supposed to be loaded when the server is started.
    /// </summary>
    /// <remarks>
    /// This may differ from <see cref="ActiveModPack"/> if the intended <see cref="ModPack"/> was changed but the
    /// server has not restarted yet.
    /// </remarks>
    [ForeignKey(nameof(SelectedModPackFk))]
    public ModPack? SelectedModPack { get; set; }

    /// <summary>
    /// Foreign key of <see cref="SelectedModPack"/>.
    /// </summary>
    public long? SelectedModPackFk { get; set; }

    /// <summary>
    /// The lifetime state of this entity.
    /// </summary>
    public ELifetimeStatus Status { get; set; }

    /// <summary>
    /// The last known server-version.
    /// </summary>
    [NotMapped]
    public Version Version { get; set; } = new(0, 0, 0, 0);

    /// <summary>
    /// Serialization property for <see cref="Version"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string VersionString
    {
        get => Version.ToString();
        set => Version = Version.Parse(value);
    }
    
    /// <summary>
    /// The <see cref="LifetimeEvent"/>'s of this entity.
    /// </summary>
    [InverseProperty(nameof(LifetimeEvent.GameServer))]
    public ICollection<LifetimeEvent>? LifetimeEvents { get; set; }
    
    /// <summary>
    /// The <see cref="ConfigurationEntry"/>'s of this entity.
    /// </summary>
    [InverseProperty(nameof(ConfigurationEntry.GameServer))]
    public ICollection<ConfigurationEntry>? ConfigurationEntries { get; set; }

    /// <summary>
    /// The game server this represents.
    /// </summary>
    public string ControllerIdentifier { get; set; } = string.Empty;
}