import type { VereinMembershipRequest } from "@/api/types";

export type MembershipStatus = Exclude<VereinMembershipRequest["status"], number>;

const legacyStatuses: Record<number, MembershipStatus> = {
  0: "Submitted",
  1: "AcceptedForContact",
  2: "Approved",
  3: "Rejected",
  4: "Withdrawn",
};

export function normalizeMembershipStatus(
  status: VereinMembershipRequest["status"],
): MembershipStatus {
  return typeof status === "number" ? (legacyStatuses[status] ?? "Submitted") : status;
}

export function isOpenMembershipStatus(status: VereinMembershipRequest["status"]): boolean {
  const normalized = normalizeMembershipStatus(status);
  return normalized === "Submitted" || normalized === "AcceptedForContact";
}

export function membershipStatusTranslationKey(
  status: VereinMembershipRequest["status"],
):
  | "Pages_Public_Join_RequestStatus_Submitted"
  | "Pages_Public_Join_RequestStatus_AcceptedForContact"
  | "Pages_Public_Join_RequestStatus_Approved"
  | "Pages_Public_Join_RequestStatus_Rejected"
  | "Pages_Public_Join_RequestStatus_Withdrawn" {
  switch (normalizeMembershipStatus(status)) {
    case "AcceptedForContact":
      return "Pages_Public_Join_RequestStatus_AcceptedForContact";
    case "Approved":
      return "Pages_Public_Join_RequestStatus_Approved";
    case "Rejected":
      return "Pages_Public_Join_RequestStatus_Rejected";
    case "Withdrawn":
      return "Pages_Public_Join_RequestStatus_Withdrawn";
    default:
      return "Pages_Public_Join_RequestStatus_Submitted";
  }
}
