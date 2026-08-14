import type { FullUser } from "@/api/types";

const globalAdministratorClaim = "adm";
export function isAuthenticated(user: FullUser | undefined): user is FullUser {
  return Boolean(user && !user.isBanned);
}

export function isGlobalAdministrator(user: FullUser | undefined): boolean {
  return (
    isAuthenticated(user) &&
    user.claims.some((claim) => claim.identifier === globalAdministratorClaim && !claim.value)
  );
}

export function hasClaim(user: FullUser | undefined, ...identifiers: string[]): boolean {
  return (
    isAuthenticated(user) &&
    (isGlobalAdministrator(user) ||
      user.claims.some((claim) => identifiers.includes(claim.identifier)))
  );
}

export function hasBaseClaim(user: FullUser | undefined, ...identifiers: string[]): boolean {
  return (
    isAuthenticated(user) &&
    (isGlobalAdministrator(user) ||
      user.claims.some((claim) => identifiers.includes(claim.identifier) && !claim.value))
  );
}

export function hasResourceClaim(
  user: FullUser | undefined,
  identifier: string,
  value: string,
): boolean {
  return (
    isAuthenticated(user) &&
    (isGlobalAdministrator(user) ||
      user.claims.some((claim) => claim.identifier === identifier && claim.value === value))
  );
}

export function isVereinMember(user: FullUser | undefined): boolean {
  return (
    isAuthenticated(user) &&
    (user.isVereinMember || user.vereinMembershipStatus?.toLocaleLowerCase() === "active")
  );
}

export function canAccessCommandCenter(user: FullUser | undefined): boolean {
  return (
    hasClaim(user, "adm") ||
    hasClaim(user, "crt:ev", "adm:ev") ||
    hasClaim(user, "crt:sv", "adm:sv", "adm:hosting", "sv", "sv:view") ||
    canAccessConfigurationProfiles(user) ||
    canManageModPacks(user) ||
    hasClaim(user, "mp", "mp:modify", "mp:delete") ||
    canAccessTerrains(user) ||
    hasClaim(user, "adm:usr") ||
    hasBaseClaim(user, "usr") ||
    hasClaim(user, "adm:rls") ||
    hasClaim(user, "adm:wiki") ||
    hasBaseClaim(user, "crt:wiki", "wiki") ||
    hasBaseClaim(user, "content:pages") ||
    hasBaseClaim(user, "fin", "fin:view-all") ||
    hasClaim(user, "adm:verein") ||
    hasBaseClaim(user, "verein", "verein:documents", "verein:kasse", "verein:members")
  );
}

export function canCreateGameServers(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "crt:sv", "adm:sv", "adm:hosting");
}

export function canAccessGameServers(user: FullUser | undefined): boolean {
  return canCreateGameServers(user) || hasClaim(user, "sv", "sv:view");
}

export function canAccessConfigurationProfiles(user: FullUser | undefined): boolean {
  return canCreateConfigurationProfiles(user) || hasClaim(user, "cfg", "cfg:view");
}

export function canCreateConfigurationProfiles(user: FullUser | undefined): boolean {
  return hasBaseClaim(user, "crt:cfg", "adm:sv", "adm:hosting");
}

export function canManageEvents(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:ev", "crt:ev");
}

export function canEditEvents(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:ev");
}

export function canManageModPacks(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:mp") || hasBaseClaim(user, "crt:mp", "mp");
}

export function canManageTerrains(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:trn") || hasBaseClaim(user, "crt:trn", "trn");
}

export function canAccessTerrains(user: FullUser | undefined): boolean {
  return canManageTerrains(user) || hasClaim(user, "trn", "trn:modify", "trn:delete");
}

export function canCreateTerrains(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:trn", "crt:trn");
}

export function canAccessTerrain(user: FullUser | undefined, terrainId: string): boolean {
  return (
    hasClaim(user, "adm", "adm:trn", "crt:trn") ||
    hasResourceClaim(user, "trn", terrainId) ||
    hasResourceClaim(user, "trn:modify", terrainId) ||
    hasResourceClaim(user, "trn:delete", terrainId)
  );
}

export function canModifyTerrain(user: FullUser | undefined, terrainId: string): boolean {
  return (
    hasClaim(user, "adm", "adm:trn") ||
    hasResourceClaim(user, "trn", terrainId) ||
    hasResourceClaim(user, "trn:modify", terrainId)
  );
}

export function canDeleteTerrain(user: FullUser | undefined, terrainId: string): boolean {
  return (
    hasClaim(user, "adm", "adm:trn") ||
    hasResourceClaim(user, "trn", terrainId) ||
    hasResourceClaim(user, "trn:delete", terrainId)
  );
}

export function canManageContent(user: FullUser | undefined): boolean {
  return canManagePublicPages(user) || canManageVereinDocuments(user);
}

export function canManagePublicPages(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm") || hasBaseClaim(user, "content:pages");
}

export function canManageGallery(user: FullUser | undefined): boolean {
  return (
    hasClaim(user, "adm", "adm:wiki") ||
    hasBaseClaim(user, "crt:wiki", "wiki") ||
    hasBaseClaim(user, "content:pages")
  );
}

export function canManageSiteImages(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:wiki", "crt:wiki") || hasBaseClaim(user, "content:pages");
}

export function canReadFieldReports(user: FullUser | undefined): boolean {
  return hasBaseClaim(user, "adm:wiki", "crt:wiki", "wiki");
}

export function canCreateFieldReports(user: FullUser | undefined): boolean {
  return canReadFieldReports(user);
}

export function canModifyFieldReports(user: FullUser | undefined): boolean {
  return hasBaseClaim(user, "adm:wiki", "wiki:modify", "wiki");
}

export function canDeleteFieldReports(user: FullUser | undefined): boolean {
  return hasBaseClaim(user, "adm:wiki", "wiki:delete", "wiki");
}

export function canManageAuthority(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:rls");
}

export function canCreateRoles(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:rls", "crt:rls");
}

export function canManageSettings(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm");
}

export function canManageVereinLedger(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:verein") || hasBaseClaim(user, "verein", "verein:kasse");
}

export function canManageVereinMemberships(user: FullUser | undefined): boolean {
  return (
    hasClaim(user, "adm", "adm:usr", "adm:verein") ||
    hasBaseClaim(user, "usr") ||
    hasBaseClaim(user, "verein", "verein:members")
  );
}

export function canManageVereinDocuments(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:verein") || hasBaseClaim(user, "verein", "verein:documents");
}

export function canAccessVereinLedger(user: FullUser | undefined): boolean {
  return (
    isVereinMember(user) ||
    hasClaim(user, "adm", "adm:verein") ||
    hasBaseClaim(user, "verein", "verein:kasse")
  );
}
