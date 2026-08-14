import type { FullUser } from "@/api/types";
import { canOperateServer, getHostingPermissions } from "@/features/hosting/authorization";

function userWithClaims(...claims: { identifier: string; value: string | null }[]): FullUser {
  return {
    primaryKey: "10000000-0000-0000-0000-000000000001",
    nickname: "Operator",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims,
    roles: [],
  } as FullUser;
}

describe("hosting authorization projection", () => {
  it("keeps view, creation, and finance capabilities granular", () => {
    const permissions = getHostingPermissions(
      userWithClaims(
        { identifier: "sv:view", value: null },
        { identifier: "fin:record-payment", value: null },
      ),
    );

    expect(permissions.accessServers).toBe(true);
    expect(permissions.createServers).toBe(false);
    expect(permissions.viewFinance).toBe(false);
    expect(permissions.recordPayment).toBe(true);
    expect(permissions.manageFinancePolicy).toBe(false);
  });

  it("requires an exact server resource value for scoped operation", () => {
    const user = userWithClaims({ identifier: "sv:operate", value: "server-a" });
    expect(canOperateServer(user, "server-a")).toBe(true);
    expect(canOperateServer(user, "server-b")).toBe(false);

    const broadServerClaim = userWithClaims({ identifier: "sv", value: "server-a" });
    expect(canOperateServer(broadServerClaim, "server-a")).toBe(true);
    expect(canOperateServer(broadServerClaim, "server-b")).toBe(false);

    expect(
      canOperateServer(userWithClaims({ identifier: "adm:sv", value: "legacy-value" }), "server-a"),
    ).toBe(false);
    expect(
      canOperateServer(userWithClaims({ identifier: "adm:sv", value: null }), "server-a"),
    ).toBe(true);
  });

  it("shows the fleet navigation for a scoped view grant while leaving filtering to the API", () => {
    const permissions = getHostingPermissions(
      userWithClaims({ identifier: "sv:view", value: "server-a" }),
    );

    expect(permissions.accessServers).toBe(true);
    expect(permissions.createServers).toBe(false);
  });

  it("allows profile creation or exact scoped view without granting server access", () => {
    const creator = getHostingPermissions(userWithClaims({ identifier: "crt:cfg", value: null }));
    const viewer = getHostingPermissions(
      userWithClaims({ identifier: "cfg:view", value: "profile-1" }),
    );

    expect(creator.accessConfigurationProfiles).toBe(true);
    expect(creator.createConfigurationProfiles).toBe(true);
    expect(creator.accessServers).toBe(false);
    expect(viewer.accessConfigurationProfiles).toBe(true);
    expect(viewer.createConfigurationProfiles).toBe(false);
    expect(viewer.accessServers).toBe(false);
    expect(
      getHostingPermissions(userWithClaims({ identifier: "crt:cfg", value: "profile-1" }))
        .createConfigurationProfiles,
    ).toBe(false);
  });

  it("honors the exact empty global administrator claim", () => {
    const administrator = getHostingPermissions(userWithClaims({ identifier: "adm", value: null }));
    const scopedAdministrator = getHostingPermissions(
      userWithClaims({ identifier: "adm", value: "some-resource" }),
    );

    expect(administrator.manageFinancePolicy).toBe(true);
    expect(administrator.createServers).toBe(true);
    expect(scopedAdministrator.createServers).toBe(true);
    expect(scopedAdministrator.manageFinancePolicy).toBe(false);
  });

  it("requires empty finance claims instead of treating resource values as global grants", () => {
    const scopedFinance = getHostingPermissions(
      userWithClaims(
        { identifier: "fin:view-all", value: "owner-a" },
        { identifier: "fin:record-payment", value: "owner-a" },
      ),
    );

    expect(scopedFinance.viewFinance).toBe(false);
    expect(scopedFinance.recordPayment).toBe(false);
  });

  it("matches the classic server-creation affordance for valued creation claims", () => {
    expect(
      getHostingPermissions(userWithClaims({ identifier: "crt:sv", value: null })).createServers,
    ).toBe(true);
    expect(
      getHostingPermissions(userWithClaims({ identifier: "crt:sv", value: "server-a" }))
        .createServers,
    ).toBe(true);
  });
});
