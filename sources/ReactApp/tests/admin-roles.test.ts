import { describe, expect, it } from "vitest";
import {
  normalizeAuthorityRole,
  normalizeRoleMembers,
  roleIsEmpty,
  roleIsHighImpact,
} from "@/features/admin/roles";

describe("role administration projection", () => {
  it("normalizes optional role and membership fields", () => {
    expect(normalizeAuthorityRole(undefined)).toBeUndefined();
    expect(normalizeAuthorityRole({ role: { primaryKey: 7, title: "Operations" } })).toEqual({
      role: { primaryKey: 7, title: "Operations" },
      claims: [],
      memberCount: 0,
    });
    expect(normalizeRoleMembers(undefined)).toEqual({
      role: {},
      assignedMemberCount: 0,
      availableMemberCount: 0,
      assignedMembers: [],
      availableMembers: [],
    });
  });

  it("matches classic empty and high-impact thresholds", () => {
    const empty = normalizeAuthorityRole({ role: {}, claims: [{}], memberCount: 0 })!;
    const highMemberCount = normalizeAuthorityRole({ role: {}, claims: [{}], memberCount: 5 })!;
    const highClaimCount = normalizeAuthorityRole({
      role: {},
      claims: [{}, {}, {}, {}, {}, {}],
      memberCount: 1,
    })!;

    expect(roleIsEmpty(empty)).toBe(true);
    expect(roleIsHighImpact(empty)).toBe(false);
    expect(roleIsHighImpact(highMemberCount)).toBe(true);
    expect(roleIsHighImpact(highClaimCount)).toBe(true);
  });
});
