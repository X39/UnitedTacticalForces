﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using X39.UnitedTacticalForces.Api.Data;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    [DbContext(typeof(ApiDbContext))]
    partial class ApiDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("RoleUser", b =>
                {
                    b.Property<long>("RolesPrimaryKey")
                        .HasColumnType("bigint");

                    b.Property<Guid>("UsersPrimaryKey")
                        .HasColumnType("uuid");

                    b.HasKey("RolesPrimaryKey", "UsersPrimaryKey");

                    b.HasIndex("UsersPrimaryKey");

                    b.ToTable("RoleUser");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Authority.Role", b =>
                {
                    b.Property<long>("PrimaryKey")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("PrimaryKey"));

                    b.Property<string>("Category")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Identifier")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("PrimaryKey");

                    b.HasIndex("Identifier")
                        .IsUnique();

                    b.ToTable("Roles");

                    b.HasData(
                        new
                        {
                            PrimaryKey = 1L,
                            Category = "General",
                            Identifier = "admin",
                            Title = "Admin"
                        },
                        new
                        {
                            PrimaryKey = 2L,
                            Category = "Events",
                            Identifier = "event-create",
                            Title = "Create events"
                        },
                        new
                        {
                            PrimaryKey = 3L,
                            Category = "Events",
                            Identifier = "event-modify",
                            Title = "Modify events"
                        },
                        new
                        {
                            PrimaryKey = 4L,
                            Category = "Events",
                            Identifier = "event-delete",
                            Title = "Delete events"
                        },
                        new
                        {
                            PrimaryKey = 5L,
                            Category = "Terrains",
                            Identifier = "terrain-create",
                            Title = "Create terrain"
                        },
                        new
                        {
                            PrimaryKey = 6L,
                            Category = "Terrains",
                            Identifier = "terrain-modify",
                            Title = "Modify terrain"
                        },
                        new
                        {
                            PrimaryKey = 7L,
                            Category = "Terrains",
                            Identifier = "terrain-delete",
                            Title = "Delete terrain"
                        },
                        new
                        {
                            PrimaryKey = 8L,
                            Category = "ModPacks",
                            Identifier = "modpack-create",
                            Title = "Create mod pack"
                        },
                        new
                        {
                            PrimaryKey = 9L,
                            Category = "ModPacks",
                            Identifier = "modpack-modify",
                            Title = "Modify mod pack"
                        },
                        new
                        {
                            PrimaryKey = 10L,
                            Category = "ModPacks",
                            Identifier = "modpack-delete",
                            Title = "Delete mod pack"
                        },
                        new
                        {
                            PrimaryKey = 11L,
                            Category = "User",
                            Identifier = "user-view-steamid64",
                            Title = "View SteamId64 of user"
                        },
                        new
                        {
                            PrimaryKey = 12L,
                            Category = "User",
                            Identifier = "user-view-mail",
                            Title = "View E-Mail of user"
                        },
                        new
                        {
                            PrimaryKey = 13L,
                            Category = "User",
                            Identifier = "user-modify",
                            Title = "Modify user"
                        },
                        new
                        {
                            PrimaryKey = 14L,
                            Category = "User",
                            Identifier = "user-ban",
                            Title = "(Un-)Ban user"
                        },
                        new
                        {
                            PrimaryKey = 15L,
                            Category = "User",
                            Identifier = "user-roles",
                            Title = "Manage user roles"
                        },
                        new
                        {
                            PrimaryKey = 16L,
                            Category = "User",
                            Identifier = "user-list",
                            Title = "List users"
                        },
                        new
                        {
                            PrimaryKey = 17L,
                            Category = "User",
                            Identifier = "user-verify",
                            Title = "Verify users"
                        },
                        new
                        {
                            PrimaryKey = 18L,
                            Category = "Event-Slotting",
                            Identifier = "event-slot-ignore",
                            Title = "Ignore slot rules"
                        },
                        new
                        {
                            PrimaryKey = 19L,
                            Category = "Event-Slotting",
                            Identifier = "event-slot-assign",
                            Title = "Assign slot"
                        },
                        new
                        {
                            PrimaryKey = 20L,
                            Category = "Event-Slotting",
                            Identifier = "event-slot-create",
                            Title = "Create slot"
                        },
                        new
                        {
                            PrimaryKey = 21L,
                            Category = "Event-Slotting",
                            Identifier = "event-slot-update",
                            Title = "Update slot"
                        },
                        new
                        {
                            PrimaryKey = 22L,
                            Category = "Event-Slotting",
                            Identifier = "event-slot-delete",
                            Title = "Delete slot"
                        },
                        new
                        {
                            PrimaryKey = 23L,
                            Category = "Server",
                            Identifier = "server-access",
                            Title = "Server Base Role"
                        },
                        new
                        {
                            PrimaryKey = 24L,
                            Category = "Server",
                            Identifier = "server-start-stop",
                            Title = "Server start/stop"
                        },
                        new
                        {
                            PrimaryKey = 25L,
                            Category = "Server",
                            Identifier = "server-create-delete",
                            Title = "Server create/delete"
                        },
                        new
                        {
                            PrimaryKey = 26L,
                            Category = "Server",
                            Identifier = "server-update",
                            Title = "Server config"
                        },
                        new
                        {
                            PrimaryKey = 27L,
                            Category = "Server",
                            Identifier = "server-upgrade",
                            Title = "Server upgrade"
                        },
                        new
                        {
                            PrimaryKey = 28L,
                            Category = "Server",
                            Identifier = "server-change-modpack",
                            Title = "Server mod-pack"
                        });
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Authority.User", b =>
                {
                    b.Property<Guid>("PrimaryKey")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<byte[]>("Avatar")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<string>("AvatarMimeType")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("EMail")
                        .HasColumnType("text");

                    b.Property<bool>("IsBanned")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsVerified")
                        .HasColumnType("boolean");

                    b.Property<string>("Nickname")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<decimal>("SteamId64")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("PrimaryKey");

                    b.HasIndex("Nickname");

                    b.HasIndex("SteamId64")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Authority.UserEventMeta", b =>
                {
                    b.Property<Guid>("UserFk")
                        .HasColumnType("uuid");

                    b.Property<Guid>("EventFk")
                        .HasColumnType("uuid");

                    b.Property<int>("Acceptance")
                        .HasColumnType("integer");

                    b.HasKey("UserFk", "EventFk");

                    b.HasIndex("EventFk");

                    b.ToTable("UserEventMetas");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Authority.UserModPackMeta", b =>
                {
                    b.Property<Guid>("UserFk")
                        .HasColumnType("uuid");

                    b.Property<long>("ModPackFk")
                        .HasColumnType("bigint");

                    b.Property<DateTimeOffset>("TimeStampDownloaded")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("UserFk", "ModPackFk");

                    b.HasIndex("ModPackFk");

                    b.ToTable("UserModPackMetas");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Core.ModPack", b =>
                {
                    b.Property<long>("PrimaryKey")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("PrimaryKey"));

                    b.Property<string>("Html")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<Guid>("OwnerFk")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("TimeStampCreated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("TimeStampUpdated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("PrimaryKey");

                    b.HasIndex("OwnerFk");

                    b.ToTable("ModPacks");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Core.Terrain", b =>
                {
                    b.Property<long>("PrimaryKey")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("PrimaryKey"));

                    b.Property<byte[]>("Image")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<string>("ImageMimeType")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("PrimaryKey");

                    b.ToTable("Terrains");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Eventing.Event", b =>
                {
                    b.Property<Guid>("PrimaryKey")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("AcceptedCount")
                        .HasColumnType("integer");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("HostedByFk")
                        .HasColumnType("uuid");

                    b.Property<byte[]>("Image")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<string>("ImageMimeType")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("MaybeCount")
                        .HasColumnType("integer");

                    b.Property<int>("MinimumAccepted")
                        .HasColumnType("integer");

                    b.Property<long>("ModPackFk")
                        .HasColumnType("bigint");

                    b.Property<Guid>("OwnerFk")
                        .HasColumnType("uuid");

                    b.Property<int>("RejectedCount")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset>("ScheduledFor")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("ScheduledForOriginal")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("TerrainFk")
                        .HasColumnType("bigint");

                    b.Property<DateTimeOffset>("TimeStampCreated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("PrimaryKey");

                    b.HasIndex("HostedByFk");

                    b.HasIndex("ModPackFk");

                    b.HasIndex("OwnerFk");

                    b.HasIndex("ScheduledFor");

                    b.HasIndex("TerrainFk");

                    b.ToTable("Events");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Eventing.EventSlot", b =>
                {
                    b.Property<Guid>("EventFk")
                        .HasColumnType("uuid");

                    b.Property<short>("SlotNumber")
                        .HasColumnType("smallint");

                    b.Property<Guid?>("AssignedToFk")
                        .HasColumnType("uuid");

                    b.Property<string>("Group")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("IsSelfAssignable")
                        .HasColumnType("boolean");

                    b.Property<int>("Side")
                        .HasColumnType("integer");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("EventFk", "SlotNumber");

                    b.HasIndex("AssignedToFk");

                    b.ToTable("EventSlots");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Hosting.ConfigurationEntry", b =>
                {
                    b.Property<long>("PrimaryKey")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("PrimaryKey"));

                    b.Property<Guid>("ChangedByFk")
                        .HasColumnType("uuid");

                    b.Property<long>("GameServerFk")
                        .HasColumnType("bigint");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsSensitive")
                        .HasColumnType("boolean");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Realm")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("TimeStamp")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("PrimaryKey");

                    b.HasIndex("ChangedByFk");

                    b.HasIndex("GameServerFk");

                    b.HasIndex("IsActive");

                    b.HasIndex("Realm", "Path");

                    b.ToTable("ConfigurationEntries");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Hosting.GameServer", b =>
                {
                    b.Property<long>("PrimaryKey")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("PrimaryKey"));

                    b.Property<long?>("ActiveModPackFk")
                        .HasColumnType("bigint");

                    b.Property<string>("ControllerIdentifier")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long?>("SelectedModPackFk")
                        .HasColumnType("bigint");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset>("TimeStampCreated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("TimeStampUpgraded")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("VersionString")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("PrimaryKey");

                    b.HasIndex("ActiveModPackFk");

                    b.HasIndex("SelectedModPackFk");

                    b.ToTable("GameServers");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Hosting.GameServerLog", b =>
                {
                    b.Property<long>("PrimaryKey")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("PrimaryKey"));

                    b.Property<int>("LogLevel")
                        .HasColumnType("integer");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Source")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("TimeStamp")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("PrimaryKey");

                    b.HasIndex("TimeStamp");

                    b.ToTable("GameServerLogs");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Hosting.LifetimeEvent", b =>
                {
                    b.Property<long>("PrimaryKey")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("PrimaryKey"));

                    b.Property<Guid?>("ExecutedByFk")
                        .HasColumnType("uuid");

                    b.Property<long>("GameServerFk")
                        .HasColumnType("bigint");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset>("TimeStamp")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("PrimaryKey");

                    b.HasIndex("ExecutedByFk");

                    b.HasIndex("GameServerFk");

                    b.ToTable("LifetimeEvents");
                });

            modelBuilder.Entity("RoleUser", b =>
                {
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.Role", null)
                        .WithMany()
                        .HasForeignKey("RolesPrimaryKey")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", null)
                        .WithMany()
                        .HasForeignKey("UsersPrimaryKey")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Authority.UserEventMeta", b =>
                {
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Eventing.Event", "Event")
                        .WithMany("UserMetas")
                        .HasForeignKey("EventFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", "User")
                        .WithMany("EventMetas")
                        .HasForeignKey("UserFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Event");

                    b.Navigation("User");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Authority.UserModPackMeta", b =>
                {
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Core.ModPack", "ModPack")
                        .WithMany("UserMetas")
                        .HasForeignKey("ModPackFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", "User")
                        .WithMany("ModPackMetas")
                        .HasForeignKey("UserFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ModPack");

                    b.Navigation("User");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Core.ModPack", b =>
                {
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", "Owner")
                        .WithMany()
                        .HasForeignKey("OwnerFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Owner");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Eventing.Event", b =>
                {
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", "HostedBy")
                        .WithMany()
                        .HasForeignKey("HostedByFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Core.ModPack", "ModPack")
                        .WithMany()
                        .HasForeignKey("ModPackFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", "Owner")
                        .WithMany()
                        .HasForeignKey("OwnerFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Core.Terrain", "Terrain")
                        .WithMany()
                        .HasForeignKey("TerrainFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("HostedBy");

                    b.Navigation("ModPack");

                    b.Navigation("Owner");

                    b.Navigation("Terrain");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Eventing.EventSlot", b =>
                {
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", "AssignedTo")
                        .WithMany("EventSlots")
                        .HasForeignKey("AssignedToFk");

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Eventing.Event", "Event")
                        .WithMany()
                        .HasForeignKey("EventFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("AssignedTo");

                    b.Navigation("Event");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Hosting.ConfigurationEntry", b =>
                {
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", "ChangedBy")
                        .WithMany()
                        .HasForeignKey("ChangedByFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Hosting.GameServer", "GameServer")
                        .WithMany("ConfigurationEntries")
                        .HasForeignKey("GameServerFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ChangedBy");

                    b.Navigation("GameServer");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Hosting.GameServer", b =>
                {
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Core.ModPack", "ActiveModPack")
                        .WithMany()
                        .HasForeignKey("ActiveModPackFk");

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Core.ModPack", "SelectedModPack")
                        .WithMany()
                        .HasForeignKey("SelectedModPackFk");

                    b.Navigation("ActiveModPack");

                    b.Navigation("SelectedModPack");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Hosting.LifetimeEvent", b =>
                {
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", "ExecutedBy")
                        .WithMany()
                        .HasForeignKey("ExecutedByFk");

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Hosting.GameServer", "GameServer")
                        .WithMany("LifetimeEvents")
                        .HasForeignKey("GameServerFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ExecutedBy");

                    b.Navigation("GameServer");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Authority.User", b =>
                {
                    b.Navigation("EventMetas");

                    b.Navigation("EventSlots");

                    b.Navigation("ModPackMetas");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Core.ModPack", b =>
                {
                    b.Navigation("UserMetas");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Eventing.Event", b =>
                {
                    b.Navigation("UserMetas");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Hosting.GameServer", b =>
                {
                    b.Navigation("ConfigurationEntries");

                    b.Navigation("LifetimeEvents");
                });
#pragma warning restore 612, 618
        }
    }
}
