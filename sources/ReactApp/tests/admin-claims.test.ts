import { describe, expect, it } from "vitest";
import { claimIdentifier, claimTitle, claimValue } from "@/features/admin/ClaimsPage";
import {
  claimMatchesView,
  claimStatus,
  normalizeAuthoritySnapshot,
  sortAuthorityClaims,
} from "@/features/admin/authority";

describe("claim administration projection", () => {
  it("normalizes aggregate counts and incomplete claim rows", () => {
    expect(
      normalizeAuthoritySnapshot({
        memberCount: 12,
        roleCount: 4,
        claimCount: 2,
        claims: [
          {},
          {
            claim: { primaryKey: 7, identifier: "sv:view", title: "View server" },
            directMemberCount: 2,
          },
        ],
      }),
    ).toMatchObject({
      memberCount: 12,
      roleCount: 4,
      claimCount: 2,
      claims: [
        {
          claim: { primaryKey: 7 },
          directMemberCount: 2,
          roleCount: 0,
          roleTitles: [],
        },
      ],
    });
  });

  it("classifies usage with direct assignment precedence", () => {
    const rows = normalizeAuthoritySnapshot({
      claims: [
        { claim: { isPrefix: true }, directMemberCount: 0, roleCount: 0 },
        { claim: { isPrefix: true }, directMemberCount: 3, roleCount: 2 },
        { claim: { isPrefix: true }, directMemberCount: 0, roleCount: 2 },
        { claim: { isPrefix: false }, directMemberCount: 0, roleCount: 2 },
      ],
    }).claims;

    expect(rows.map(claimStatus)).toEqual(["unused", "direct", "prefix", "role-backed"]);
    expect(claimMatchesView(rows[1]!, "direct")).toBe(true);
    expect(claimMatchesView(rows[2]!, "role-backed")).toBe(true);
    expect(claimMatchesView(rows[0]!, "unused")).toBe(true);
  });

  it("sorts by usage and formats identifiers and values", () => {
    const rows = normalizeAuthoritySnapshot({
      claims: [
        { claim: { title: "Low", identifier: "low" }, roleCount: 1 },
        {
          claim: { title: "High", identifier: "high", value: "alpha", valueType: "guid" },
          directMemberCount: 4,
        },
      ],
    }).claims;

    expect(sortAuthorityClaims(rows)[0]!.claim.title).toBe("High");
    expect(claimTitle({ identifier: "fallback" }, "Unknown")).toBe("fallback");
    expect(claimIdentifier(rows[1]!.claim, "No identifier")).toBe("high = alpha");
    expect(claimValue(rows[1]!.claim, "No fixed value")).toBe("alpha (guid)");
  });
});
