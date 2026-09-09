import { describe, expect, it } from "vitest";
import type { FullUser } from "@/api/types";
import {
  canAccessModPack,
  canDeleteModPack,
  canModifyModPack,
  latestActiveRevision,
  modPackMatchesFilter,
  normalizeModPackSection,
  type FullModPackDefinition,
} from "@/features/admin/modPacks";

function user(identifier: string, value: string | null = null): FullUser {
  return {
    primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f",
    nickname: "Operator",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims: [
      {
        primaryKey: 1,
        identifier,
        value,
        title: identifier,
        category: "test",
        description: "",
        isPrefix: false,
      },
    ],
    roles: [],
  };
}

const definition: FullModPackDefinition = {
  primaryKey: 4,
  gameIdentifier: "arma3",
  title: "Operations",
  owner: null,
  modPackRevisions: [
    { primaryKey: 1, isActive: false, timeStampCreated: "2026-08-13T12:00:00Z" },
    { primaryKey: 3, isActive: true, timeStampCreated: "2026-08-14T12:00:00Z" },
  ],
};

describe("mod-pack administration model", () => {
  it("selects the latest active normalized revision", () => {
    expect(latestActiveRevision(definition)?.primaryKey).toBe(3);
    expect(modPackMatchesFilter(definition, "all")).toBe(true);
  });

  it("ports administrative and exact resource authorization", () => {
    expect(canAccessModPack(user("crt:mp"), 42)).toBe(true);
    expect(canAccessModPack(user("mp:modify", "42"), 42)).toBe(true);
    expect(canModifyModPack(user("mp", "42"), 42)).toBe(true);
    expect(canModifyModPack(user("mp:modify", "41"), 42)).toBe(false);
    expect(canDeleteModPack(user("mp:delete", "42"), 42)).toBe(true);
  });

  it("normalizes detail tabs", () => {
    expect(normalizeModPackSection(" DOWNLOADS ")).toBe("downloads");
    expect(normalizeModPackSection("unknown")).toBe("overview");
  });
});
