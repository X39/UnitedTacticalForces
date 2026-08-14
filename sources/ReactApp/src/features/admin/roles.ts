import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type { components } from "@/generated/api";

type AuthorityRoleDto = components["schemas"]["CommandCenterAuthorityRoleDto"];
type AuthorityRoleMembersDto = components["schemas"]["CommandCenterAuthorityRoleMembersDto"];
export type PlainClaim = components["schemas"]["PlainClaimDto"];
export type PlainRole = components["schemas"]["PlainRoleDto"];
export type RoleMember = components["schemas"]["CommandCenterCommunityMemberDto"];

export interface AuthorityRole {
  role: PlainRole;
  claims: PlainClaim[];
  memberCount: number;
}

export interface RoleMembers {
  role: PlainRole;
  assignedMemberCount: number;
  availableMemberCount: number;
  assignedMembers: RoleMember[];
  availableMembers: RoleMember[];
}

export function normalizeAuthorityRole(
  row: AuthorityRoleDto | undefined,
): AuthorityRole | undefined {
  if (!row?.role) return undefined;
  return { role: row.role, claims: row.claims ?? [], memberCount: row.memberCount ?? 0 };
}

export function normalizeRoleMembers(value: AuthorityRoleMembersDto | undefined): RoleMembers {
  return {
    role: value?.role ?? {},
    assignedMemberCount: value?.assignedMemberCount ?? 0,
    availableMemberCount: value?.availableMemberCount ?? 0,
    assignedMembers: value?.assignedMembers ?? [],
    availableMembers: value?.availableMembers ?? [],
  };
}

export function roleIsHighImpact(role: AuthorityRole): boolean {
  return role.memberCount >= 5 || role.claims.length >= 6;
}

export function roleIsEmpty(role: AuthorityRole): boolean {
  return role.memberCount === 0 || role.claims.length === 0;
}

export async function loadRoleMembers(
  roleId: number,
  search: string,
  signal?: AbortSignal,
): Promise<RoleMembers> {
  const query = new URLSearchParams({ take: "160" });
  if (search.trim()) query.set("search", search.trim());
  const path = apiPath("/command-center/authority-role/{roleId}/members");
  const result = await getApiClient().request<AuthorityRoleMembersDto>(
    apiPathWithQuery(fillApiPath(path, { roleId }) as typeof path, query.toString()),
    { signal },
  );
  return normalizeRoleMembers(result);
}

export async function createRole(title: string, description: string): Promise<void> {
  const path = apiPath("/roles/create/role");
  const body = new FormData();
  body.set("title", title);
  body.set("description", description);
  await getApiClient().request(path, { method: apiMethod(path, "post"), body });
}

export async function updateRole(
  roleId: number,
  title: string,
  description: string,
): Promise<void> {
  const path = apiPath("/roles/update/role/{roleId}");
  await getApiClient().request(fillApiPath(path, { roleId }), {
    method: apiMethod(path, "post"),
    body: { title, description },
  });
}

export async function deleteRole(roleId: number): Promise<void> {
  const path = apiPath("/roles/delete/role/{roleId}");
  await getApiClient().request(fillApiPath(path, { roleId }), {
    method: apiMethod(path, "post"),
  });
}

export async function setRoleClaim(
  roleId: number,
  claimId: number,
  active: boolean,
): Promise<void> {
  const path = apiPath("/roles/set/role/{roleId}/claim/{claimId}/to/{mode}");
  await getApiClient().request(fillApiPath(path, { roleId, claimId, mode: String(active) }), {
    method: apiMethod(path, "post"),
  });
}

export async function setRoleMember(
  roleId: number,
  userId: string,
  active: boolean,
): Promise<void> {
  const path = apiPath("/roles/set/user/{userId}/role/{roleId}/to/{mode}");
  await getApiClient().request(fillApiPath(path, { roleId, userId, mode: String(active) }), {
    method: apiMethod(path, "post"),
  });
}
