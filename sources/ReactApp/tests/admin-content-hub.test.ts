import { describe, expect, it } from "vitest";
import type { FullUser } from "@/api/types";
import { contentHubAreas } from "@/features/admin/ContentHubPage";

function user(...claims: string[]): FullUser {
  return {
    primaryKey: "00000000-0000-0000-0000-000000000001",
    nickname: "Editor",
    isBanned: false,
    isDeleted: false,
    isVerified: true,
    isVereinMember: false,
    claims: claims.map((identifier) => ({ identifier, value: null })),
    roles: [],
  } as FullUser;
}

describe("content hub authorization", () => {
  it("keeps website and governed document permissions independent", () => {
    expect(contentHubAreas(user("content:pages")).map((area) => area.id)).toEqual(["website"]);
    expect(contentHubAreas(user("verein:documents")).map((area) => area.id)).toEqual(["verein"]);
    expect(contentHubAreas(user("adm")).map((area) => area.id)).toEqual(["website", "verein"]);
  });
});
