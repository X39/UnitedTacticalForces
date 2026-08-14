import { describe, expect, it } from "vitest";
import type { FullUser } from "@/api/types";
import {
  getCommandNavigationContext,
  getCommandNavigationGroups,
} from "@/components/commandNavigation";
import {
  commandAttentionItems,
  commandOperationalSummary,
  commandOverviewLinks,
} from "@/pages/CommandOverviewPage";
import type { GameServer, ServerNotification } from "@/features/hosting/types";

function userWithClaims(...identifiers: string[]): FullUser {
  return {
    primaryKey: "00000000-0000-0000-0000-000000000001",
    nickname: "Operator",
    isBanned: false,
    isDeleted: false,
    isVerified: true,
    isVereinMember: false,
    claims: identifiers.map((identifier) => ({ identifier, value: null })),
    roles: [],
  } as FullUser;
}

function server(
  primaryKey: string,
  options: { possibleCost?: boolean; pendingJobs?: number; status?: string } = {},
): GameServer {
  return {
    primaryKey,
    title: `Server ${primaryKey}`,
    pendingJobCount: options.pendingJobs ?? 0,
    deployment: options.status
      ? {
          status: options.status,
          stage: "running",
          possibleCost: options.possibleCost ?? false,
        }
      : null,
  } as unknown as GameServer;
}

describe("command navigation manifest", () => {
  it("drives the global administrator overview from the same permission-aware entries as navigation", () => {
    const user = userWithClaims("adm");
    const overviewPaths = commandOverviewLinks(user).map((link) => link.to);
    const navigationPaths = getCommandNavigationGroups(user)
      .flatMap((group) => group.items)
      .filter((item) => item.showOnOverview)
      .map((item) => item.to);

    expect(overviewPaths).toEqual(navigationPaths);
    expect(overviewPaths).toEqual(
      expect.arrayContaining([
        "/command/servers",
        "/command/events",
        "/command/hosting-finance",
        "/command/mod-packs",
        "/command/terrains",
        "/command/content",
        "/command/verein/documents",
        "/command/verein/kassenbuch",
      ]),
    );
  });

  it("only exposes destinations authorized by granular base claims", () => {
    expect(
      commandOverviewLinks(userWithClaims("crt:ev", "trn", "verein:kasse")).map((link) => link.to),
    ).toEqual(["/command/events", "/command/terrains", "/command/verein/kassenbuch"]);
  });

  it("keeps resource-scoped asset workspaces reachable", () => {
    const user = userWithClaims();
    user.claims = [
      { identifier: "trn", value: "42" },
      { identifier: "mp:modify", value: "17" },
    ];
    const paths = getCommandNavigationGroups(user)
      .flatMap((group) => group.items)
      .map((item) => item.to);

    expect(paths).toContain("/command/terrains");
    expect(paths).toContain("/command/mod-packs");
  });

  it("resolves canonical, detail, and legacy route context without a component-side path chain", () => {
    expect(getCommandNavigationContext("/command/servers/create")?.titleKey).toBe(
      "Shared_CommandCenter_CurrentArea_CreateServer",
    );
    expect(getCommandNavigationContext("/command/servers/server-1")?.titleKey).toBe(
      "Shared_CommandCenter_CurrentArea_ServerWorkspace",
    );
    expect(getCommandNavigationContext("/data/mod-packs/42")?.titleKey).toBe(
      "Shared_CommandCenter_CurrentArea_ModPackDetail",
    );
    expect(getCommandNavigationContext("/command/content/pages/gdpr/edit")?.titleKey).toBe(
      "Shared_CommandCenter_CurrentArea_EditPublicPage",
    );
  });

  it("does not expose global settings merely because another governance area is authorized", () => {
    const items = getCommandNavigationGroups(userWithClaims("content:pages")).flatMap(
      (group) => group.items,
    );
    expect(items.map((item) => item.id)).toContain("content");
    expect(items.map((item) => item.id)).not.toContain("settings");
  });
});

describe("command operational overview", () => {
  it("summarizes active deployments, cost exposure, and pending jobs", () => {
    expect(
      commandOperationalSummary([
        server("one", { status: "Running", possibleCost: true, pendingJobs: 2 }),
        server("two", { status: "Terminated", pendingJobs: 1 }),
        server("three"),
      ]),
    ).toEqual({
      totalServers: 3,
      activeDeployments: 1,
      possibleCost: 1,
      pendingJobs: 3,
    });
  });

  it("merges cost notifications and pending work into one prioritized item per server", () => {
    const servers = [
      server("cost", { status: "Running", possibleCost: true, pendingJobs: 2 }),
      server("pending", { status: "Preparing", pendingJobs: 4 }),
    ];
    const notifications = [
      {
        serverId: "cost",
        title: "Cost server",
        statusLabel: "Running",
        possibleCost: true,
      } as ServerNotification,
    ];

    expect(commandAttentionItems(servers, notifications)).toEqual([
      {
        serverId: "cost",
        title: "Cost server",
        detail: "Running",
        possibleCost: true,
        pendingJobCount: 2,
      },
      {
        serverId: "pending",
        title: "Server pending",
        detail: "running",
        possibleCost: false,
        pendingJobCount: 4,
      },
    ]);
  });
});
