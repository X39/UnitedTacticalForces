import type { StatusTone } from "@/components/statusTones";

/**
 * Role status in the roles list and on the role detail page: `empty`, `no-claims`, `no-members` (a role that
 * lacks claims or members), `high-impact` and `assigned`.
 *
 * Every role that needs attention soon is `warning`; a role that grants broad access is `danger`.
 */
export const roleTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["empty", "warning"],
  ["no-claims", "warning"],
  ["no-members", "warning"],
  ["high-impact", "danger"],
  ["assigned", "success"],
]);

/**
 * Claim usage on the claims page: `unused`, `direct`, `prefix` and `role-backed`, matching `ClaimStatus`.
 *
 * `unused` is neutral because the lexicon lists an unused value as inactive.
 */
export const claimUsageTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["unused", "neutral"],
  ["direct", "warning"],
  ["prefix", "info"],
  ["role-backed", "success"],
]);

/** Whether a claim row on the role detail page is `assigned` to the role or still `available`. */
export const roleClaimTones: ReadonlyMap<string, StatusTone> = new Map<string, StatusTone>([
  ["assigned", "success"],
  ["available", "info"],
]);
