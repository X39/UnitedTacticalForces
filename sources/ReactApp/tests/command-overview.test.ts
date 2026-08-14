import { describe, expect, it } from "vitest";
import type { FullUser } from "@/api/types";
import { commandOverviewLinks } from "@/pages/CommandOverviewPage";

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

describe("commandOverviewLinks", () => {
  it("shows every classic overview destination to a global administrator", () => {
    expect(commandOverviewLinks(userWithClaims("adm")).map((link) => link.to)).toEqual([
      "/command/servers",
      "/command/events",
      "/command/mod-packs",
      "/command/terrains",
      "/command/content",
      "/command/content/gallery",
      "/command/verein/kassenbuch",
    ]);
  });

  it("only exposes destinations authorized by granular base claims", () => {
    expect(
      commandOverviewLinks(userWithClaims("crt:ev", "trn", "verein:kasse")).map((link) => link.to),
    ).toEqual(["/command/events", "/command/terrains", "/command/verein/kassenbuch"]);
  });
});
