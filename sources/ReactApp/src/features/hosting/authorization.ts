import type { FullUser } from "@/api/types";
import {
  canAccessConfigurationProfiles,
  canCreateConfigurationProfiles,
  canCreateGameServers,
  hasBaseClaim,
  hasClaim,
  hasResourceClaim,
} from "@/auth/claims";

export interface HostingPermissions {
  accessServers: boolean;
  createServers: boolean;
  accessConfigurationProfiles: boolean;
  createConfigurationProfiles: boolean;
  viewFinance: boolean;
  recordPayment: boolean;
  manageAllowance: boolean;
  manageFinancePolicy: boolean;
}

export function getHostingPermissions(user: FullUser | undefined): HostingPermissions {
  const createServers = canCreateGameServers(user);
  return {
    createServers,
    accessServers: createServers || hasClaim(user, "sv", "sv:view"),
    createConfigurationProfiles: canCreateConfigurationProfiles(user),
    accessConfigurationProfiles: canAccessConfigurationProfiles(user),
    viewFinance: hasBaseClaim(user, "fin", "fin:view-all"),
    recordPayment: hasBaseClaim(user, "fin", "fin:record-payment"),
    manageAllowance: hasBaseClaim(user, "fin", "fin:manage-allowance"),
    manageFinancePolicy: hasBaseClaim(user, "fin", "fin:manage-policy"),
  };
}

export function canOperateServer(user: FullUser | undefined, serverId: string): boolean {
  return (
    hasBaseClaim(user, "adm:sv", "adm:hosting") ||
    hasResourceClaim(user, "sv", serverId) ||
    hasResourceClaim(user, "sv:operate", serverId)
  );
}
