import type { FullUser } from "@/api/types";
import {
  canAccessCommandCenter,
  canAccessTerrain,
  canCreateTerrains,
  canDeleteTerrain,
  canManageTerrains,
  canModifyTerrain,
} from "@/auth/claims";
import {
  createTerrainFormSchema,
  formatTerrainTitle,
  isLongRouteValue,
  terrainHasImage,
  terrainMatchesImageFilter,
  type Terrain,
} from "@/features/admin/terrains";

function user(claims: FullUser["claims"]): FullUser {
  return {
    primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f",
    nickname: "Terrain operator",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims,
    roles: [],
  };
}

function claim(identifier: string, value: string | null = null): FullUser["claims"][number] {
  return {
    primaryKey: 1,
    identifier,
    value,
    title: identifier,
    category: "test",
    description: "",
    isPrefix: false,
  };
}

const terrain: Terrain = {
  primaryKey: 41,
  gameIdentifier: "arma3",
  title: "Altis",
  imageAssetFk: "c9a541fe-e715-4643-b085-d655514e518b",
  imagePath: "media-assets/c9a541fe-e715-4643-b085-d655514e518b/content",
  isActive: true,
};

describe("terrain authorization predicates", () => {
  it("keeps list and creation access scoped to their classic base/admin claims", () => {
    expect(canManageTerrains(user([claim("trn")]))).toBe(true);
    expect(canManageTerrains(user([claim("trn", "41")]))).toBe(false);
    expect(canCreateTerrains(user([claim("crt:trn")]))).toBe(true);
    expect(canCreateTerrains(user([claim("trn")]))).toBe(false);
    expect(canCreateTerrains(user([claim("adm:trn", "delegated")]))).toBe(true);
  });

  it("applies exact terrain resource IDs for detail, modify, and delete affordances", () => {
    const operator = user([
      claim("trn:modify", "41"),
      claim("trn:delete", "42"),
      claim("trn", "43"),
    ]);
    expect(canAccessTerrain(operator, "41")).toBe(true);
    expect(canModifyTerrain(operator, "41")).toBe(true);
    expect(canDeleteTerrain(operator, "41")).toBe(false);
    expect(canDeleteTerrain(operator, "42")).toBe(true);
    expect(canModifyTerrain(operator, "43")).toBe(true);
    expect(canDeleteTerrain(operator, "43")).toBe(true);
    expect(canAccessTerrain(operator, "44")).toBe(false);
  });

  it("allows resource terrain grants through the command shell without elevating valued creation claims", () => {
    expect(canAccessCommandCenter(user([claim("trn:modify", "41")]))).toBe(true);
    expect(canAccessCommandCenter(user([claim("crt:trn", "delegated")]))).toBe(false);
  });
});

describe("terrain form and route model", () => {
  it("validates signed Int64 route values without losing precision", () => {
    expect(isLongRouteValue("9223372036854775807")).toBe(true);
    expect(isLongRouteValue("-9223372036854775808")).toBe(true);
    expect(isLongRouteValue("9223372036854775808")).toBe(false);
    expect(isLongRouteValue("41.5")).toBe(false);
    expect(isLongRouteValue(undefined)).toBe(false);
  });

  it("requires a registered game and a trimmed title of at most 1,024 characters", () => {
    const schema = createTerrainFormSchema({
      gameRequired: "game",
      titleRequired: "title",
      titleTooLong: "long",
    });
    expect(schema.safeParse({ gameIdentifier: " ", title: "Altis" }).success).toBe(false);
    expect(schema.safeParse({ gameIdentifier: "arma3", title: " " }).success).toBe(false);
    expect(schema.safeParse({ gameIdentifier: "arma3", title: "A".repeat(1025) }).success).toBe(
      false,
    );
    expect(schema.parse({ gameIdentifier: " arma3 ", title: " Altis " })).toEqual({
      gameIdentifier: "arma3",
      title: "Altis",
    });
  });

  it("filters imagery and supplies localized untitled fallbacks", () => {
    expect(terrainHasImage(terrain)).toBe(true);
    expect(terrainMatchesImageFilter(terrain, "missing-image")).toBe(false);
    expect(terrainMatchesImageFilter({ ...terrain, imagePath: "" }, "missing-image")).toBe(true);
    expect(formatTerrainTitle({ title: "  " }, "Untitled terrain")).toBe("Untitled terrain");
  });
});
