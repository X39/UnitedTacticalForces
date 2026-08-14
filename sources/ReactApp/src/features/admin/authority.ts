import { getApiClient } from "@/api/client";
import { apiPath, apiPathWithQuery } from "@/api/contract";
import type { components } from "@/generated/api";

export type ClaimView = "all" | "prefix" | "role-backed" | "direct" | "unused";
export type ClaimStatus = "unused" | "direct" | "prefix" | "role-backed";
export type AuthorityClaimDto = components["schemas"]["CommandCenterAuthorityClaimDto"];
export type AuthorityRoleDto = components["schemas"]["CommandCenterAuthorityRoleDto"];
type AuthoritySnapshotDto = components["schemas"]["CommandCenterAuthoritySnapshotDto"];
export type PlainClaim = components["schemas"]["PlainClaimDto"];

export interface AuthorityClaim extends Omit<AuthorityClaimDto, "claim" | "roleTitles"> {
  claim: PlainClaim;
  directMemberCount: number;
  roleCount: number;
  roleTitles: string[];
}

export interface AuthoritySnapshot {
  memberCount: number;
  roleCount: number;
  claimCount: number;
  roles: AuthorityRoleDto[];
  claims: AuthorityClaim[];
}

export function normalizeAuthoritySnapshot(
  snapshot: AuthoritySnapshotDto | undefined,
): AuthoritySnapshot {
  return {
    memberCount: snapshot?.memberCount ?? 0,
    roleCount: snapshot?.roleCount ?? 0,
    claimCount: snapshot?.claimCount ?? 0,
    roles: snapshot?.roles ?? [],
    claims: (snapshot?.claims ?? [])
      .filter((row): row is AuthorityClaimDto & { claim: PlainClaim } => Boolean(row.claim))
      .map((row) => ({
        ...row,
        claim: row.claim,
        directMemberCount: row.directMemberCount ?? 0,
        roleCount: row.roleCount ?? 0,
        roleTitles: row.roleTitles ?? [],
      })),
  };
}

export async function loadAuthoritySnapshot(
  search: string,
  signal?: AbortSignal,
  take = 240,
): Promise<AuthoritySnapshot> {
  const query = new URLSearchParams({ take: String(take) });
  if (search.trim()) query.set("search", search.trim());
  const snapshot = await getApiClient().request<AuthoritySnapshotDto>(
    apiPathWithQuery(apiPath("/command-center/authority-snapshot"), query.toString()),
    { signal },
  );
  return normalizeAuthoritySnapshot(snapshot);
}

export function claimStatus(row: AuthorityClaim): ClaimStatus {
  if (row.directMemberCount === 0 && row.roleCount === 0) return "unused";
  if (row.directMemberCount > 0) return "direct";
  return row.claim.isPrefix ? "prefix" : "role-backed";
}

export function claimMatchesView(row: AuthorityClaim, view: ClaimView): boolean {
  switch (view) {
    case "prefix":
      return Boolean(row.claim.isPrefix);
    case "role-backed":
      return row.roleCount > 0;
    case "direct":
      return row.directMemberCount > 0;
    case "unused":
      return row.directMemberCount === 0 && row.roleCount === 0;
    default:
      return true;
  }
}

export function sortAuthorityClaims(claims: AuthorityClaim[]): AuthorityClaim[] {
  return [...claims].sort((left, right) => {
    const usage =
      right.directMemberCount + right.roleCount - (left.directMemberCount + left.roleCount);
    return (
      usage ||
      (left.claim.category ?? "").localeCompare(right.claim.category ?? "") ||
      (left.claim.title ?? left.claim.identifier ?? "").localeCompare(
        right.claim.title ?? right.claim.identifier ?? "",
      )
    );
  });
}
