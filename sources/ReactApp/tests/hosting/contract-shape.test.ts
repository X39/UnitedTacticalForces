import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import type { components, paths } from "@/generated/api";
import type {
  AutomationLimitsInput,
  AutomationUpdateInput,
  SavedAutomationInput,
} from "@/features/hosting/automationModel";
import { hostingService } from "@/features/hosting/services";
import type {
  AccruedCost,
  ConfigurationProfileSummary,
  CostAlert,
  CostAlertSettings,
  GameServerAccruedCost,
  GameConfigurationEntry,
  GameServer,
  LifecycleJob,
  ServerAccess,
  ServerAccessRoleCandidate,
  ServerAccessRoleGrant,
  ServerAccessUserCandidate,
  ServerAccessUserGrant,
  ServerAutomation,
  ServerAutomationLimits,
  ServerAutomationSettings,
  ServerAutomationUpdate,
  ServerChangeArea,
  ServerCostOwnerOption,
  ServerModPackContents,
  ServerModPackOption,
} from "@/features/hosting/types";
import { fleetServer, serverAutomation } from "../helpers/fleetFixtures";
import { workspaceAutomaticJob, workspaceJob } from "../helpers/workspaceFixtures";

type Schemas = components["schemas"];

/** The 200 JSON body of the rotate endpoint, which carries the generated value once. */
type RotateResponse =
  paths["/game-servers/{gameServerId}/secrets/{key}/rotate"]["post"]["responses"][200]["content"]["application/json"];

/**
 * Compile-time proof that the generated schema carries the Phase 65 fields. The assignments below fail the
 * typecheck when the snapshot or the generated types lose one of them.
 */
const tier: Schemas["GameConfigurationEntryDto"]["tier"] = "Common";
const areas: Schemas["GameServerDto"]["changesPendingStartAreas"] = ["ModPack", "Credentials"];
const pending: Schemas["GameServerDto"]["hasChangesPendingStart"] = true;
const items: Schemas["GameServerModPackContentsDto"]["items"] = [{ key: "steam:1", title: "Core" }];
const revisionId: Schemas["ConfigurationProfileSummaryDto"]["latestRevisionId"] = "revision-1";
const availability: Schemas["GameServerModPackOptionDto"]["isAvailable"] = false;
const reason: Schemas["GameServerModPackOptionDto"]["unavailableReason"] = "Removed";
const changeArea: ServerChangeArea = "Configuration";
const rotated: RotateResponse = { key: "rcon-password", value: "generated", version: 4 };
const rotatedValue: Schemas["GameServerSecretValueDto"] = rotated;
const acceptsChosen: Schemas["GameServerSecretStatusDto"]["acceptsChosenValue"] = true;
const valueFormat: Schemas["GameServerSecretStatusDto"]["valueFormat"] = "Token";

/** Compile-time proof that the generated schema carries the Phase 66 accrued cost, alert and settings shapes. */
const accruedSession: Schemas["AccruedCostDto"] = {
  deploymentId: "d0000000-0000-4000-8000-000000000001",
  amount: null,
  asOf: "2026-11-21T14:05:00Z",
  isFinal: false,
};
const accruedForServer: Schemas["GameServerAccruedCostDto"] = {
  gameServerId: "f1ee7000-0000-4000-8000-000000000001",
  session: accruedSession,
};
const alertShape: Schemas["CostAlertDto"] = {
  id: "a1",
  serverId: "s1",
  serverName: "Alpha",
  costOwnerId: "c1",
  thresholdAmount: 5,
  accruedAmount: 5.01,
  currency: "EUR",
  createdAt: "2026-11-21T14:05:00Z",
  kind: "CostThreshold",
  stopsAt: null,
  stopReason: null,
  stopLimit: null,
};
const settingsShape: Schemas["CostAlertSettingsDto"] = {
  costOwnerId: "c1",
  costOwnerName: "Operations",
  revision: 0,
  thresholds: [5, 10],
  discordEnabled: true,
  emailEnabled: false,
  currency: "EUR",
  maxThresholds: 10,
  maxThresholdAmount: 10000,
  canManage: true,
  audience: "Self",
  discord: "Deliverable",
  email: "NoAddress",
};
const serverAlertSummary: Pick<
  Schemas["GameServerDto"],
  "costAlertThresholds" | "canManageCostAlerts"
