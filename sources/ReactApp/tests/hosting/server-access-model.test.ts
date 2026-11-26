import { describe, expect, it } from "vitest";
import en from "@/locales/en.json";
import de from "@/locales/de.json";
import type { ServerAccess } from "@/features/hosting/types";
import {
  applyOptimistic,
  canAssign,
  effectivePermissions,
  fullControlPermission,
  heldSummaryItems,
  heldSummaryKeys,
  manageAccessPermission,
  needsViewWarning,
  requiresSelfConfirm,
  serverPermissionCatalog,
  viewPermission,
} from "@/features/hosting/serverAccessModel";

/* The ordered permission catalog and the pure rules of the Access panel (D-06, D-07). */

const catalogIds = [
  "sv",
  "sv:view",
  "sv:operate",
  "sv:modify",
  "sv:manage-access",
  "sv:delete-data",
  "sv:manage-secrets",
  "sv:reveal-secrets",
];

describe("serverPermissionCatalog", () => {
  it("lists the eight claims in the fixed order", () => {
    expect(serverPermissionCatalog.map((permission) => permission.id)).toEqual(catalogIds);
  });

  it("names the three special permissions", () => {
    expect(fullControlPermission).toBe("sv");
    expect(viewPermission).toBe("sv:view");
    expect(manageAccessPermission).toBe("sv:manage-access");
  });

  it.each(serverPermissionCatalog.map((permission) => [permission.id, permission] as const))(
    "gives %s a label and a description that exist in English and German",
    (_id, permission) => {
      const english = en as Record<string, string>;
      const german = de as Record<string, string>;
      expect(english[permission.labelKey], permission.labelKey).toBeTruthy();
      expect(german[permission.labelKey], permission.labelKey).toBeTruthy();
      expect(english[permission.descriptionKey], permission.descriptionKey).toBeTruthy();
      expect(german[permission.descriptionKey], permission.descriptionKey).toBeTruthy();
    },
  );
});

describe("effectivePermissions", () => {
  it("returns all eight ids for Full control", () => {
    expect(effectivePermissions(["sv"])).toEqual(catalogIds);
  });

  it("returns a single permission as it is", () => {
    expect(effectivePermissions(["sv:operate"])).toEqual(["sv:operate"]);
  });

  it("returns nothing for nothing", () => {
    expect(effectivePermissions([])).toEqual([]);
  });

  it("returns every id once when Full control is stored beside others", () => {
    expect(effectivePermissions(["sv:operate", "sv"])).toEqual(catalogIds);
  });
});

describe("heldSummaryKeys", () => {
  it("returns the label keys of the stored permissions in catalog order", () => {
    expect(heldSummaryKeys(["sv:operate", "sv:view"])).toEqual([
      "Server_Access_Permission_View",
      "Server_Access_Permission_Operate",
    ]);
  });

  it("returns only the Full control key when Full control is stored", () => {
    expect(heldSummaryKeys(["sv:view", "sv", "sv:modify"])).toEqual([
      "Server_Access_Permission_All",
    ]);
  });

  it("returns nothing for nothing", () => {
    expect(heldSummaryKeys([])).toEqual([]);
  });

  it("yields no label key for an id the catalog does not know", () => {
    expect(heldSummaryKeys(["sv:view", "sv:unknown"])).toEqual(["Server_Access_Permission_View"]);
  });
});

describe("heldSummaryItems", () => {
  it("keeps an unknown id as an item without a label key, after the known permissions", () => {
    expect(heldSummaryItems(["sv:unknown", "sv:operate"])).toEqual([
      { id: "sv:operate", labelKey: "Server_Access_Permission_Operate" },
      { id: "sv:unknown", labelKey: null },
    ]);
  });

  it("collapses to Full control when it is stored", () => {
    expect(heldSummaryItems(["sv:view", "sv"])).toEqual([
      { id: "sv", labelKey: "Server_Access_Permission_All" },
    ]);
  });
});

describe("needsViewWarning", () => {
  it("is true for a subject that holds something without View or Full control", () => {
    expect(needsViewWarning(["sv:operate"])).toBe(true);
  });

  it.each([[[]], [["sv:view", "sv:operate"]], [["sv", "sv:operate"]], [["sv"]], [["sv:view"]]])(
    "is false for %j",
    (permissions) => {
      expect(needsViewWarning(permissions)).toBe(false);
    },
  );
});

