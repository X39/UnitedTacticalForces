using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableGameServerHosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostOwners",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    UserFk = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsFrozen = table.Column<bool>(type: "boolean", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Allowance = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostOwners", x => x.PrimaryKey);
                    table.CheckConstraint("CK_CostOwners_KindUser", "(\"Kind\" = 0 AND \"UserFk\" IS NOT NULL) OR (\"Kind\" <> 0 AND \"UserFk\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_CostOwners_Users_UserFk",
                        column: x => x.UserFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameServers",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    GameIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RuntimeProviderIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentCostOwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    IsLaunchDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    LaunchDisabledReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    GracefulStopAttempts = table.Column<int>(type: "integer", nullable: false),
                    GracefulStopTimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampArchived = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServers", x => x.PrimaryKey);
                    table.CheckConstraint("CK_GameServers_GracefulStopPolicy", "\"GracefulStopAttempts\" > 0 AND \"GracefulStopTimeoutSeconds\" > 0");
                    table.ForeignKey(
                        name: "FK_GameServers_CostOwners_CurrentCostOwnerFk",
                        column: x => x.CurrentCostOwnerFk,
                        principalTable: "CostOwners",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameServers_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameServerCostOwnerAssignments",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    CostOwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    UnassignedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangeReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TimeStampAssigned = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUnassigned = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerCostOwnerAssignments", x => x.PrimaryKey);
                    table.CheckConstraint("CK_GameServerCostOwnerAssignments_Interval", "\"TimeStampUnassigned\" IS NULL OR \"TimeStampUnassigned\" >= \"TimeStampAssigned\"");
                    table.ForeignKey(
                        name: "FK_GameServerCostOwnerAssignments_CostOwners_CostOwnerFk",
                        column: x => x.CostOwnerFk,
                        principalTable: "CostOwners",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameServerCostOwnerAssignments_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameServerCostOwnerAssignments_Users_AssignedByFk",
                        column: x => x.AssignedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameServerCostOwnerAssignments_Users_UnassignedByFk",
                        column: x => x.UnassignedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameServerDeployments",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    CostOwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsProvisioningBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    PossibleCost = table.Column<bool>(type: "boolean", nullable: false),
                    FaultCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FaultMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampStarted = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampFaulted = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampResolved = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServerDeployments", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_GameServerDeployments_CostOwners_CostOwnerFk",
                        column: x => x.CostOwnerFk,
                        principalTable: "CostOwners",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameServerDeployments_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreservationSelections",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    IsGameSeeded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreservationSelections", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_PreservationSelections_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreservationSelections_Users_CreatedByFk",
                        column: x => x.CreatedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentResources",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentFk = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProvisioningIntentIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProviderResourceIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Kind = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    PossibleCost = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampObserved = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampReleased = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentResources", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_DeploymentResources_GameServerDeployments_DeploymentFk",
                        column: x => x.DeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LifecycleJobs",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentFk = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ActionIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LeaseExpires = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampNextAttempt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampStarted = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampCompleted = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifecycleJobs", x => x.PrimaryKey);
                    table.CheckConstraint("CK_LifecycleJobs_Attempts", "\"Attempt\" >= 0 AND \"MaxAttempts\" > 0 AND \"Attempt\" <= \"MaxAttempts\"");
                    table.CheckConstraint("CK_LifecycleJobs_Progress", "\"Progress\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_LifecycleJobs_GameServerDeployments_DeploymentFk",
                        column: x => x.DeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LifecycleJobs_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LifecycleJobs_Users_RequestedByFk",
                        column: x => x.RequestedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LifecycleWorkers",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Identity = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastHeartbeatAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LeaseExpires = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    ProcessId = table.Column<int>(type: "integer", nullable: true),
                    ProcessStartedAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    RuntimeRoot = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampRegistered = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampStopped = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifecycleWorkers", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_LifecycleWorkers_GameServerDeployments_DeploymentFk",
                        column: x => x.DeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LifecycleJobAttempts",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    LifecycleJobFk = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkerFk = table.Column<Guid>(type: "uuid", nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TimeStampStarted = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampCompleted = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifecycleJobAttempts", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_LifecycleJobAttempts_LifecycleJobs_LifecycleJobFk",
                        column: x => x.LifecycleJobFk,
                        principalTable: "LifecycleJobs",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LifecycleJobAttempts_LifecycleWorkers_WorkerFk",
                        column: x => x.WorkerFk,
                        principalTable: "LifecycleWorkers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkerCommands",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    LifecycleJobFk = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentFk = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ActionIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LeaseExpires = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampReceived = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampStarted = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampCompleted = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerCommands", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_WorkerCommands_GameServerDeployments_DeploymentFk",
                        column: x => x.DeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerCommands_LifecycleJobs_LifecycleJobFk",
                        column: x => x.LifecycleJobFk,
                        principalTable: "LifecycleJobs",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerCommands_LifecycleWorkers_WorkerFk",
                        column: x => x.WorkerFk,
                        principalTable: "LifecycleWorkers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HostingAuditRecords",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ActorFk = table.Column<Guid>(type: "uuid", nullable: true),
                    GameServerFk = table.Column<Guid>(type: "uuid", nullable: true),
                    DeploymentFk = table.Column<Guid>(type: "uuid", nullable: true),
                    LifecycleJobFk = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkerFk = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkerCommandFk = table.Column<Guid>(type: "uuid", nullable: true),
                    CostOwnerFk = table.Column<Guid>(type: "uuid", nullable: true),
                    CostImpact = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Details = table.Column<string>(type: "jsonb", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostingAuditRecords", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_HostingAuditRecords_CostOwners_CostOwnerFk",
                        column: x => x.CostOwnerFk,
                        principalTable: "CostOwners",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingAuditRecords_GameServerDeployments_DeploymentFk",
                        column: x => x.DeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingAuditRecords_GameServers_GameServerFk",
                        column: x => x.GameServerFk,
                        principalTable: "GameServers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingAuditRecords_LifecycleJobs_LifecycleJobFk",
                        column: x => x.LifecycleJobFk,
                        principalTable: "LifecycleJobs",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingAuditRecords_LifecycleWorkers_WorkerFk",
                        column: x => x.WorkerFk,
                        principalTable: "LifecycleWorkers",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingAuditRecords_Users_ActorFk",
                        column: x => x.ActorFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingAuditRecords_WorkerCommands_WorkerCommandFk",
                        column: x => x.WorkerCommandFk,
                        principalTable: "WorkerCommands",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostOwners_Kind_Verein",
                table: "CostOwners",
                column: "Kind",
                unique: true,
                filter: "\"Kind\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CostOwners_UserFk_Unique",
                table: "CostOwners",
                column: "UserFk",
                unique: true,
                filter: "\"UserFk\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentResources_DeploymentFk_State",
                table: "DeploymentResources",
                columns: new[] { "DeploymentFk", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentResources_ProviderIdentifier_ProvisioningIntentId~",
                table: "DeploymentResources",
                columns: new[] { "ProviderIdentifier", "ProvisioningIntentIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentResources_ProviderIdentity_Unique",
                table: "DeploymentResources",
                columns: new[] { "ProviderIdentifier", "Kind", "ProviderResourceIdentifier" },
                unique: true,
                filter: "\"ProviderResourceIdentifier\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerCostOwnerAssignments_AssignedByFk",
                table: "GameServerCostOwnerAssignments",
                column: "AssignedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerCostOwnerAssignments_CostOwnerFk",
                table: "GameServerCostOwnerAssignments",
                column: "CostOwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerCostOwnerAssignments_GameServerFk_Active",
                table: "GameServerCostOwnerAssignments",
                column: "GameServerFk",
                unique: true,
                filter: "\"TimeStampUnassigned\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerCostOwnerAssignments_GameServerFk_TimeStampAssign~",
                table: "GameServerCostOwnerAssignments",
                columns: new[] { "GameServerFk", "TimeStampAssigned" });

            migrationBuilder.CreateIndex(
                name: "IX_GameServerCostOwnerAssignments_UnassignedByFk",
                table: "GameServerCostOwnerAssignments",
                column: "UnassignedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerDeployments_CostOwnerFk",
                table: "GameServerDeployments",
                column: "CostOwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerDeployments_GameServerFk_Active",
                table: "GameServerDeployments",
                column: "GameServerFk",
                unique: true,
                filter: "\"TimeStampResolved\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GameServerDeployments_GameServerFk_TimeStampCreated",
                table: "GameServerDeployments",
                columns: new[] { "GameServerFk", "TimeStampCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_GameServerDeployments_Status_TimeStampUpdated",
                table: "GameServerDeployments",
                columns: new[] { "Status", "TimeStampUpdated" });

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_CreatedByFk",
                table: "GameServers",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_CurrentCostOwnerFk",
                table: "GameServers",
                column: "CurrentCostOwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_GameIdentifier",
                table: "GameServers",
                column: "GameIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_IsArchived_IsLaunchDisabled",
                table: "GameServers",
                columns: new[] { "IsArchived", "IsLaunchDisabled" });

            migrationBuilder.CreateIndex(
                name: "IX_HostingAuditRecords_ActorFk",
                table: "HostingAuditRecords",
                column: "ActorFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingAuditRecords_CostOwnerFk",
                table: "HostingAuditRecords",
                column: "CostOwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingAuditRecords_DeploymentFk",
                table: "HostingAuditRecords",
                column: "DeploymentFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingAuditRecords_GameServerFk_TimeStampCreated",
                table: "HostingAuditRecords",
                columns: new[] { "GameServerFk", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_HostingAuditRecords_Kind_TimeStampCreated",
                table: "HostingAuditRecords",
                columns: new[] { "Kind", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_HostingAuditRecords_LifecycleJobFk",
                table: "HostingAuditRecords",
                column: "LifecycleJobFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingAuditRecords_WorkerCommandFk",
                table: "HostingAuditRecords",
                column: "WorkerCommandFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingAuditRecords_WorkerFk",
                table: "HostingAuditRecords",
                column: "WorkerFk");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleJobAttempts_LifecycleJobFk_AttemptNumber",
                table: "LifecycleJobAttempts",
                columns: new[] { "LifecycleJobFk", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleJobAttempts_WorkerFk",
                table: "LifecycleJobAttempts",
                column: "WorkerFk");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleJobs_DeploymentFk_Active",
                table: "LifecycleJobs",
                column: "DeploymentFk",
                unique: true,
                filter: "\"Status\" IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleJobs_GameServerFk_Sequence",
                table: "LifecycleJobs",
                columns: new[] { "GameServerFk", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleJobs_RequestedByFk",
                table: "LifecycleJobs",
                column: "RequestedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleJobs_Status_Priority_TimeStampCreated",
                table: "LifecycleJobs",
                columns: new[] { "Status", "Priority", "TimeStampCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleWorkers_DeploymentFk_Active",
                table: "LifecycleWorkers",
                column: "DeploymentFk",
                unique: true,
                filter: "\"TimeStampStopped\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleWorkers_DeploymentFk_Status",
                table: "LifecycleWorkers",
                columns: new[] { "DeploymentFk", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PreservationSelections_CreatedByFk",
                table: "PreservationSelections",
                column: "CreatedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_PreservationSelections_GameServerFk_Path_Scope",
                table: "PreservationSelections",
                columns: new[] { "GameServerFk", "Path", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCommands_DeploymentFk_Active",
                table: "WorkerCommands",
                column: "DeploymentFk",
                unique: true,
                filter: "\"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCommands_DeploymentFk_Sequence",
                table: "WorkerCommands",
                columns: new[] { "DeploymentFk", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCommands_LifecycleJobFk",
                table: "WorkerCommands",
                column: "LifecycleJobFk");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCommands_Status_Priority_TimeStampCreated",
                table: "WorkerCommands",
                columns: new[] { "Status", "Priority", "TimeStampCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCommands_WorkerFk",
                table: "WorkerCommands",
                column: "WorkerFk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeploymentResources");

            migrationBuilder.DropTable(
                name: "GameServerCostOwnerAssignments");

            migrationBuilder.DropTable(
                name: "HostingAuditRecords");

            migrationBuilder.DropTable(
                name: "LifecycleJobAttempts");

            migrationBuilder.DropTable(
                name: "PreservationSelections");

            migrationBuilder.DropTable(
                name: "WorkerCommands");

            migrationBuilder.DropTable(
                name: "LifecycleJobs");

            migrationBuilder.DropTable(
                name: "LifecycleWorkers");

            migrationBuilder.DropTable(
                name: "GameServerDeployments");

            migrationBuilder.DropTable(
                name: "GameServers");

            migrationBuilder.DropTable(
                name: "CostOwners");
        }
    }
}