> = { costAlertThresholds: [5], canManageCostAlerts: true };

/**
 * Compile-time proof that the generated automation types are assignable to the pure model's structural input
 * types, so the model and the contract cannot drift silently. The identity functions below fail the typecheck when
 * either side renames or retypes a member.
 */
const asModelSettings = (value: ServerAutomationSettings): SavedAutomationInput => value;
const asModelLimits = (value: ServerAutomationLimits): AutomationLimitsInput => value;
const asContractUpdate = (value: AutomationUpdateInput): ServerAutomationUpdate => value;

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

/** Answers every fetch with the given JSON body. */
function stubJson(body: unknown): void {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify(body), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    ),
  );
  initializeApiClient(configuration);
}

describe("generated contract shape", () => {
  it("carries the accrued cost, the cost alert, the alert settings and the server alert summary", () => {
    expect(accruedForServer.session?.amount).toBeNull();
    expect([alertShape.thresholdAmount, alertShape.accruedAmount]).toEqual([5, 5.01]);
    expect([settingsShape.audience, settingsShape.discord, settingsShape.email]).toEqual([
      "Self",
      "Deliverable",
      "NoAddress",
    ]);
    expect(serverAlertSummary).toEqual({ costAlertThresholds: [5], canManageCostAlerts: true });
  });

  it("exposes the Phase 66 shapes through the project aliases", () => {
    const session: AccruedCost = {
      ...accruedSession,
      currency: "EUR",
      unavailableReason: null,
      billingUnitSeconds: null,
      billedThrough: null,
    };
    const accrued: GameServerAccruedCost = { ...accruedForServer, session };
    const alert: CostAlert = alertShape;
    const settings: CostAlertSettings = { ...settingsShape, messageLanguage: null };
    const thresholds: GameServer["costAlertThresholds"] = null;
    expect([
      accrued.session?.isFinal,
      alert.currency,
      settings.messageLanguage,
      thresholds,
    ]).toEqual([false, "EUR", null, null]);
  });
  it("carries the tier, the pending-start fields, the pack contents and the latest revision id", () => {
    expect([tier, pending, availability, reason, changeArea, revisionId]).toEqual([
      "Common",
      true,
      false,
      "Removed",
      "Configuration",
      "revision-1",
    ]);
    expect(areas).toEqual(["ModPack", "Credentials"]);
    expect(items).toEqual([{ key: "steam:1", title: "Core" }]);
    expect(rotatedValue).toEqual({ key: "rcon-password", value: "generated", version: 4 });
    expect([acceptsChosen, valueFormat]).toEqual([true, "Token"]);
  });

  it("exposes the generated fields through the project aliases", () => {
    const entryTier: GameConfigurationEntry["tier"] = "Advanced";
    const serverAreas: GameServer["changesPendingStartAreas"] = [];
    const contents: ServerModPackContents["items"] = [];
    const summaryRevision: ConfigurationProfileSummary["latestRevisionId"] = null;
    const option: Pick<ServerModPackOption, "isAvailable"> = {};
    expect([entryTier, serverAreas, contents, summaryRevision, option.isAvailable]).toEqual([
      "Advanced",
      [],
      [],
      null,
      undefined,
    ]);
  });

  it("keeps the generated automation types assignable to the model input types", () => {
    const automation = serverAutomation();
    expect(asModelSettings(automation.settings).version).toBe(0);
    expect(asModelLimits(automation.limits).maxSchedules).toBe(20);
    expect(
      asContractUpdate({
        version: 3,
        idleShutdownEnabled: true,
        idleShutdownMinutes: 30,
        maxRuntimeEnabled: false,
        maxRuntimeHours: 6,
        timeZone: "Europe/Berlin",
        schedules: [{ id: null, action: "Stop", days: [1, 2], time: "23:00" }],
        startConsent: false,
      }).schedules,
    ).toHaveLength(1);
  });

  it("carries the automation endpoint, the job trigger and the alert kind in the contract", () => {
    const update: paths["/game-servers/{gameServerId}/automation"]["put"]["requestBody"]["content"]["application/json"] =
      { version: 0 };
    const trigger: Schemas["LifecycleJobDto"]["trigger"] = "Idle";
    const kind: Schemas["CostAlertDto"]["kind"] = "AutoStop";
    expect([update.version, trigger, kind]).toEqual([0, "Idle", "AutoStop"]);
  });
});

