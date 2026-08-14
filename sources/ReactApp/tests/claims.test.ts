import type { FullUser } from "@/api/types";
import {
  canAccessCommandCenter,
  canAccessVereinLedger,
  canCreateRoles,
  canCreateGameServers,
  canEditEvents,
  canManageAuthority,
  canManageEvents,
  canManageGallery,
  canManagePublicPages,
  canManageSettings,
  canManageSiteImages,
  canManageVereinDocuments,
  canManageVereinLedger,
  canManageVereinMemberships,
  canReadFieldReports,
  hasBaseClaim,
  hasClaim,
  hasResourceClaim,
  isAuthenticated,
  isGlobalAdministrator,
} from "@/auth/claims";

function user(claims: FullUser["claims"]): FullUser {
  return {
    primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f",
    nickname: "Operator",
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

describe("authorization predicates", () => {
  it("ports exact, base, resource, and global administrator semantics", () => {
    const operator = user([claim("resource.server", "server-1"), claim("event.read", null)]);
    expect(hasClaim(operator, "resource.server")).toBe(true);
    expect(hasBaseClaim(operator, "resource.server")).toBe(false);
    expect(hasBaseClaim(operator, "event.read")).toBe(true);
    expect(hasResourceClaim(operator, "resource.server", "server-1")).toBe(true);
    const administrator = user([claim("adm", null)]);
    expect(isGlobalAdministrator(administrator)).toBe(true);
    expect(hasResourceClaim(administrator, "anything", "anything")).toBe(true);
  });

  it("matches the classic identity bootstrap treatment of deleted and banned users", () => {
    const deleted = { ...user([claim("usr")]), isDeleted: true };
    const banned = { ...user([claim("usr")]), isBanned: true };
    expect(isAuthenticated(deleted)).toBe(true);
    expect(hasBaseClaim(deleted, "usr")).toBe(true);
    expect(isAuthenticated(banned)).toBe(false);
    expect(hasBaseClaim(banned, "usr")).toBe(false);
  });

  it("only exposes command center navigation for its explicit capability set", () => {
    expect(canAccessCommandCenter(user([claim("vrf")]))).toBe(false);
    expect(canAccessCommandCenter(user([claim("crt:ev")]))).toBe(true);
    expect(canAccessCommandCenter(user([claim("sv:view", "server-1")]))).toBe(true);
    expect(canAccessCommandCenter(user([claim("sv:view")]))).toBe(true);
    expect(canAccessCommandCenter(user([claim("content:pages")]))).toBe(true);
    expect(canAccessCommandCenter(user([claim("fin:view-all")]))).toBe(true);
    expect(canAccessCommandCenter(user([claim("adm", "legacy-value")]))).toBe(true);
  });

  it("keeps event creation and event administration permissions distinct", () => {
    expect(canManageEvents(user([claim("crt:ev")]))).toBe(true);
    expect(canEditEvents(user([claim("crt:ev")]))).toBe(false);
    expect(canEditEvents(user([claim("adm:ev", "event-scope")]))).toBe(true);
    expect(canEditEvents(user([claim("adm")]))).toBe(true);
  });

  it("matches classic Verein membership administration claims", () => {
    expect(canManageVereinMemberships(user([claim("verein:members")]))).toBe(true);
    expect(canManageVereinMemberships(user([claim("adm:verein", "resource")]))).toBe(true);
    expect(canManageVereinMemberships(user([claim("adm:usr", "resource")]))).toBe(true);
    expect(canManageVereinMemberships(user([claim("verein:members", "resource")]))).toBe(false);
  });

  it("matches classic Verein Kassenbuch administrator affordances", () => {
    expect(canManageVereinLedger(user([claim("adm:verein", "resource")]))).toBe(true);
    expect(canAccessVereinLedger(user([claim("adm:verein", "resource")]))).toBe(true);
    expect(canManageVereinDocuments(user([claim("adm:verein", "resource")]))).toBe(true);
    expect(canManageVereinLedger(user([claim("verein:kasse", "resource")]))).toBe(false);
  });

  it("matches the classic server-creation affordance for legacy valued claims", () => {
    expect(canCreateGameServers(user([claim("crt:sv", "legacy-value")]))).toBe(true);
    expect(canCreateGameServers(user([claim("adm:hosting", "legacy-value")]))).toBe(true);
  });

  it("keeps public-page, media, and field-report affordances distinct", () => {
    const scopedGlobalAdministrator = user([claim("adm", "legacy-value")]);
    expect(canManagePublicPages(scopedGlobalAdministrator)).toBe(true);
    expect(canManageGallery(scopedGlobalAdministrator)).toBe(true);
    expect(canManageSiteImages(scopedGlobalAdministrator)).toBe(true);
    expect(canReadFieldReports(scopedGlobalAdministrator)).toBe(false);

    const scopedWikiAdministrator = user([claim("adm:wiki", "legacy-value")]);
    expect(canManageGallery(scopedWikiAdministrator)).toBe(true);
    expect(canManageSiteImages(scopedWikiAdministrator)).toBe(true);
    expect(canReadFieldReports(scopedWikiAdministrator)).toBe(false);

    const scopedWikiCreator = user([claim("crt:wiki", "legacy-value")]);
    expect(canManageGallery(scopedWikiCreator)).toBe(false);
    expect(canManageSiteImages(scopedWikiCreator)).toBe(true);
    expect(canReadFieldReports(scopedWikiCreator)).toBe(false);

    const pageEditor = user([claim("content:pages")]);
    expect(canManagePublicPages(pageEditor)).toBe(true);
    expect(canManageGallery(pageEditor)).toBe(true);
    expect(canReadFieldReports(pageEditor)).toBe(false);
  });

  it("ports role and settings administration with legacy valued administrator claims", () => {
    const scopedGlobalAdministrator = user([claim("adm", "legacy-value")]);
    expect(canManageAuthority(scopedGlobalAdministrator)).toBe(true);
    expect(canCreateRoles(scopedGlobalAdministrator)).toBe(true);
    expect(canManageSettings(scopedGlobalAdministrator)).toBe(true);

    const roleAdministrator = user([claim("adm:rls", "legacy-value")]);
    expect(canManageAuthority(roleAdministrator)).toBe(true);
    expect(canCreateRoles(roleAdministrator)).toBe(true);
    expect(canManageSettings(roleAdministrator)).toBe(false);

    const roleCreator = user([claim("crt:rls", "legacy-value")]);
    expect(canManageAuthority(roleCreator)).toBe(false);
    expect(canCreateRoles(roleCreator)).toBe(true);
  });
});