describe("canAssign", () => {
  const access = { assignablePermissions: ["sv:view", "sv:manage-access"] };

  it("is true only for a permission in the assignable list", () => {
    expect(canAssign(access, "sv:view")).toBe(true);
    expect(canAssign(access, "sv:manage-access")).toBe(true);
    expect(canAssign(access, "sv")).toBe(false);
    expect(canAssign(access, "sv:operate")).toBe(false);
  });

  it("ignores the delegable list: a permission that may be delegated but not by this caller is false", () => {
    expect(
      canAssign({ assignablePermissions: [], allowedPermissions: ["sv:view"] } as never, "sv:view"),
    ).toBe(false);
  });
});

describe("requiresSelfConfirm", () => {
  it("is true only for removing Full control, View or Manage access on the own row", () => {
    expect(requiresSelfConfirm(true, "sv", false)).toBe(true);
    expect(requiresSelfConfirm(true, "sv:view", false)).toBe(true);
    expect(requiresSelfConfirm(true, "sv:manage-access", false)).toBe(true);
  });

  it("never asks for another subject", () => {
    expect(requiresSelfConfirm(false, "sv", false)).toBe(false);
    expect(requiresSelfConfirm(false, "sv:view", false)).toBe(false);
    expect(requiresSelfConfirm(false, "sv:manage-access", false)).toBe(false);
  });

  it("never asks for a grant", () => {
    for (const id of catalogIds) expect(requiresSelfConfirm(true, id, true)).toBe(false);
  });

  it("never asks for the other permissions", () => {
    for (const id of catalogIds.filter(
      (candidate) => !["sv", "sv:view", "sv:manage-access"].includes(candidate),
    ))
      expect(requiresSelfConfirm(true, id, false)).toBe(false);
  });
});

describe("applyOptimistic", () => {
  const base = {
    allowedPermissions: ["sv", "sv:view", "sv:operate"],
    assignablePermissions: ["sv", "sv:view", "sv:operate"],
    users: [
      { userId: "u1", nickname: "Alice", detail: "#1", permissions: ["sv:view"] },
      { userId: "u2", nickname: "Bob", detail: "#2", permissions: ["sv:view", "sv:operate"] },
    ],
    roles: [{ roleId: 7, title: "Ops", description: "", memberCount: 2, permissions: ["sv:view"] }],
  } as ServerAccess;

  it("adds one permission to one member and leaves everything else untouched", () => {
    const next = applyOptimistic(base, {
      kind: "user",
      subjectId: "u1",
      permission: "sv:operate",
      grant: true,
    });

    expect(next.users[0]?.permissions).toEqual(["sv:view", "sv:operate"]);
    expect(next.users[1]).toBe(base.users[1]);
    expect(next.roles).toBe(base.roles);
    expect(base.users[0]?.permissions).toEqual(["sv:view"]);
  });

  it("removes one permission from a role addressed by its numeric id as text", () => {
    const next = applyOptimistic(base, {
      kind: "role",
      subjectId: "7",
      permission: "sv:view",
      grant: false,
    });

    expect(next.roles[0]?.permissions).toEqual([]);
    expect(next.users).toBe(base.users);
  });

  it("keeps a subject whose last permission is removed", () => {
    const next = applyOptimistic(base, {
      kind: "user",
      subjectId: "u1",
      permission: "sv:view",
      grant: false,
    });

    expect(next.users).toHaveLength(2);
    expect(next.users[0]?.permissions).toEqual([]);
  });

  it("is restored by the inverse change", () => {
    const change = { kind: "user", subjectId: "u2", permission: "sv:operate" } as const;
    const there = applyOptimistic(base, { ...change, grant: false });
    const back = applyOptimistic(there, { ...change, grant: true });

    expect(there.users[1]?.permissions).toEqual(["sv:view"]);
    expect(back.users[1]?.permissions).toEqual(["sv:view", "sv:operate"]);
  });

  it("does not duplicate a permission that is already held or fail on one that is absent", () => {
    const again = applyOptimistic(base, {
      kind: "user",
      subjectId: "u2",
      permission: "sv:operate",
      grant: true,
    });
    const absent = applyOptimistic(base, {
      kind: "user",
      subjectId: "u1",
      permission: "sv:operate",
      grant: false,
    });

    expect(again.users[1]?.permissions).toEqual(["sv:view", "sv:operate"]);
    expect(absent.users[0]?.permissions).toEqual(["sv:view"]);
  });
});
