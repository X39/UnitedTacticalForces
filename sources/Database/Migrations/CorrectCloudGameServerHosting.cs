using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace X39.UnitedTacticalForces.Database.Migrations
{
    /// <inheritdoc />
    public partial class CorrectCloudGameServerHosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $utf_cloud_upgrade$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "GameServerDeployments"
                        WHERE "TimeStampResolved" IS NULL
                    ) OR EXISTS (
                        SELECT 1
                        FROM "LifecycleWorkers"
                        WHERE "TimeStampStopped" IS NULL OR "ProcessId" IS NOT NULL
                    ) OR EXISTS (
                        SELECT 1
                        FROM "DeploymentResources"
                        WHERE "TimeStampReleased" IS NULL
                          AND ("ProviderResourceIdentifier" IS NOT NULL OR "PossibleCost")
                    ) OR EXISTS (
                        SELECT 1
                        FROM "LifecycleJobs"
                        WHERE "Status" IN (0, 1, 2, 3)
                    ) OR EXISTS (
                        SELECT 1
                        FROM "WorkerCommands"
                        WHERE "Status" IN (0, 1, 2)
                    ) THEN
                        RAISE EXCEPTION USING
                            MESSAGE = 'Cloud hosting migration refused: unresolved legacy hosting state exists.',
                            HINT = 'Run the previous release to resolve or terminate legacy hosting before retrying.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "CostOwners"
                        WHERE "Balance" <> 0 OR "Allowance" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION USING
                            MESSAGE = 'Cloud hosting migration refused: existing finance projections have no append-only opening ledger.',
                            HINT = 'Reconcile to zero/null or add reviewed opening-ledger entries before retrying.';
                    END IF;
                END
                $utf_cloud_upgrade$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_GameServers_AllocatedLocalPort_Unique",
                table: "GameServers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GameServers_AllocatedLocalPort",
                table: "GameServers");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentResources_ProviderIdentifier_ProvisioningIntentId~",
                table: "DeploymentResources");

            migrationBuilder.DropColumn(
                name: "RuntimeRoot",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "AllocatedLocalPort",
                table: "GameServers");

            migrationBuilder.AddColumn<string>(
                name: "DiagnosticOutput",
                table: "WorkerCommands",
                type: "character varying(16384)",
                maxLength: 16384,
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "BootstrapCredentialConsumedAt",
                table: "LifecycleWorkers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "BootstrapCredentialExpiresAt",
                table: "LifecycleWorkers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BootstrapCredentialHash",
                table: "LifecycleWorkers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "PendingWorkerCredentialExpiresAt",
                table: "LifecycleWorkers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingWorkerCredentialHash",
                table: "LifecycleWorkers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "PendingWorkerCredentialIssuedAt",
                table: "LifecycleWorkers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "WorkerCredentialActivatedAt",
                table: "LifecycleWorkers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "WorkerCredentialExpiresAt",
                table: "LifecycleWorkers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkerCredentialHash",
                table: "LifecycleWorkers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "WorkerCredentialIssuedAt",
                table: "LifecycleWorkers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "WorkerCredentialRevokedAt",
                table: "LifecycleWorkers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RequestedByFk",
                table: "LifecycleJobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Instant>(
                name: "CostAuthorizationExpiresAt",
                table: "GameServerDeployments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "CostReservationExpiresAt",
                table: "GameServerDeployments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "CostReservationStartsAt",
                table: "GameServerDeployments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedHourlyCostEur",
                table: "GameServerDeployments",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedProviderCostEur",
                table: "GameServerDeployments",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "TimeStampProvisioningRequested",
                table: "DeploymentResources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Instant>(
                name: "TimeStampProvisioningResolved",
                table: "DeploymentResources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DanglingProviderResourceIncidents",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderResourceIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsPlatformManaged = table.Column<bool>(type: "boolean", nullable: false),
                    PossibleCost = table.Column<bool>(type: "boolean", nullable: false),
                    LastProviderState = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastFailure = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ObservationCount = table.Column<int>(type: "integer", nullable: false),
                    CleanupAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampUpdated = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampLastObserved = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    TimeStampLastCleanupAttempt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    TimeStampResolved = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DanglingProviderResourceIncidents", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "HostingCostLedgerEntries",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CostOwnerFk = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentFk = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    EstimatedHourlyRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ProviderIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SliceStartsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    SliceEndsAt = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    RecordIdentifier = table.Column<Guid>(type: "uuid", nullable: true),
                    FinanceOperationFk = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversedEntryFk = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeStampEffective = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    RecordedByFk = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PreviousAllowance = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    NewAllowance = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostingCostLedgerEntries", x => x.PrimaryKey);
                    table.CheckConstraint("CK_HostingCostLedgerEntries_Currency", "length(\"Currency\") = 3");
                    table.CheckConstraint("CK_HostingCostLedgerEntries_Shape", "(\"Kind\" = 0 AND \"Amount\" < 0 AND \"DeploymentFk\" IS NOT NULL AND \"EstimatedHourlyRate\" > 0 AND \"ProviderIdentifier\" IS NOT NULL AND \"SliceStartsAt\" IS NOT NULL AND \"SliceEndsAt\" IS NOT NULL AND \"RecordedByFk\" IS NULL AND \"RecordIdentifier\" IS NULL AND \"PreviousAllowance\" IS NULL AND \"NewAllowance\" IS NULL AND \"FinanceOperationFk\" IS NULL AND \"ReversedEntryFk\" IS NULL) OR (\"Kind\" = 1 AND \"Amount\" > 0 AND \"DeploymentFk\" IS NULL AND \"EstimatedHourlyRate\" IS NULL AND \"ProviderIdentifier\" IS NULL AND \"SliceStartsAt\" IS NULL AND \"SliceEndsAt\" IS NULL AND \"RecordedByFk\" IS NOT NULL AND \"RecordIdentifier\" IS NOT NULL AND \"ExternalReference\" IS NOT NULL AND \"PreviousAllowance\" IS NULL AND \"NewAllowance\" IS NULL AND \"FinanceOperationFk\" IS NULL AND \"ReversedEntryFk\" IS NULL) OR (\"Kind\" = 2 AND \"Amount\" = 0 AND \"DeploymentFk\" IS NULL AND \"EstimatedHourlyRate\" IS NULL AND \"ProviderIdentifier\" IS NULL AND \"SliceStartsAt\" IS NULL AND \"SliceEndsAt\" IS NULL AND \"RecordedByFk\" IS NOT NULL AND \"RecordIdentifier\" IS NOT NULL AND \"Reason\" IS NOT NULL AND (\"PreviousAllowance\" IS NOT NULL OR \"NewAllowance\" IS NOT NULL) AND \"FinanceOperationFk\" IS NULL AND \"ReversedEntryFk\" IS NULL) OR (\"Kind\" = 3 AND \"Amount\" < 0 AND \"DeploymentFk\" IS NULL AND \"EstimatedHourlyRate\" IS NULL AND \"ProviderIdentifier\" IS NULL AND \"SliceStartsAt\" IS NULL AND \"SliceEndsAt\" IS NULL AND \"RecordedByFk\" IS NOT NULL AND \"RecordIdentifier\" IS NOT NULL AND \"ExternalReference\" IS NOT NULL AND \"Reason\" IS NOT NULL AND \"PreviousAllowance\" IS NULL AND \"NewAllowance\" IS NULL AND \"FinanceOperationFk\" IS NULL AND \"ReversedEntryFk\" IS NULL) OR (\"Kind\" IN (4, 5) AND \"DeploymentFk\" IS NOT NULL AND ((\"Kind\" = 4 AND \"Amount\" > 0) OR (\"Kind\" = 5 AND \"Amount\" < 0)) AND \"EstimatedHourlyRate\" IS NULL AND \"ProviderIdentifier\" IS NULL AND \"SliceStartsAt\" IS NULL AND \"SliceEndsAt\" IS NULL AND \"RecordedByFk\" IS NOT NULL AND \"RecordIdentifier\" IS NULL AND \"ExternalReference\" IS NULL AND \"Reason\" IS NOT NULL AND \"PreviousAllowance\" IS NULL AND \"NewAllowance\" IS NULL AND \"FinanceOperationFk\" IS NOT NULL AND \"ReversedEntryFk\" IS NULL) OR (\"Kind\" = 6 AND \"Amount\" <> 0 AND \"EstimatedHourlyRate\" IS NULL AND \"ProviderIdentifier\" IS NULL AND \"SliceStartsAt\" IS NULL AND \"SliceEndsAt\" IS NULL AND \"RecordedByFk\" IS NOT NULL AND \"RecordIdentifier\" IS NULL AND \"ExternalReference\" IS NULL AND \"Reason\" IS NOT NULL AND \"PreviousAllowance\" IS NULL AND \"NewAllowance\" IS NULL AND \"FinanceOperationFk\" IS NOT NULL AND \"ReversedEntryFk\" IS NOT NULL)");
                    table.CheckConstraint("CK_HostingCostLedgerEntries_Slice", "\"SliceStartsAt\" IS NULL OR \"SliceEndsAt\" > \"SliceStartsAt\"");
                    table.ForeignKey(
                        name: "FK_HostingCostLedgerEntries_CostOwners_CostOwnerFk",
                        column: x => x.CostOwnerFk,
                        principalTable: "CostOwners",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingCostLedgerEntries_GameServerDeployments_DeploymentFk",
                        column: x => x.DeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingCostLedgerEntries_HostingCostLedgerEntries_ReversedE~",
                        column: x => x.ReversedEntryFk,
                        principalTable: "HostingCostLedgerEntries",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingCostLedgerEntries_Users_RecordedByFk",
                        column: x => x.RecordedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HostingFinanceOperations",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    DeploymentFk = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceCostOwnerFk = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationCostOwnerFk = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetLedgerEntryFk = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetFinanceOperationFk = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TimeStampEffective = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    RecordedByFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TimeStampCreated = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostingFinanceOperations", x => x.PrimaryKey);
                    table.CheckConstraint("CK_HostingFinanceOperations_Currency", "length(\"Currency\") = 3");
                    table.CheckConstraint("CK_HostingFinanceOperations_Shape", "(\"Kind\" = 0 AND \"DeploymentFk\" IS NOT NULL AND \"SourceCostOwnerFk\" IS NOT NULL AND \"DestinationCostOwnerFk\" IS NOT NULL AND \"SourceCostOwnerFk\" <> \"DestinationCostOwnerFk\" AND \"TargetLedgerEntryFk\" IS NULL AND \"TargetFinanceOperationFk\" IS NULL AND \"Amount\" > 0 AND \"Reason\" IS NOT NULL) OR (\"Kind\" = 1 AND \"SourceCostOwnerFk\" IS NULL AND \"DestinationCostOwnerFk\" IS NULL AND ((\"TargetLedgerEntryFk\" IS NOT NULL AND \"TargetFinanceOperationFk\" IS NULL) OR (\"TargetLedgerEntryFk\" IS NULL AND \"TargetFinanceOperationFk\" IS NOT NULL)) AND \"Amount\" > 0 AND \"Reason\" IS NOT NULL)");
                    table.CheckConstraint("CK_HostingFinanceOperations_TargetNotSelf", "\"TargetFinanceOperationFk\" IS NULL OR \"TargetFinanceOperationFk\" <> \"PrimaryKey\"");
                    table.ForeignKey(
                        name: "FK_HostingFinanceOperations_CostOwners_DestinationCostOwnerFk",
                        column: x => x.DestinationCostOwnerFk,
                        principalTable: "CostOwners",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingFinanceOperations_CostOwners_SourceCostOwnerFk",
                        column: x => x.SourceCostOwnerFk,
                        principalTable: "CostOwners",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingFinanceOperations_GameServerDeployments_DeploymentFk",
                        column: x => x.DeploymentFk,
                        principalTable: "GameServerDeployments",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingFinanceOperations_HostingCostLedgerEntries_TargetLed~",
                        column: x => x.TargetLedgerEntryFk,
                        principalTable: "HostingCostLedgerEntries",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingFinanceOperations_HostingFinanceOperations_TargetFin~",
                        column: x => x.TargetFinanceOperationFk,
                        principalTable: "HostingFinanceOperations",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingFinanceOperations_Users_RecordedByFk",
                        column: x => x.RecordedByFk,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HostingFinanceCorrectionAllocations",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    FinanceOperationFk = table.Column<Guid>(type: "uuid", nullable: false),
                    AffectedLedgerEntryFk = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostingFinanceCorrectionAllocations", x => x.PrimaryKey);
                    table.CheckConstraint("CK_HostingFinanceCorrectionAllocations_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_HostingFinanceCorrectionAllocations_HostingCostLedgerEntrie~",
                        column: x => x.AffectedLedgerEntryFk,
                        principalTable: "HostingCostLedgerEntries",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostingFinanceCorrectionAllocations_HostingFinanceOperation~",
                        column: x => x.FinanceOperationFk,
                        principalTable: "HostingFinanceOperations",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleWorkers_BootstrapCredentialExpiresAt",
                table: "LifecycleWorkers",
                column: "BootstrapCredentialExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleWorkers_PendingWorkerCredentialExpiresAt",
                table: "LifecycleWorkers",
                column: "PendingWorkerCredentialExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleWorkers_WorkerCredentialExpiresAt",
                table: "LifecycleWorkers",
                column: "WorkerCredentialExpiresAt");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LifecycleWorkers_BootstrapConsumption",
                table: "LifecycleWorkers",
                sql: "\"BootstrapCredentialConsumedAt\" IS NULL OR \"BootstrapCredentialExpiresAt\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LifecycleWorkers_BootstrapCredential",
                table: "LifecycleWorkers",
                sql: "\"BootstrapCredentialHash\" IS NULL OR \"BootstrapCredentialExpiresAt\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LifecycleWorkers_PendingWorkerCredential",
                table: "LifecycleWorkers",
                sql: "\"PendingWorkerCredentialHash\" IS NULL OR (\"PendingWorkerCredentialIssuedAt\" IS NOT NULL AND \"PendingWorkerCredentialExpiresAt\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LifecycleWorkers_WorkerCredential",
                table: "LifecycleWorkers",
                sql: "\"WorkerCredentialHash\" IS NULL OR (\"WorkerCredentialIssuedAt\" IS NOT NULL AND \"WorkerCredentialExpiresAt\" IS NOT NULL AND \"WorkerCredentialActivatedAt\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LifecycleWorkers_WorkerCredentialRevocation",
                table: "LifecycleWorkers",
                sql: "\"WorkerCredentialRevokedAt\" IS NULL OR \"WorkerCredentialIssuedAt\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GameServerDeployments_CostReservation",
                table: "GameServerDeployments",
                sql: "(\"CostReservationStartsAt\" IS NULL AND \"CostReservationExpiresAt\" IS NULL AND \"ReservedProviderCostEur\" IS NULL) OR (\"CostReservationStartsAt\" IS NOT NULL AND \"CostReservationExpiresAt\" IS NOT NULL AND \"ReservedProviderCostEur\" > 0 AND \"CostReservationExpiresAt\" > \"CostReservationStartsAt\")");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentResources_ProviderIdentifier_ProvisioningIntentId~",
                table: "DeploymentResources",
                columns: new[] { "ProviderIdentifier", "ProvisioningIntentIdentifier" });

            migrationBuilder.CreateIndex(
                name: "IX_DanglingProviderResourceIncidents_ProviderIdentifier_Resour~",
                table: "DanglingProviderResourceIncidents",
                columns: new[] { "ProviderIdentifier", "ResourceType", "ProviderResourceIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DanglingProviderResourceIncidents_Status_TimeStampUpdated",
                table: "DanglingProviderResourceIncidents",
                columns: new[] { "Status", "TimeStampUpdated" });

            migrationBuilder.CreateIndex(
                name: "IX_HostingCostLedgerEntries_CostOwnerFk_TimeStampCreated",
                table: "HostingCostLedgerEntries",
                columns: new[] { "CostOwnerFk", "TimeStampCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_HostingCostLedgerEntries_DeploymentFk_SliceStartsAt",
                table: "HostingCostLedgerEntries",
                columns: new[] { "DeploymentFk", "SliceStartsAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HostingCostLedgerEntries_FinanceOperationFk",
                table: "HostingCostLedgerEntries",
                column: "FinanceOperationFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingCostLedgerEntries_RecordedByFk",
                table: "HostingCostLedgerEntries",
                column: "RecordedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingCostLedgerEntries_RecordIdentifier_Unique",
                table: "HostingCostLedgerEntries",
                column: "RecordIdentifier",
                unique: true,
                filter: "\"RecordIdentifier\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HostingCostLedgerEntries_ReversedEntryFk_Unique",
                table: "HostingCostLedgerEntries",
                column: "ReversedEntryFk",
                unique: true,
                filter: "\"ReversedEntryFk\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HostingFinanceCorrectionAllocations_AffectedLedgerEntryFk",
                table: "HostingFinanceCorrectionAllocations",
                column: "AffectedLedgerEntryFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingFinanceCorrectionAllocations_FinanceOperationFk_Affe~",
                table: "HostingFinanceCorrectionAllocations",
                columns: new[] { "FinanceOperationFk", "AffectedLedgerEntryFk" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HostingFinanceOperations_DeploymentFk_Kind",
                table: "HostingFinanceOperations",
                columns: new[] { "DeploymentFk", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_HostingFinanceOperations_DestinationCostOwnerFk",
                table: "HostingFinanceOperations",
                column: "DestinationCostOwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingFinanceOperations_RecordedByFk",
                table: "HostingFinanceOperations",
                column: "RecordedByFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingFinanceOperations_SourceCostOwnerFk",
                table: "HostingFinanceOperations",
                column: "SourceCostOwnerFk");

            migrationBuilder.CreateIndex(
                name: "IX_HostingFinanceOperations_TargetFinanceOperationFk_Unique",
                table: "HostingFinanceOperations",
                column: "TargetFinanceOperationFk",
                unique: true,
                filter: "\"TargetFinanceOperationFk\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HostingFinanceOperations_TargetLedgerEntryFk_Unique",
                table: "HostingFinanceOperations",
                column: "TargetLedgerEntryFk",
                unique: true,
                filter: "\"TargetLedgerEntryFk\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_HostingCostLedgerEntries_HostingFinanceOperations_FinanceOp~",
                table: "HostingCostLedgerEntries",
                column: "FinanceOperationFk",
                principalTable: "HostingFinanceOperations",
                principalColumn: "PrimaryKey",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HostingCostLedgerEntries_HostingFinanceOperations_FinanceOp~",
                table: "HostingCostLedgerEntries");

            migrationBuilder.DropTable(
                name: "DanglingProviderResourceIncidents");

            migrationBuilder.DropTable(
                name: "HostingFinanceCorrectionAllocations");

            migrationBuilder.DropTable(
                name: "HostingFinanceOperations");

            migrationBuilder.DropTable(
                name: "HostingCostLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_LifecycleWorkers_BootstrapCredentialExpiresAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropIndex(
                name: "IX_LifecycleWorkers_PendingWorkerCredentialExpiresAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropIndex(
                name: "IX_LifecycleWorkers_WorkerCredentialExpiresAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LifecycleWorkers_BootstrapConsumption",
                table: "LifecycleWorkers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LifecycleWorkers_BootstrapCredential",
                table: "LifecycleWorkers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LifecycleWorkers_PendingWorkerCredential",
                table: "LifecycleWorkers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LifecycleWorkers_WorkerCredential",
                table: "LifecycleWorkers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LifecycleWorkers_WorkerCredentialRevocation",
                table: "LifecycleWorkers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GameServerDeployments_CostReservation",
                table: "GameServerDeployments");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentResources_ProviderIdentifier_ProvisioningIntentId~",
                table: "DeploymentResources");

            migrationBuilder.DropColumn(
                name: "DiagnosticOutput",
                table: "WorkerCommands");

            migrationBuilder.DropColumn(
                name: "BootstrapCredentialConsumedAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "BootstrapCredentialExpiresAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "BootstrapCredentialHash",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "PendingWorkerCredentialExpiresAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "PendingWorkerCredentialHash",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "PendingWorkerCredentialIssuedAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "WorkerCredentialActivatedAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "WorkerCredentialExpiresAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "WorkerCredentialHash",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "WorkerCredentialIssuedAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "WorkerCredentialRevokedAt",
                table: "LifecycleWorkers");

            migrationBuilder.DropColumn(
                name: "CostAuthorizationExpiresAt",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "CostReservationExpiresAt",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "CostReservationStartsAt",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "EstimatedHourlyCostEur",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "ReservedProviderCostEur",
                table: "GameServerDeployments");

            migrationBuilder.DropColumn(
                name: "TimeStampProvisioningRequested",
                table: "DeploymentResources");

            migrationBuilder.DropColumn(
                name: "TimeStampProvisioningResolved",
                table: "DeploymentResources");

            migrationBuilder.AddColumn<string>(
                name: "RuntimeRoot",
                table: "LifecycleWorkers",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "RequestedByFk",
                table: "LifecycleJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AllocatedLocalPort",
                table: "GameServers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_AllocatedLocalPort_Unique",
                table: "GameServers",
                column: "AllocatedLocalPort",
                unique: true,
                filter: "\"AllocatedLocalPort\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GameServers_AllocatedLocalPort",
                table: "GameServers",
                sql: "\"AllocatedLocalPort\" IS NULL OR \"AllocatedLocalPort\" BETWEEN 1 AND 65535");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentResources_ProviderIdentifier_ProvisioningIntentId~",
                table: "DeploymentResources",
                columns: new[] { "ProviderIdentifier", "ProvisioningIntentIdentifier" },
                unique: true);
        }
    }
}
