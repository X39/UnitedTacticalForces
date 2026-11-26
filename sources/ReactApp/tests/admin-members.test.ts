import { describe, expect, it } from "vitest";
import type { FullUser } from "@/api/types";
import { accountTones, toneOf } from "@/components/statusTones";
import { isValidMemberId } from "@/features/admin/MemberDetailPage";
import {
  memberAvatarSource,
  memberInitials,
  memberMatchesView,
  memberPermissions,
  memberStatus,
  normalizeCommunitySnapshot,
  sortMembers,
} from "@/features/admin/members";

function userWithClaim(identifier: string, value: string | null = null): FullUser {
  return {
    primaryKey: "00000000-0000-0000-0000-000000000001",
    nickname: "Operator",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims: [{ identifier, value }],
    roles: [],
  } as FullUser;
}

describe("member administration projection", () => {
  it("normalizes missing aggregate fields and rejects incomplete roster identities", () => {
    expect(
      normalizeCommunitySnapshot({
        memberCount: 3,
        members: [{ nickname: "Incomplete" }, { primaryKey: "member-1", nickname: "Raven" }],
      }),
    ).toMatchObject({
      memberCount: 3,
      verifiedMemberCount: 0,
      roleCount: 0,
      members: [{ primaryKey: "member-1", nickname: "Raven" }],
    });
  });

  it("mirrors base, administrative, and resource-valued claim predicates", () => {
    expect(memberPermissions(userWithClaim("verein:members")).roster).toBe(true);
    expect(memberPermissions(userWithClaim("verein:members", "resource")).membership).toBe(false);
    expect(memberPermissions(userWithClaim("usr", "member-id"))).toMatchObject({
      roster: false,
      profile: true,
      membership: true,
    });
    expect(memberPermissions(userWithClaim("adm:rls", "legacy-value"))).toMatchObject({
      roster: true,
      authority: true,
    });
    expect(memberPermissions(userWithClaim("usr:verify"))).toMatchObject({
      profile: true,
      verify: true,
      ban: false,
    });
    expect(memberPermissions(userWithClaim("adm", "legacy-value"))).toEqual({
      roster: true,
      profile: true,
      nickname: true,
      email: true,
      verify: true,
      ban: true,
      membership: true,
      authority: true,
    });
  });

  it("classifies status and applies every roster view", () => {
    const attention = { nickname: "A", isVerified: false, roleTitles: [] };
    const verified = { nickname: "B", isVerified: true, roleTitles: [] };
    const authority = { nickname: "C", isVerified: true, roleTitles: ["Planner"] };
    const verein = {
      nickname: "D",
      isVerified: true,
      roleTitles: [],
      vereinMembershipStatus: "Suspended",
    };

    expect(memberStatus(attention)).toBe("unverified");
    expect(memberMatchesView(attention, "attention")).toBe(true);
    expect(memberMatchesView(verified, "verified")).toBe(true);
    expect(memberMatchesView(authority, "roles")).toBe(true);
    expect(memberMatchesView(verein, "verein")).toBe(true);
    expect(memberMatchesView(verified, "all")).toBe(true);
  });

  it("resolves a lexicon tone for every account status the projection can return", () => {
    const statuses = [
      memberStatus({ isDeleted: true }),
      memberStatus({ isBanned: true }),
      memberStatus({ isVerified: true }),
      memberStatus({ isVerified: false }),
    ];

    expect(statuses.map((status) => toneOf(accountTones, status))).toEqual([
      "danger",
      "danger",
      "success",
      "neutral",
    ]);
    expect(toneOf(accountTones, "a-status-the-api-adds-later")).toBe("neutral");
  });

  it("sorts attention first and builds safe avatar fallbacks", () => {
    const rows = sortMembers([
      { nickname: "Zulu", isVerified: true },
      { nickname: "Alpha", isVerified: false },
      { nickname: "Bravo", isVerified: false, isBanned: true },
    ]);
    expect(rows.map((member) => member.nickname)).toEqual(["Bravo", "Alpha", "Zulu"]);
    expect(memberInitials("Raven Actual")).toBe("RA");
    expect(memberInitials(" ")).toBe("?");
    expect(memberAvatarSource({ avatar: "YWJj", avatarMimeType: "text/html" })).toBe(
      "data:image/png;base64,YWJj",
    );
  });

  it("validates constrained member routes before issuing API reads", () => {
    expect(isValidMemberId("83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f")).toBe(true);
    expect(isValidMemberId("not-a-guid")).toBe(false);
    expect(isValidMemberId(undefined)).toBe(false);
  });
});