describe("hosting reads of the automation members", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("keeps the automation block and the manage flag when the server sends them", async () => {
    const automation: ServerAutomation = serverAutomation();
    stubJson(fleetServer({ automation, canManageAutomation: true }));
    const server = await hostingService.server("s1");
    expect(server?.automation).toEqual(automation);
    expect(server?.canManageAutomation).toBe(true);
  });

  it("sets the automation members to null when an older API omits them", async () => {
    const legacy: Record<string, unknown> = { ...fleetServer() };
    delete legacy.automation;
    delete legacy.canManageAutomation;
    stubJson([legacy]);
    const [server] = await hostingService.servers();
    expect(server?.automation).toBeNull();
    expect(server?.canManageAutomation).toBeNull();
  });

  it("keeps the job trigger and its limit and leaves an absent trigger absent", async () => {
    const legacy: Record<string, unknown> = { ...workspaceJob() };
    delete legacy.trigger;
    delete legacy.triggerLimit;
    const idle: LifecycleJob = workspaceAutomaticJob("Idle", 30);
    stubJson([idle, legacy]);
    const [automatic, manual] = await hostingService.jobs("s1");
    expect([automatic?.trigger, automatic?.triggerLimit]).toEqual(["Idle", 30]);
    expect(manual).not.toHaveProperty("trigger");
    expect(manual?.trigger).toBeUndefined();
  });
});

/** Compile-time proof that the generated schema carries the Phase 71 access shapes the panel reads. */
const accessShape: Schemas["GameServerAccessDto"] = {
  allowedPermissions: ["sv", "sv:view"],
  assignablePermissions: ["sv:view"],
  users: [{ userId: "u1", nickname: "Raven", detail: "#0000a2", permissions: ["sv:view"] }],
  roles: [{ roleId: 7, title: "Crew", description: "", memberCount: 3, permissions: [] }],
};
const userCandidateShape: Schemas["GameServerUserAccessCandidateDto"] = {
  userId: "u1",
  nickname: "Raven",
  detail: "#0000a2",
};
const roleCandidateShape: Schemas["GameServerRoleAccessCandidateDto"] = {
  roleId: 7,
  title: "Crew",
  description: "",
  memberCount: 3,
};
const grantBody: paths["/game-servers/{gameServerId}/access/users/grant"]["post"]["requestBody"]["content"]["application/json"] =
  { userId: "u1", permission: "sv:view" };

describe("generated access contract", () => {
  it("carries the assignable permissions, the fingerprint detail and the member count", () => {
    expect(accessShape.assignablePermissions).toEqual(["sv:view"]);
    expect(accessShape.users?.[0]?.detail).toBe("#0000a2");
    expect(accessShape.roles?.[0]?.memberCount).toBe(3);
    expect([userCandidateShape.detail, roleCandidateShape.memberCount]).toEqual(["#0000a2", 3]);
    expect(grantBody.permission).toBe("sv:view");
  });

  it("exposes the access shapes through the project aliases", () => {
    const user: ServerAccessUserGrant = {
      userId: "u1",
      nickname: "Raven",
      detail: "#0000a2",
      permissions: [],
    };
    const role: ServerAccessRoleGrant = {
      roleId: 7,
      title: "Crew",
      description: "",
      memberCount: 3,
      permissions: [],
    };
    const access: ServerAccess = {
      allowedPermissions: [],
      assignablePermissions: [],
      users: [user],
      roles: [role],
    };
    const userCandidate: ServerAccessUserCandidate =
      userCandidateShape as ServerAccessUserCandidate;
    const roleCandidate: ServerAccessRoleCandidate =
      roleCandidateShape as ServerAccessRoleCandidate;
    expect([access.users.length, userCandidate.userId, roleCandidate.roleId]).toEqual([1, "u1", 7]);
  });
});

