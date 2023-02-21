﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using X39.UnitedTacticalForces.Api.Data;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    [DbContext(typeof(ApiDbContext))]
    [Migration("20230221004000_RenamingPrivilegesToRoles")]
    partial class RenamingPrivilegesToRoles
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("RoleUser", b =>
                {
                    b.Property<long>("RolesId")
                        .HasColumnType("bigint");

                    b.Property<Guid>("UsersId")
                        .HasColumnType("uuid");

                    b.HasKey("RolesId", "UsersId");

                    b.HasIndex("UsersId");

                    b.ToTable("RoleUser");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Authority.Role", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<string>("Category")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ClaimCode")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("ClaimCode")
                        .IsUnique();

                    b.ToTable("Privileges");

                    b.HasData(
                        new
                        {
                            Id = 1L,
                            Category = "Events",
                            ClaimCode = "event-create",
                            Title = "Events erstellen"
                        },
                        new
                        {
                            Id = 2L,
                            Category = "Events",
                            ClaimCode = "event-modify",
                            Title = "Alle events bearbeiten"
                        },
                        new
                        {
                            Id = 3L,
                            Category = "Events",
                            ClaimCode = "event-delete",
                            Title = "Alle events löschen"
                        },
                        new
                        {
                            Id = 4L,
                            Category = "Terrains",
                            ClaimCode = "terrain-create",
                            Title = "Terrain anlegen"
                        },
                        new
                        {
                            Id = 5L,
                            Category = "Terrains",
                            ClaimCode = "terrain-modify",
                            Title = "Terrain bearbeiten"
                        },
                        new
                        {
                            Id = 6L,
                            Category = "Terrains",
                            ClaimCode = "terrain-delete",
                            Title = "Terrain löschen"
                        },
                        new
                        {
                            Id = 7L,
                            Category = "ModPacks",
                            ClaimCode = "modpack-create",
                            Title = "ModPack anlegen"
                        },
                        new
                        {
                            Id = 8L,
                            Category = "ModPacks",
                            ClaimCode = "modpack-modify",
                            Title = "ModPack bearbeiten"
                        },
                        new
                        {
                            Id = 9L,
                            Category = "ModPacks",
                            ClaimCode = "modpack-delete",
                            Title = "ModPack löschen"
                        });
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Authority.User", b =>
                {
                    b.Property<Guid>("Id")
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

                    b.Property<string>("Nickname")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<decimal>("SteamId64")
                        .HasColumnType("numeric(20,0)");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Core.ModPack", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<Guid>("OwnerFk")
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("TimeStampCreated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("TimeStampUpdated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Xml")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("OwnerFk");

                    b.ToTable("ModPacks");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Core.Terrain", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<byte[]>("Image")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<string>("ImageMimeType")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Terrains");
                });

            modelBuilder.Entity("X39.UnitedTacticalForces.Api.Data.Eventing.Event", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CreatedByFk")
                        .HasColumnType("uuid");

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

                    b.Property<long>("ModPackFk")
                        .HasColumnType("bigint");

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

                    b.HasKey("Id");

                    b.HasIndex("CreatedByFk");

                    b.HasIndex("HostedByFk");

                    b.HasIndex("ModPackFk");

                    b.HasIndex("TerrainFk");

                    b.ToTable("Events");
                });

            modelBuilder.Entity("RoleUser", b =>
                {
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.Role", null)
                        .WithMany()
                        .HasForeignKey("RolesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", null)
                        .WithMany()
                        .HasForeignKey("UsersId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
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
                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Authority.User", "CreatedBy")
                        .WithMany()
                        .HasForeignKey("CreatedByFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

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

                    b.HasOne("X39.UnitedTacticalForces.Api.Data.Core.Terrain", "Terrain")
                        .WithMany()
                        .HasForeignKey("TerrainFk")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CreatedBy");

                    b.Navigation("HostedBy");

                    b.Navigation("ModPack");

                    b.Navigation("Terrain");
                });
#pragma warning restore 612, 618
        }
    }
}
