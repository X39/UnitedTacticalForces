import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type { FullUser } from "@/api/types";
import { hasBaseClaim, hasClaim } from "@/auth/claims";
import type { components } from "@/generated/api";
import { loadAuthoritySnapshot, type AuthoritySnapshot } from "@/features/admin/authority";

type CommunitySnapshotDto = components["schemas"]["CommandCenterCommunitySnapshotDto"];
export type CommunityMember = components["schemas"]["CommandCenterCommunityMemberDto"];
export type MemberUser = components["schemas"]["UserDto"];
export type MemberRole = components["schemas"]["PlainRoleDto"];
export type MemberClaim = components["schemas"]["PlainClaimDto"];
export type VereinMembership = components["schemas"]["VereinMembershipDto"];
export type VereinMembershipStatus = components["schemas"]["EVereinMembershipStatus"];
type UserUpdate = components["schemas"]["UserUpdate"];
type MembershipUpdate = components["schemas"]["VereinMembershipUpdatePayload"];

export type MemberView = "attention" | "verified" | "roles" | "verein" | "all";

export interface CommunitySnapshot {
  memberCount: number;
  verifiedMemberCount: number;
  unverifiedMemberCount: number;
  bannedMemberCount: number;
  deletedMemberCount: number;
  activeVereinMemberCount: number;
  suspendedVereinMemberCount: number;
  endedVereinMemberCount: number;
  roleCount: number;
  claimCount: number;
  members: CommunityMember[];
}

export interface MemberPermissions {
  roster: boolean;
  profile: boolean;
  nickname: boolean;
  email: boolean;
  verify: boolean;
  ban: boolean;
  membership: boolean;
  authority: boolean;
}

export interface MemberDetail {
  user: MemberUser;
  membership?: VereinMembership;
  assignedRoles: MemberRole[];
  assignedClaims: MemberClaim[];
  authority?: AuthoritySnapshot;
}

export function memberPermissions(user: FullUser | undefined): MemberPermissions {
  const userAdmin = hasClaim(user, "adm", "adm:usr");
  const roleAdmin = hasClaim(user, "adm", "adm:rls");
  const vereinAdmin = hasClaim(user, "adm", "adm:verein");
  const profile = hasClaim(
    user,
    "adm",
    "adm:usr",
    "usr",
    "usr:modify",
    "usr:nick",
    "usr:mail",
    "usr:verify",
    "usr:ban",
  );
  const membership =
    vereinAdmin ||
    userAdmin ||
    hasClaim(user, "usr") ||
    hasBaseClaim(user, "verein", "verein:members");
  return {
    roster:
      userAdmin ||
      vereinAdmin ||
      roleAdmin ||
      hasBaseClaim(user, "usr", "verein", "verein:members"),
    profile,
    nickname: userAdmin || hasClaim(user, "usr", "usr:modify", "usr:nick"),
    email: userAdmin || hasClaim(user, "usr", "usr:modify", "usr:mail"),
    verify: userAdmin || hasClaim(user, "usr", "usr:verify"),
    ban: userAdmin || hasClaim(user, "usr", "usr:ban"),
    membership,
    authority: roleAdmin,
  };
}

export function normalizeCommunitySnapshot(
  snapshot: CommunitySnapshotDto | undefined,
): CommunitySnapshot {
  return {
    memberCount: snapshot?.memberCount ?? 0,
    verifiedMemberCount: snapshot?.verifiedMemberCount ?? 0,
    unverifiedMemberCount: snapshot?.unverifiedMemberCount ?? 0,
    bannedMemberCount: snapshot?.bannedMemberCount ?? 0,
    deletedMemberCount: snapshot?.deletedMemberCount ?? 0,
    activeVereinMemberCount: snapshot?.activeVereinMemberCount ?? 0,
    suspendedVereinMemberCount: snapshot?.suspendedVereinMemberCount ?? 0,
    endedVereinMemberCount: snapshot?.endedVereinMemberCount ?? 0,
    roleCount: snapshot?.roleCount ?? 0,
    claimCount: snapshot?.claimCount ?? 0,
    members: (snapshot?.members ?? []).filter(
      (member): member is CommunityMember & { primaryKey: string } => Boolean(member.primaryKey),
    ),
  };
}

export async function loadCommunitySnapshot(
  search: string,
  signal?: AbortSignal,
): Promise<CommunitySnapshot> {
  const query = new URLSearchParams({ take: "500" });
  if (search.trim()) query.set("search", search.trim());
  const value = await getApiClient().request<CommunitySnapshotDto>(
    apiPathWithQuery(apiPath("/command-center/community-snapshot"), query.toString()),
    { signal },
  );
  return normalizeCommunitySnapshot(value);
}

