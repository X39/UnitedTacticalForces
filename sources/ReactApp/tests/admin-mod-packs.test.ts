import { describe, expect, it } from "vitest";
import type { FullUser } from "@/api/types";
import {
  canAccessModPack,
  canDeleteModPack,
  canModifyModPack,
  isSelectableStandalone,
  latestActiveRevision,
  modPackDownloadPath,
  modPackMatchesFilter,
  normalizeFullModPack,
  normalizeModPackSection,
  normalizeModPackWorkspace,
  type ModPackWorkspace,
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

function workspace(composition = false): ModPackWorkspace {
  return {
    definition: {
      primaryKey: 42,
      title: "UTF Main",
      isComposition: composition,
      revisionPrimaryKey: composition ? null : 8,
    },
    owner: null,
    latestRevision: null,
    compositionMembers: [],
    revisionPreview: [],
    downloadPreview: [],
    eventUsageCount: 0,
    downloadRecordCount: 0,
    downloadUserCount: 0,
    lastDownloaded: null,
    canModify: false,
    canDelete: false,
    canViewLedgers: false,
  };
}

describe("mod-pack administration model", () => {
  it("normalizes optional collections and drops incomplete composition links", () => {
    expect(normalizeFullModPack({ title: "Main" })).toEqual({
      title: "Main",
      owner: null,
      modPackRevisions: [],
    });
    expect(
      normalizeModPackWorkspace({
        definition: { primaryKey: 4 },
        compositionMembers: [
          {},
          {
            definition: { primaryKey: 7, title: "Radio" },
            revision: { primaryKey: 9, definitionFk: 7 },
          },
        ],
      }),
    ).toMatchObject({
      definition: { primaryKey: 4 },
      compositionMembers: [
        {
          definition: { primaryKey: 7 },
          revision: { primaryKey: 9 },
          owner: null,
        },
      ],
      revisionPreview: [],
      downloadPreview: [],
      canModify: false,
      canDelete: false,
      canViewLedgers: false,
    });
    expect(normalizeModPackWorkspace({})).toBeUndefined();
  });

  it("selects the latest active standalone revision and preserves classic filters", () => {
    const definition = normalizeFullModPack({
      primaryKey: 4,
      isComposition: false,
      modPackRevisions: [
        { primaryKey: 1, isActive: false, timeStampCreated: "2026-08-13T12:00:00Z" },
        { primaryKey: 2, isActive: true, timeStampCreated: "2026-08-12T12:00:00Z" },
        { primaryKey: 3, isActive: true, timeStampCreated: "2026-08-14T12:00:00Z" },
      ],
    });
    expect(latestActiveRevision(definition)?.primaryKey).toBe(3);
    expect(isSelectableStandalone(definition)).toBe(true);
    expect(modPackMatchesFilter(definition, "standalone")).toBe(true);
    expect(modPackMatchesFilter(definition, "compositions")).toBe(false);
    expect(modPackMatchesFilter(normalizeFullModPack({}), "no-revisions")).toBe(true);
  });

  it("ports base, administrative, and exact resource authorization", () => {
    expect(canAccessModPack(user("crt:mp"), 42)).toBe(true);
    expect(canAccessModPack(user("mp:modify", "42"), 42)).toBe(true);
    expect(canModifyModPack(user("mp", "42"), 42)).toBe(true);
    expect(canModifyModPack(user("mp:modify", "41"), 42)).toBe(false);
    expect(canDeleteModPack(user("mp:delete", "42"), 42)).toBe(true);
    expect(canDeleteModPack(user("mp:modify", "42"), 42)).toBe(false);
    expect(canModifyModPack(user("adm", "legacy-value"), 42)).toBe(true);
    expect(canDeleteModPack(user("adm:mp", "legacy-value"), 42)).toBe(true);
  });

  it("normalizes detail tabs and picks revision-specific download paths", () => {
    expect(normalizeModPackSection(" DOWNLOADS ")).toBe("downloads");
    expect(normalizeModPackSection("unknown")).toBe("overview");
    expect(modPackDownloadPath(workspace())).toBe("/mod-packs/42/download/8");
    expect(modPackDownloadPath(workspace(true))).toBe("/mod-packs/42/download/latest");
  });
});
