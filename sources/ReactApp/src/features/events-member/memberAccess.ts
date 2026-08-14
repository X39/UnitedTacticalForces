import type { FullUser } from "@/api/types";
import { canAccessVereinLedger } from "@/auth/claims";

export function canReadVereinLedger(user: FullUser | undefined): boolean {
  return canAccessVereinLedger(user);
}
