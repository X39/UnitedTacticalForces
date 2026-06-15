using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Identifier = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IsPrefix = table.Column<bool>(type: "boolean", nullable: false),
                    Value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ValueType = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    EMail = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DateOfBirth = table.Column<LocalDate>(type: "date", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    City = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Country = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsBanned = table.Column<bool>(type: "boolean", nullable: false),
                    Avatar = table.Column<byte[]>(type: "bytea", nullable: false),
                    AvatarMimeType = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Steam_Id64 = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Discord_Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Discord_Username = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "WikiPages",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiPages", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "ClaimRole",
                columns: table => new
                {
                    ClaimsPrimaryKey = table.Column<long>(type: "bigint", nullable: false),
                    RolesPrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimRole", x => new { x.ClaimsPrimaryKey, x.RolesPrimaryKey });
                    table.ForeignKey(
                        name: "FK_ClaimRole_Claims_ClaimsPrimaryKey",
                        column: x => x.ClaimsPrimaryKey,
                        principalTable: "Claims",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaimRole_Roles_RolesPrimaryKey",
                        column: x => x.RolesPrimaryKey,
                        principalTable: "Roles",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimUser",
                columns: table => new
                {
                    ClaimsPrimaryKey = table.Column<long>(type: "bigint", nullable: false),
                    UsersPrimaryKey = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimUser", x => new { x.ClaimsPrimaryKey, x.UsersPrimaryKey });
                    table.ForeignKey(
                        name: "FK_ClaimUser_Claims_ClaimsPrimaryKey",
                        column: x => x.ClaimsPrimaryKey,
                        principalTable: "Claims",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClaimUser_Users_UsersPrimaryKey",
                        column: x => x.UsersPrimaryKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KassenbuchEntries",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeStampBooked = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Category = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KassenbuchEntries", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_KassenbuchEntries_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KassenbuchEntries_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StorageFileName = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_MediaAssets_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModPackDefinitions",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    OwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsComposition = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPackDefinitions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ModPackDefinitions_Users_OwnerFk",
                        column: x => x.OwnerFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicPages",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicPages", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_PublicPages_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicPages_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleUser",
                columns: table => new
                {
                    RolesPrimaryKey = table.Column<long>(type: "bigint", nullable: false),
                    UsersPrimaryKey = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleUser", x => new { x.RolesPrimaryKey, x.UsersPrimaryKey });
                    table.ForeignKey(
                        name: "FK_RoleUser_Roles_RolesPrimaryKey",
                        column: x => x.RolesPrimaryKey,
                        principalTable: "Roles",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleUser_Users_UsersPrimaryKey",
                        column: x => x.UsersPrimaryKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VereinDocuments",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VereinDocuments", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_VereinDocuments_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VereinDocuments_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VereinMembershipRequests",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserFk = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LastName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DateOfBirth = table.Column<LocalDate>(type: "date", nullable: false),
                    Email = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    City = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Country = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Motivation = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    AcknowledgesMembershipSeparation = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgesDataProcessing = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgesSatzung = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecisionReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ReviewedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampResolved = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VereinMembershipRequests", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_VereinMembershipRequests_Users_ReviewedByFk",
                        column: x => x.ReviewedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VereinMembershipRequests_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VereinMemberships",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserFk = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TimeStampStarted = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampEnded = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VereinMemberships", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_VereinMemberships_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VereinMemberships_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VereinMemberships_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VereinSettings",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Identifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MembershipContributionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MembershipContributionCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MembershipContributionCadence = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    MembershipContributionPublicTextDe = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    MembershipContributionPublicTextEn = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VereinSettings", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_VereinSettings_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WikiPageAudits",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PageForeignKey = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    UserForeignKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiPageAudits", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_WikiPageAudits_Users_UserForeignKey",
                        column: x => x.UserForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WikiPageAudits_WikiPages_PageForeignKey",
                        column: x => x.PageForeignKey,
                        principalTable: "WikiPages",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WikiPageRevisions",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PageForeignKey = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    Markdown = table.Column<string>(type: "text", nullable: false),
                    AuthorForeignKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiPageRevisions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_WikiPageRevisions_Users_AuthorForeignKey",
                        column: x => x.AuthorForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WikiPageRevisions_WikiPages_PageForeignKey",
                        column: x => x.PageForeignKey,
                        principalTable: "WikiPages",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KassenbuchEntryAudits",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KassenbuchEntryFk = table.Column<long>(type: "bigint", nullable: false),
                    UserForeignKey = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KassenbuchEntryAudits", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_KassenbuchEntryAudits_KassenbuchEntries_KassenbuchEntryFk",
                        column: x => x.KassenbuchEntryFk,
                        principalTable: "KassenbuchEntries",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KassenbuchEntryAudits_Users_UserForeignKey",
                        column: x => x.UserForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FieldReports",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Excerpt = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Markdown = table.Column<string>(type: "text", nullable: false),
                    HeroImageAssetFk = table.Column<Guid>(type: "uuid", nullable: true),
                    HeroImageAlt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampPublished = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldReports", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_FieldReports_MediaAssets_HeroImageAssetFk",
                        column: x => x.HeroImageAssetFk,
                        principalTable: "MediaAssets",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldReports_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldReports_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaAssetTags",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaAssetFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssetTags", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_MediaAssetTags_MediaAssets_MediaAssetFk",
                        column: x => x.MediaAssetFk,
                        principalTable: "MediaAssets",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaAssetTags_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteImageSettings",
                columns: table => new
                {
                    PrimaryKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Group = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ImageAssetFk = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteImageSettings", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_SiteImageSettings_MediaAssets_ImageAssetFk",
                        column: x => x.ImageAssetFk,
                        principalTable: "MediaAssets",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteImageSettings_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Terrains",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ImageAssetFk = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terrains", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_Terrains_MediaAssets_ImageAssetFk",
                        column: x => x.ImageAssetFk,
                        principalTable: "MediaAssets",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModPackRevisions",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    Html = table.Column<string>(type: "text", nullable: false),
                    UpdatedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DefinitionFk = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPackRevisions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_ModPackRevisions_ModPackDefinitions_DefinitionFk",
                        column: x => x.DefinitionFk,
                        principalTable: "ModPackDefinitions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModPackRevisions_Users_UpdatedByFk",
                        column: x => x.UpdatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicPageRevisions",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PageFk = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    HeroKicker = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    HeroTitle = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    HeroDescription = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    HeroImageAssetFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Markdown = table.Column<string>(type: "text", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampPublished = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    AuthorFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicPageRevisions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_PublicPageRevisions_MediaAssets_HeroImageAssetFk",
                        column: x => x.HeroImageAssetFk,
                        principalTable: "MediaAssets",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicPageRevisions_PublicPages_PageFk",
                        column: x => x.PageFk,
                        principalTable: "PublicPages",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicPageRevisions_Users_AuthorFk",
                        column: x => x.AuthorFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VereinDocumentRevisions",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentFk = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Markdown = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampPublished = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    AuthorFk = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VereinDocumentRevisions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_VereinDocumentRevisions_Users_AuthorFk",
                        column: x => x.AuthorFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VereinDocumentRevisions_VereinDocuments_DocumentFk",
                        column: x => x.DocumentFk,
                        principalTable: "VereinDocuments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VereinMembershipRequestAudits",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestFk = table.Column<long>(type: "bigint", nullable: false),
                    UserForeignKey = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VereinMembershipRequestAudits", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_VereinMembershipRequestAudits_Users_UserForeignKey",
                        column: x => x.UserForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VereinMembershipRequestAudits_VereinMembershipRequests_Requ~",
                        column: x => x.RequestFk,
                        principalTable: "VereinMembershipRequests",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VereinMembershipAudits",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MembershipFk = table.Column<long>(type: "bigint", nullable: false),
                    UserForeignKey = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VereinMembershipAudits", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_VereinMembershipAudits_Users_UserForeignKey",
                        column: x => x.UserForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VereinMembershipAudits_VereinMemberships_MembershipFk",
                        column: x => x.MembershipFk,
                        principalTable: "VereinMemberships",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VereinSettingsAudits",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SettingsFk = table.Column<long>(type: "bigint", nullable: false),
                    UserForeignKey = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VereinSettingsAudits", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_VereinSettingsAudits_Users_UserForeignKey",
                        column: x => x.UserForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VereinSettingsAudits_VereinSettings_SettingsFk",
                        column: x => x.SettingsFk,
                        principalTable: "VereinSettings",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FieldReportAudits",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FieldReportFk = table.Column<Guid>(type: "uuid", nullable: false),
                    UserForeignKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldReportAudits", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_FieldReportAudits_FieldReports_FieldReportFk",
                        column: x => x.FieldReportFk,
                        principalTable: "FieldReports",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldReportAudits_Users_UserForeignKey",
                        column: x => x.UserForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TerrainFk = table.Column<long>(type: "bigint", nullable: false),
                    ModPackRevisionFk = table.Column<long>(type: "bigint", nullable: false),
                    ImageAssetFk = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledForOriginal = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    ScheduledFor = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedCount = table.Column<int>(type: "integer", nullable: false),
                    MaybeCount = table.Column<int>(type: "integer", nullable: false),
                    MinimumAccepted = table.Column<int>(type: "integer", nullable: false),
                    OwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    HostedByFk = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_Events_MediaAssets_ImageAssetFk",
                        column: x => x.ImageAssetFk,
                        principalTable: "MediaAssets",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_ModPackRevisions_ModPackRevisionFk",
                        column: x => x.ModPackRevisionFk,
                        principalTable: "ModPackRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Terrains_TerrainFk",
                        column: x => x.TerrainFk,
                        principalTable: "Terrains",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Users_HostedByFk",
                        column: x => x.HostedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Users_OwnerFk",
                        column: x => x.OwnerFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModPackDefinitionModPackRevision",
                columns: table => new
                {
                    ModPackDefinitionsPrimaryKey = table.Column<long>(type: "bigint", nullable: false),
                    ModPackRevisionsPrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModPackDefinitionModPackRevision", x => new { x.ModPackDefinitionsPrimaryKey, x.ModPackRevisionsPrimaryKey });
                    table.ForeignKey(
                        name: "FK_ModPackDefinitionModPackRevision_ModPackDefinitions_ModPack~",
                        column: x => x.ModPackDefinitionsPrimaryKey,
                        principalTable: "ModPackDefinitions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModPackDefinitionModPackRevision_ModPackRevisions_ModPackRe~",
                        column: x => x.ModPackRevisionsPrimaryKey,
                        principalTable: "ModPackRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserModPackMetas",
                columns: table => new
                {
                    UserFk = table.Column<Guid>(type: "uuid", nullable: false),
                    ModPackDefinitionFk = table.Column<long>(type: "bigint", nullable: false),
                    ModPackRevisionFk = table.Column<long>(type: "bigint", nullable: false),
                    TimeStampDownloaded = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModPackMetas", x => new { x.UserFk, x.ModPackDefinitionFk, x.ModPackRevisionFk });
                    table.ForeignKey(
                        name: "FK_UserModPackMetas_ModPackDefinitions_ModPackDefinitionFk",
                        column: x => x.ModPackDefinitionFk,
                        principalTable: "ModPackDefinitions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserModPackMetas_ModPackRevisions_ModPackRevisionFk",
                        column: x => x.ModPackRevisionFk,
                        principalTable: "ModPackRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserModPackMetas_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicPageAudits",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PageFk = table.Column<long>(type: "bigint", nullable: false),
                    RevisionFk = table.Column<long>(type: "bigint", nullable: true),
                    UserForeignKey = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicPageAudits", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_PublicPageAudits_PublicPageRevisions_RevisionFk",
                        column: x => x.RevisionFk,
                        principalTable: "PublicPageRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicPageAudits_PublicPages_PageFk",
                        column: x => x.PageFk,
                        principalTable: "PublicPages",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicPageAudits_Users_UserForeignKey",
                        column: x => x.UserForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VereinDocumentAudits",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentFk = table.Column<long>(type: "bigint", nullable: false),
                    RevisionFk = table.Column<long>(type: "bigint", nullable: true),
                    UserForeignKey = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VereinDocumentAudits", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_VereinDocumentAudits_Users_UserForeignKey",
                        column: x => x.UserForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VereinDocumentAudits_VereinDocumentRevisions_RevisionFk",
                        column: x => x.RevisionFk,
                        principalTable: "VereinDocumentRevisions",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VereinDocumentAudits_VereinDocuments_DocumentFk",
                        column: x => x.DocumentFk,
                        principalTable: "VereinDocuments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventSlots",
                columns: table => new
                {
                    SlotNumber = table.Column<short>(type: "smallint", nullable: false),
                    EventFk = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToFk = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSelfAssignable = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    Group = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSlots", x => new { x.EventFk, x.SlotNumber });
                    table.ForeignKey(
                        name: "FK_EventSlots_Events_EventFk",
                        column: x => x.EventFk,
                        principalTable: "Events",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventSlots_Users_AssignedToFk",
                        column: x => x.AssignedToFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey");
                });

            migrationBuilder.CreateTable(
                name: "UserEventMetas",
                columns: table => new
                {
                    UserFk = table.Column<Guid>(type: "uuid", nullable: false),
                    EventFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Acceptance = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEventMetas", x => new { x.UserFk, x.EventFk });
                    table.ForeignKey(
                        name: "FK_UserEventMetas_Events_EventFk",
                        column: x => x.EventFk,
                        principalTable: "Events",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserEventMetas_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRole_RolesPrimaryKey",
                table: "ClaimRole",
                column: "RolesPrimaryKey");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Identifier_NullValue_NullValueType",
                table: "Claims",
                column: "Identifier",
                unique: true,
                filter: "\"Value\" IS NULL AND \"ValueType\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Identifier_Value_NullValueType",
                table: "Claims",
                columns: new[] { "Identifier", "Value" },
                unique: true,
                filter: "\"Value\" IS NOT NULL AND \"ValueType\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Identifier_Value_ValueType",
                table: "Claims",
                columns: new[] { "Identifier", "Value", "ValueType" },
                unique: true,
                filter: "\"Value\" IS NOT NULL AND \"ValueType\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Identifier_ValueType_NullValue",
                table: "Claims",
                columns: new[] { "Identifier", "ValueType" },
                unique: true,
                filter: "\"Value\" IS NULL AND \"ValueType\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimUser_UsersPrimaryKey",
                table: "ClaimUser",
                column: "UsersPrimaryKey");

            migrationBuilder.CreateIndex(
                name: "IX_Events_HostedByFk",
                table: "Events",
                column: "HostedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ImageAssetFk",
                table: "Events",
                column: "ImageAssetFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ModPackRevisionFk",
                table: "Events",
                column: "ModPackRevisionFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_OwnerFk",
                table: "Events",
                column: "OwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ScheduledFor",
                table: "Events",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_Events_TerrainFk",
                table: "Events",
                column: "TerrainFk");

            migrationBuilder.CreateIndex(
                name: "IX_EventSlots_AssignedToFk",
                table: "EventSlots",
                column: "AssignedToFk");

            migrationBuilder.CreateIndex(
                name: "IX_FieldReportAudits_FieldReportFk_TimeStampCreated",
                table: "FieldReportAudits",
                columns: new[] { "FieldReportFk", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_FieldReportAudits_UserForeignKey",
                table: "FieldReportAudits",
                column: "UserForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_FieldReports_CreatedByFk",
                table: "FieldReports",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_FieldReports_HeroImageAssetFk",
                table: "FieldReports",
                column: "HeroImageAssetFk");

            migrationBuilder.CreateIndex(
                name: "IX_FieldReports_IsFeatured_SortOrder_TimeStampPublished",
                table: "FieldReports",
                columns: new[] { "IsFeatured", "SortOrder", "TimeStampPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldReports_IsPublished_IsDeleted_TimeStampPublished",
                table: "FieldReports",
                columns: new[] { "IsPublished", "IsDeleted", "TimeStampPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldReports_Slug",
                table: "FieldReports",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldReports_UpdatedByFk",
                table: "FieldReports",
                column: "UpdatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_KassenbuchEntries_Category",
                table: "KassenbuchEntries",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_KassenbuchEntries_CreatedByFk",
                table: "KassenbuchEntries",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_KassenbuchEntries_IsDeleted_TimeStampBooked",
                table: "KassenbuchEntries",
                columns: new[] { "IsDeleted", "TimeStampBooked" });

            migrationBuilder.CreateIndex(
                name: "IX_KassenbuchEntries_UpdatedByFk",
                table: "KassenbuchEntries",
                column: "UpdatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_KassenbuchEntryAudits_KassenbuchEntryFk_TimeStampCreated",
                table: "KassenbuchEntryAudits",
                columns: new[] { "KassenbuchEntryFk", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_KassenbuchEntryAudits_UserForeignKey",
                table: "KassenbuchEntryAudits",
                column: "UserForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_CreatedByFk",
                table: "MediaAssets",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_Sha256Hash",
                table: "MediaAssets",
                column: "Sha256Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_TimeStampCreated",
                table: "MediaAssets",
                column: "TimeStampCreated");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssetTags_CreatedByFk",
                table: "MediaAssetTags",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssetTags_MediaAssetFk_Value",
                table: "MediaAssetTags",
                columns: new[] { "MediaAssetFk", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssetTags_Value",
                table: "MediaAssetTags",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackDefinitionModPackRevision_ModPackRevisionsPrimaryKey",
                table: "ModPackDefinitionModPackRevision",
                column: "ModPackRevisionsPrimaryKey");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackDefinitions_OwnerFk",
                table: "ModPackDefinitions",
                column: "OwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackRevisions_DefinitionFk",
                table: "ModPackRevisions",
                column: "DefinitionFk");

            migrationBuilder.CreateIndex(
                name: "IX_ModPackRevisions_UpdatedByFk",
                table: "ModPackRevisions",
                column: "UpdatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_PublicPageAudits_PageFk_TimeStampCreated",
                table: "PublicPageAudits",
                columns: new[] { "PageFk", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_PublicPageAudits_RevisionFk",
                table: "PublicPageAudits",
                column: "RevisionFk");

            migrationBuilder.CreateIndex(
                name: "IX_PublicPageAudits_UserForeignKey",
                table: "PublicPageAudits",
                column: "UserForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_PublicPageRevisions_AuthorFk",
                table: "PublicPageRevisions",
                column: "AuthorFk");

            migrationBuilder.CreateIndex(
                name: "IX_PublicPageRevisions_HeroImageAssetFk",
                table: "PublicPageRevisions",
                column: "HeroImageAssetFk");

            migrationBuilder.CreateIndex(
                name: "IX_PublicPageRevisions_PageFk_IsPublished_TimeStampPublished",
                table: "PublicPageRevisions",
                columns: new[] { "PageFk", "IsPublished", "TimeStampPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicPageRevisions_PageFk_Version",
                table: "PublicPageRevisions",
                columns: new[] { "PageFk", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicPages_CreatedByFk",
                table: "PublicPages",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_PublicPages_IsDeleted",
                table: "PublicPages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PublicPages_Slug",
                table: "PublicPages",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicPages_UpdatedByFk",
                table: "PublicPages",
                column: "UpdatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_RoleUser_UsersPrimaryKey",
                table: "RoleUser",
                column: "UsersPrimaryKey");

            migrationBuilder.CreateIndex(
                name: "IX_SiteImageSettings_ImageAssetFk",
                table: "SiteImageSettings",
                column: "ImageAssetFk");

            migrationBuilder.CreateIndex(
                name: "IX_SiteImageSettings_SortOrder",
                table: "SiteImageSettings",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_SiteImageSettings_UpdatedByFk",
                table: "SiteImageSettings",
                column: "UpdatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_Terrains_ImageAssetFk",
                table: "Terrains",
                column: "ImageAssetFk");

            migrationBuilder.CreateIndex(
                name: "IX_UserEventMetas_EventFk",
                table: "UserEventMetas",
                column: "EventFk");

            migrationBuilder.CreateIndex(
                name: "IX_UserModPackMetas_ModPackDefinitionFk",
                table: "UserModPackMetas",
                column: "ModPackDefinitionFk");

            migrationBuilder.CreateIndex(
                name: "IX_UserModPackMetas_ModPackRevisionFk",
                table: "UserModPackMetas",
                column: "ModPackRevisionFk");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Nickname",
                table: "Users",
                column: "Nickname");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Steam_Id64_Active",
                table: "Users",
                column: "Steam_Id64",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Steam_Id64\" <> 0");

            migrationBuilder.CreateIndex(
                name: "IX_VereinDocumentAudits_DocumentFk_TimeStampCreated",
                table: "VereinDocumentAudits",
                columns: new[] { "DocumentFk", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_VereinDocumentAudits_RevisionFk",
                table: "VereinDocumentAudits",
                column: "RevisionFk");

            migrationBuilder.CreateIndex(
                name: "IX_VereinDocumentAudits_UserForeignKey",
                table: "VereinDocumentAudits",
                column: "UserForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_VereinDocumentRevisions_AuthorFk",
                table: "VereinDocumentRevisions",
                column: "AuthorFk");

            migrationBuilder.CreateIndex(
                name: "IX_VereinDocumentRevisions_DocumentFk_IsPublished_TimeStampPub~",
                table: "VereinDocumentRevisions",
                columns: new[] { "DocumentFk", "IsPublished", "TimeStampPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_VereinDocumentRevisions_DocumentFk_Version",
                table: "VereinDocumentRevisions",
                columns: new[] { "DocumentFk", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VereinDocuments_CreatedByFk",
                table: "VereinDocuments",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_VereinDocuments_Kind_IsDeleted",
                table: "VereinDocuments",
                columns: new[] { "Kind", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_VereinDocuments_UpdatedByFk",
                table: "VereinDocuments",
                column: "UpdatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_VereinMembershipAudits_MembershipFk_TimeStampCreated",
                table: "VereinMembershipAudits",
                columns: new[] { "MembershipFk", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_VereinMembershipAudits_UserForeignKey",
                table: "VereinMembershipAudits",
                column: "UserForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_VereinMembershipRequestAudits_RequestFk_TimeStampCreated",
                table: "VereinMembershipRequestAudits",
                columns: new[] { "RequestFk", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_VereinMembershipRequestAudits_UserForeignKey",
                table: "VereinMembershipRequestAudits",
                column: "UserForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_VereinMembershipRequests_ReviewedByFk",
                table: "VereinMembershipRequests",
                column: "ReviewedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_VereinMembershipRequests_Status_TimeStampCreated",
                table: "VereinMembershipRequests",
                columns: new[] { "Status", "TimeStampCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_VereinMembershipRequests_UserFk_Open",
                table: "VereinMembershipRequests",
                column: "UserFk",
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_VereinMembershipRequests_UserFk_Status",
                table: "VereinMembershipRequests",
                columns: new[] { "UserFk", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VereinMemberships_CreatedByFk",
                table: "VereinMemberships",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_VereinMemberships_MemberNumber",
                table: "VereinMemberships",
                column: "MemberNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VereinMemberships_Status",
                table: "VereinMemberships",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VereinMemberships_UpdatedByFk",
                table: "VereinMemberships",
                column: "UpdatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_VereinMemberships_UserFk",
                table: "VereinMemberships",
                column: "UserFk",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VereinSettings_Identifier",
                table: "VereinSettings",
                column: "Identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VereinSettings_UpdatedByFk",
                table: "VereinSettings",
                column: "UpdatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_VereinSettingsAudits_SettingsFk_TimeStampCreated",
                table: "VereinSettingsAudits",
                columns: new[] { "SettingsFk", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_VereinSettingsAudits_UserForeignKey",
                table: "VereinSettingsAudits",
                column: "UserForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPageAudits_PageForeignKey_TimeStampCreated",
                table: "WikiPageAudits",
                columns: new[] { "PageForeignKey", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_WikiPageAudits_UserForeignKey",
                table: "WikiPageAudits",
                column: "UserForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPageRevisions_AuthorForeignKey",
                table: "WikiPageRevisions",
                column: "AuthorForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPageRevisions_PageForeignKey_TimeStampCreated",
                table: "WikiPageRevisions",
                columns: new[] { "PageForeignKey", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_Title",
                table: "WikiPages",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimRole");

            migrationBuilder.DropTable(
                name: "ClaimUser");

            migrationBuilder.DropTable(
                name: "EventSlots");

            migrationBuilder.DropTable(
                name: "FieldReportAudits");

            migrationBuilder.DropTable(
                name: "KassenbuchEntryAudits");

            migrationBuilder.DropTable(
                name: "MediaAssetTags");

            migrationBuilder.DropTable(
                name: "ModPackDefinitionModPackRevision");

            migrationBuilder.DropTable(
                name: "PublicPageAudits");

            migrationBuilder.DropTable(
                name: "RoleUser");

            migrationBuilder.DropTable(
                name: "SiteImageSettings");

            migrationBuilder.DropTable(
                name: "UserEventMetas");

            migrationBuilder.DropTable(
                name: "UserModPackMetas");

            migrationBuilder.DropTable(
                name: "VereinDocumentAudits");

            migrationBuilder.DropTable(
                name: "VereinMembershipAudits");

            migrationBuilder.DropTable(
                name: "VereinMembershipRequestAudits");

            migrationBuilder.DropTable(
                name: "VereinSettingsAudits");

            migrationBuilder.DropTable(
                name: "WikiPageAudits");

            migrationBuilder.DropTable(
                name: "WikiPageRevisions");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "FieldReports");

            migrationBuilder.DropTable(
                name: "KassenbuchEntries");

            migrationBuilder.DropTable(
                name: "PublicPageRevisions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "VereinDocumentRevisions");

            migrationBuilder.DropTable(
                name: "VereinMemberships");

            migrationBuilder.DropTable(
                name: "VereinMembershipRequests");

            migrationBuilder.DropTable(
                name: "VereinSettings");

            migrationBuilder.DropTable(
                name: "WikiPages");

            migrationBuilder.DropTable(
                name: "PublicPages");

            migrationBuilder.DropTable(
                name: "ModPackRevisions");

            migrationBuilder.DropTable(
                name: "Terrains");

            migrationBuilder.DropTable(
                name: "VereinDocuments");

            migrationBuilder.DropTable(
                name: "ModPackDefinitions");

            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