export function memberMatchesView(member: CommunityMember, view: MemberView): boolean {
  switch (view) {
    case "attention":
      return !member.isVerified || Boolean(member.isBanned) || Boolean(member.isDeleted);
    case "verified":
      return Boolean(member.isVerified) && !member.isBanned && !member.isDeleted;
    case "roles":
      return (member.roleTitles?.length ?? 0) > 0 || (member.directClaimCount ?? 0) > 0;
    case "verein":
      return Boolean(member.vereinMembershipStatus);
    default:
      return true;
  }
}

export function memberStatus(
  member: Pick<CommunityMember, "isDeleted" | "isBanned" | "isVerified">,
) {
  if (member.isDeleted) return "deleted" as const;
  if (member.isBanned) return "banned" as const;
  return member.isVerified ? ("verified" as const) : ("unverified" as const);
}

export function sortMembers(members: CommunityMember[]): CommunityMember[] {
  return [...members].sort(
    (left, right) =>
      Number(Boolean(left.isVerified)) - Number(Boolean(right.isVerified)) ||
      Number(Boolean(right.isBanned)) - Number(Boolean(left.isBanned)) ||
      Number(Boolean(left.isDeleted)) - Number(Boolean(right.isDeleted)) ||
      (left.nickname ?? "").localeCompare(right.nickname ?? ""),
  );
}

export function memberAvatarSource(member: Pick<CommunityMember, "avatar" | "avatarMimeType">) {
  if (!member.avatar) return undefined;
  const mimeType = /^image\/(?:png|jpe?g|gif|webp)$/i.test(member.avatarMimeType ?? "")
    ? member.avatarMimeType
    : "image/png";
  return `data:${mimeType};base64,${member.avatar}`;
}

export function memberInitials(nickname: string | null | undefined): string {
  const normalized = nickname?.trim();
  if (!normalized) return "?";
  return normalized
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toLocaleUpperCase();
}

async function loadUserRoles(userId: string, signal?: AbortSignal): Promise<MemberRole[]> {
  const value = await getApiClient().request<MemberRole[]>(
    fillApiPath("/users/{userId}/roles", { userId }),
    { signal },
  );
  return value ?? [];
}

async function loadUserClaims(userId: string, signal?: AbortSignal): Promise<MemberClaim[]> {
  const path = fillApiPath("/users/{userId}/claims", { userId });
  const value = await getApiClient().request<MemberClaim[]>(
    apiPathWithQuery(path as "/users/{userId}/claims", "skip=0&take=500"),
    { signal },
  );
  return value ?? [];
}

async function loadMembership(
  userId: string,
  signal?: AbortSignal,
): Promise<VereinMembership | undefined> {
  return getApiClient().request<VereinMembership>(
    fillApiPath("/verein/memberships/{userId}", { userId }),
    { signal, expectedStatuses: [404] },
  );
}

export async function loadMemberDetail(
  userId: string,
  permissions: MemberPermissions,
  signal?: AbortSignal,
): Promise<MemberDetail | undefined> {
  const user = await getApiClient().request<MemberUser>(
    fillApiPath("/users/{userId}", { userId }),
    {
      signal,
      expectedStatuses: [204, 404],
    },
  );
  if (!user) return undefined;

  const [assignedRoles, assignedClaims, authority, membership] = await Promise.all([
    permissions.authority ? loadUserRoles(userId, signal) : Promise.resolve([]),
    permissions.authority ? loadUserClaims(userId, signal) : Promise.resolve([]),
    permissions.authority ? loadAuthoritySnapshot("", signal, 500) : Promise.resolve(undefined),
    permissions.membership ? loadMembership(userId, signal) : Promise.resolve(undefined),
  ]);
  return {
    user,
    assignedRoles,
    assignedClaims,
    ...(authority ? { authority } : {}),
    ...(membership ? { membership } : {}),
  };
}

export async function updateMemberProfile(userId: string, payload: UserUpdate): Promise<void> {
  const path = apiPath("/users/{userId}/update");
  await getApiClient().request(fillApiPath(path, { userId }), {
    method: apiMethod(path, "post"),
    body: payload,
  });
}

export async function upsertMembership(
  userId: string,
  payload: MembershipUpdate,
): Promise<VereinMembership> {
  const path = apiPath("/verein/memberships/{userId}");
  const result = await getApiClient().request<VereinMembership>(fillApiPath(path, { userId }), {
    method: apiMethod(path, "put"),
    body: payload,
  });
  if (!result) throw new Error("Membership update returned no response.");
  return result;
}

export async function setMemberClaim(
  userId: string,
  claimId: number,
  active: boolean,
): Promise<void> {
  const path = apiPath("/users/{userId}/claims/{claimId}/to/{mode}");
  await getApiClient().request(
    fillApiPath(path, { userId, claimId, mode: String(active).toLowerCase() }),
    { method: apiMethod(path, "post") },
  );
}
