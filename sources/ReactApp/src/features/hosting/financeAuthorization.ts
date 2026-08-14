import type { FullUser } from "@/api/types";
import { hasBaseClaim } from "@/auth/claims";

export interface FinancePermissions {
  view: boolean;
  recordPayment: boolean;
  recordRefund: boolean;
  manageAllowance: boolean;
  correctLedger: boolean;
  managePolicy: boolean;
}

export function getFinancePermissions(user: FullUser | undefined): FinancePermissions {
  return {
    view: hasBaseClaim(user, "fin", "fin:view-all"),
    recordPayment: hasBaseClaim(user, "fin", "fin:record-payment"),
    recordRefund: hasBaseClaim(user, "fin", "fin:record-refund"),
    manageAllowance: hasBaseClaim(user, "fin", "fin:manage-allowance"),
    correctLedger: hasBaseClaim(user, "fin", "fin:correct-ledger"),
    managePolicy: hasBaseClaim(user, "fin", "fin:manage-policy"),
  };
}
