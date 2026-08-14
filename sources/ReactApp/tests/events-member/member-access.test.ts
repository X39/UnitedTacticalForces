import type { FullUser } from "@/api/types";
import { canReadVereinLedger } from "@/features/events-member/memberAccess";

function user(overrides: Partial<FullUser> = {}): FullUser {
  return {
    primaryKey: "34b29531-739a-41eb-983c-49c105f3475e",
    nickname: "Player",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims: [],
    roles: [],
    ...overrides,
  };
}

describe("member-only Verein access", () => {
  it("allows active members and exact empty-base Verein claims", () => {
    expect(canReadVereinLedger(user({ isVereinMember: true }))).toBe(true);
    expect(
      canReadVereinLedger(user({ claims: [{ identifier: "verein:kasse", value: null }] })),
    ).toBe(true);
    expect(canReadVereinLedger(user({ claims: [{ identifier: "adm", value: null }] }))).toBe(true);
    expect(
      canReadVereinLedger(user({ claims: [{ identifier: "adm:verein", value: "legacy-value" }] })),
    ).toBe(true);
  });

  it("does not treat a resource-scoped claim or an anonymous user as base access", () => {
    expect(
      canReadVereinLedger(user({ claims: [{ identifier: "verein:kasse", value: "42" }] })),
    ).toBe(false);
    expect(canReadVereinLedger(undefined)).toBe(false);
  });
});
