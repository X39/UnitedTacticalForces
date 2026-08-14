import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type { components } from "@/generated/api";

type MembershipRequestDto = components["schemas"]["VereinMembershipRequestDto"];
export type MembershipRequestTransition =
  components["schemas"]["VereinMembershipRequestTransitionPayload"];

export type MembershipRequestStatus =
  "Submitted" | "AcceptedForContact" | "Approved" | "Rejected" | "Withdrawn";

export type MembershipRequestView =
  "all" | "open" | "submitted" | "contact" | "approved" | "rejected" | "withdrawn";

export interface MembershipRequest {
  primaryKey: number;
  userFk: string;
  userNickname: string | null;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  email: string;
  phoneNumber: string;
  addressLine1: string;
  addressLine2: string;
  postalCode: string;
  city: string;
  country: string;
  motivation: string;
  acknowledgesMembershipSeparation: boolean;
  acknowledgesDataProcessing: boolean;
  acknowledgesSatzung: boolean;
  status: MembershipRequestStatus;
  decisionReason: string;
  reviewedByFk: string | null;
  reviewedByName: string | null;
  timeStampCreated: string;
  timeStampUpdated: string;
  timeStampResolved: string | null;
}

const legacyStatus: Record<number, MembershipRequestStatus> = {
  0: "Submitted",
  1: "AcceptedForContact",
  2: "Approved",
  3: "Rejected",
  4: "Withdrawn",
};

const statusValues = new Set<MembershipRequestStatus>([
  "Submitted",
  "AcceptedForContact",
  "Approved",
  "Rejected",
  "Withdrawn",
]);

export const membershipRequestQueryKeys = {
  all: ["verein", "admin", "membership-requests"] as const,
};

export function normalizeMembershipRequestStatus(value: unknown): MembershipRequestStatus {
  if (typeof value === "number") return legacyStatus[value] ?? "Submitted";
  if (typeof value === "string") {
    const numeric = Number(value);
    const numericStatus = Number.isInteger(numeric) ? legacyStatus[numeric] : undefined;
    if (numericStatus) return numericStatus;
    if (statusValues.has(value as MembershipRequestStatus)) return value as MembershipRequestStatus;
  }
  return "Submitted";
}

export function normalizeMembershipRequest(dto: MembershipRequestDto): MembershipRequest {
  return {
    primaryKey: dto.primaryKey ?? 0,
    userFk: dto.userFk ?? "",
    userNickname: dto.userNickname ?? null,
    firstName: dto.firstName ?? "",
    lastName: dto.lastName ?? "",
    dateOfBirth: dto.dateOfBirth ?? "",
    email: dto.email ?? "",
    phoneNumber: dto.phoneNumber ?? "",
    addressLine1: dto.addressLine1 ?? "",
    addressLine2: dto.addressLine2 ?? "",
    postalCode: dto.postalCode ?? "",
    city: dto.city ?? "",
    country: dto.country ?? "",
    motivation: dto.motivation ?? "",
    acknowledgesMembershipSeparation: dto.acknowledgesMembershipSeparation ?? false,
    acknowledgesDataProcessing: dto.acknowledgesDataProcessing ?? false,
    acknowledgesSatzung: dto.acknowledgesSatzung ?? false,
    status: normalizeMembershipRequestStatus(dto.status),
    decisionReason: dto.decisionReason ?? "",
    reviewedByFk: dto.reviewedByFk ?? null,
    reviewedByName: dto.reviewedByName ?? null,
    timeStampCreated: dto.timeStampCreated ?? "",
    timeStampUpdated: dto.timeStampUpdated ?? "",
    timeStampResolved: dto.timeStampResolved ?? null,
  };
}

export async function loadMembershipRequests(
  search: string,
  signal?: AbortSignal,
): Promise<MembershipRequest[]> {
  const query = new URLSearchParams({ take: "100" });
  if (search.trim()) query.set("search", search.trim());
  const rows = await getApiClient().request<MembershipRequestDto[]>(
    apiPathWithQuery(apiPath("/verein/admin/membership-requests"), query.toString()),
    { signal },
  );
  return (rows ?? []).map(normalizeMembershipRequest);
}

export async function transitionMembershipRequest(
  requestId: number,
  payload: MembershipRequestTransition,
  signal?: AbortSignal,
): Promise<MembershipRequest> {
  const path = "/verein/admin/membership-requests/{requestId}/transition" as const;
  const row = await getApiClient().request<MembershipRequestDto>(fillApiPath(path, { requestId }), {
    method: apiMethod(path, "post"),
    body: payload,
    signal,
  });
  if (!row) throw new Error("Membership request transition returned no response body.");
  return normalizeMembershipRequest(row);
}

export function isOpenMembershipRequest(request: MembershipRequest): boolean {
  return request.status === "Submitted" || request.status === "AcceptedForContact";
}

export function membershipRequestMatchesView(
  request: MembershipRequest,
  view: MembershipRequestView,
): boolean {
  switch (view) {
    case "open":
      return isOpenMembershipRequest(request);
    case "submitted":
      return request.status === "Submitted";
    case "contact":
      return request.status === "AcceptedForContact";
    case "approved":
      return request.status === "Approved";
    case "rejected":
      return request.status === "Rejected";
    case "withdrawn":
      return request.status === "Withdrawn";
    default:
      return true;
  }
}

export function sortMembershipRequests(requests: MembershipRequest[]): MembershipRequest[] {
  return [...requests].sort((left, right) => {
    const openOrder =
      Number(isOpenMembershipRequest(right)) - Number(isOpenMembershipRequest(left));
    return openOrder || Date.parse(right.timeStampUpdated) - Date.parse(left.timeStampUpdated);
  });
}

export function createMembershipTransition(
  status: Extract<MembershipRequestStatus, "AcceptedForContact" | "Approved" | "Rejected">,
  reason: string,
  memberNumber: string,
  notes: string,
): MembershipRequestTransition | undefined {
  const normalizedReason = reason.trim();
  if (!normalizedReason) return undefined;
  return {
    status,
    reason: normalizedReason,
    memberNumber: status === "Approved" ? memberNumber.trim() || null : null,
    notes: status === "Approved" ? notes.trim() || null : null,
  };
}
