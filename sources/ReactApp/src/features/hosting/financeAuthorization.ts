import type { FullUser } from "@/api/types";
import { hasBaseClaim } from "@/auth/claims";

export interface FinancePermissions {
  view: boolean;
  recordPayment: boolean;
  recordRefund: boolean;
  manageAllowance: boolean;
  correctLedger: boolean;
  managePolicy: boolean;
  manageCostOwners: boolean;
  freezeCostOwner: boolean;
}

export function getFinancePermissions(user: FullUser | undefined): FinancePermissions {
  return {
    view: hasBaseClaim(user, "fin", "fin:view-all"),
    recordPayment: hasBaseClaim(user, "fin", "fin:record-payment"),
    recordRefund: hasBaseClaim(user, "fin", "fin:record-refund"),
    manageAllowance: hasBaseClaim(user, "fin", "fin:manage-allowance"),
    correctLedger: hasBaseClaim(user, "fin", "fin:correct-ledger"),
    managePolicy: hasBaseClaim(user, "fin", "fin:manage-policy"),
    manageCostOwners: hasBaseClaim(user, "fin", "fin:manage-cost-owners"),
    freezeCostOwner: hasBaseClaim(user, "fin", "fin:freeze-cost-owner"),
  };
}