describe("hosting reads of the access members", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("keeps canManageAccess when the server sends it", async () => {
    stubJson(fleetServer({ canManageAccess: true }));
    const server = await hostingService.server("s1");
    expect(server?.canManageAccess).toBe(true);
  });

  it("normalizes a legacy server payload without canManageAccess to false", async () => {
    const legacy: Record<string, unknown> = { ...fleetServer() };
    delete legacy.canManageAccess;
    stubJson([legacy]);
    const [server] = await hostingService.servers();
    expect(server?.canManageAccess).toBe(false);
  });

  it("normalizes a null canManageAccess to false", async () => {
    stubJson([{ ...fleetServer(), canManageAccess: null }]);
    const [server] = await hostingService.servers();
    expect(server?.canManageAccess).toBe(false);
  });

  it("normalizes an access payload without arrays to empty arrays", async () => {
    stubJson({});
    const access = await hostingService.serverAccess("s1");
    expect(access).toEqual({
      allowedPermissions: [],
      assignablePermissions: [],
      users: [],
      roles: [],
    });
  });
});

/** Compile-time proof that the generated schema carries the Phase 71 cost owner shapes the panel reads. */
const ownerOptionShape: Schemas["GameServerCostOwnerOptionDto"] = {
  costOwnerId: "c0000000-0000-4000-8000-000000000002",
  displayName: "Association",
  isFrozen: false,
  isCurrentUser: false,
  kind: "Verein",
};
const ownerFacts: Pick<Schemas["GameServerDto"], "costOwnerIsFrozen" | "costOwnerAssignedAt"> = {
  costOwnerIsFrozen: true,
  costOwnerAssignedAt: "2026-11-20T10:00:00Z",
};
const transferBody: paths["/game-servers/{gameServerId}/cost-owner"]["put"]["requestBody"]["content"]["application/json"] =
  { costOwnerId: "c0000000-0000-4000-8000-000000000002" };
const optionsQuery: NonNullable<
  paths["/game-servers/{gameServerId}/cost-owner/options"]["get"]["parameters"]["query"]
> = { search: "assoc", take: 20 };

describe("generated cost owner contract", () => {
  it("carries the owner kind, the frozen flag, the since instant and the transfer body", () => {
    expect([ownerOptionShape.kind, ownerOptionShape.isFrozen]).toEqual(["Verein", false]);
    expect([ownerFacts.costOwnerIsFrozen, ownerFacts.costOwnerAssignedAt]).toEqual([
      true,
      "2026-11-20T10:00:00Z",
    ]);
    expect(transferBody.costOwnerId).toBe("c0000000-0000-4000-8000-000000000002");
    expect(optionsQuery).toEqual({ search: "assoc", take: 20 });
  });

  it("exposes the owner option and the server owner facts through the project aliases", () => {
    const option: ServerCostOwnerOption = ownerOptionShape as ServerCostOwnerOption;
    const frozen: GameServer["costOwnerIsFrozen"] = true;
    const since: GameServer["costOwnerAssignedAt"] = null;
    expect([option.displayName, frozen, since]).toEqual(["Association", true, null]);
  });
});

describe("hosting reads of the cost owner members", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("keeps the frozen flag and the since instant when the server sends them", async () => {
    stubJson(fleetServer({ costOwnerIsFrozen: true, costOwnerAssignedAt: "2026-11-20T10:00:00Z" }));
    const server = await hostingService.server("s1");
    expect(server?.costOwnerIsFrozen).toBe(true);
    expect(server?.costOwnerAssignedAt).toBe("2026-11-20T10:00:00Z");
  });

  it("normalizes a legacy server payload without the cost owner facts to null", async () => {
    const legacy: Record<string, unknown> = { ...fleetServer() };
    delete legacy.costOwnerIsFrozen;
    delete legacy.costOwnerAssignedAt;
    stubJson([legacy]);
    const [server] = await hostingService.servers();
    expect(server?.costOwnerIsFrozen).toBeNull();
    expect(server?.costOwnerAssignedAt).toBeNull();
  });
});
