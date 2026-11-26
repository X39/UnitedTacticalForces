import {
  useMutation,
  useQueryClient,
  type MutationScope,
  type QueryClient,
  type UseMutationResult,
} from "@tanstack/react-query";
import { queryKeys } from "@/api/services";
import { ApiError } from "@/api/client";
import type { TranslationKey } from "@/app/localizationTypes";
import {
  applyOptimistic,
  fullControlPermission,
  manageAccessPermission,
  serverPermissionCatalog,
  viewPermission,
} from "@/features/hosting/serverAccessModel";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, ServerAccess } from "@/features/hosting/types";

/** Which kind of subject an access change is about. */
export type AccessSubjectKind = "user" | "role";

/**
 * The serial scope of one server's access mutations. Every access mutation of a server shares it, so two changes to
 * the same server run one after the other and never race each other on the server.
 *
 * @param serverId The id of the server.
 * @returns The scope to pass as the `scope` option of `useMutation`.
 */
export function accessMutationScope(serverId: string): MutationScope {
  return { id: `server-access-${serverId}` };
}

/**
 * Refreshes everything an access change can have moved: the access list, the server and the fleet (each by its exact
 * key, so the access change never refetches a sub-key by accident) and the signed-in member, whose own flags may have
 * changed. The cached candidate searches of both kinds are marked stale as well, because a subject that lost its last
 * permission is a candidate again; invalidating keeps their data and refetches only the search that is on screen.
 * That search is optional, so it is marked stale without being awaited: a slow or retrying search must not hold the
 * settle step, which decides whether the member leaves the workspace.
 *
 * @param queryClient The query client of the app.
 * @param serverId The id of the server whose access changed.
 * @returns A promise that settles when the refetches of the access list, the server, the fleet and the signed-in
 * member have settled.
 */
export async function invalidateAfterAccessChange(
  queryClient: QueryClient,
  serverId: string,
): Promise<void> {
  void queryClient.invalidateQueries({
    queryKey: [...hostingQueryKeys.access(serverId), "candidates"],
  });
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: hostingQueryKeys.access(serverId), exact: true }),
    queryClient.invalidateQueries({ queryKey: hostingQueryKeys.server(serverId), exact: true }),
    queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet, exact: true }),
    queryClient.invalidateQueries({ queryKey: queryKeys.me, exact: true }),
  ]);
}

/** What the settle step of an access change reports to the component that started it (D-15). */
export interface AccessSettleCallbacks {
  /**
   * Called when the member can no longer open the server: the refreshed server read answered 404 or 403. The caller
   * leaves the workspace; nothing of the server may stay on screen.
   */
  onViewLost(): void;
  /**
   * Called when the server still loads but the member may no longer manage its access, so the Access panel is gone.
   * The caller says so and moves focus to a surviving element.
   */
  onManageLost(): void;
}

/**
 * The last step of every access change, run from the hook-level `onSettled` of the access mutations. The hook-level
 * callback is the only place for it: the Access panel unmounts as soon as the refreshed server says the member may no
 * longer manage access, and the `mutate` callbacks of a mutation that was started by an unmounted component never run.
 *
 * It refreshes the access list, the server, the fleet and the signed-in member, then reads the refreshed server from the
 * cache. A server that is gone (null, a 404) or whose read failed with 403 means the member lost View, so `onViewLost`
 * runs; a loaded server with `canManageAccess` false means only the right to manage access is gone, so `onManageLost`
 * runs. The view case wins, because there is no workspace left to stay in.
 *
 * @param queryClient The query client of the app.
 * @param serverId The id of the server whose access changed.
 * @param callbacks What to do when the member lost View or lost the right to manage access.
 * @returns A promise that settles after the refreshes settled and the matching callback, if any, was called.
 */
export async function settleAccessChange(
  queryClient: QueryClient,
  serverId: string,
  callbacks: AccessSettleCallbacks,
): Promise<void> {
  await invalidateAfterAccessChange(queryClient, serverId);
  const state = queryClient.getQueryState<GameServer | null>(hostingQueryKeys.server(serverId));
  if (state === undefined) return;
  if (state.data === null || (state.error instanceof ApiError && state.error.status === 403)) {
    callbacks.onViewLost();
    return;
  }
  if (state.data?.canManageAccess === false) callbacks.onManageLost();
}

/** What {@link useGrantAccess} needs to give one member or role access. */
export interface GrantAccessVariables {
  /** Whether the subject is a member or a role. */
  kind: AccessSubjectKind;
  /** The member id, or the numeric role id as text. */
  subjectId: string;
  /** The display name, used by the caller for the announcement only. */
  name: string;
}

const grantFailureKeys: ReadonlyMap<number, TranslationKey> = new Map([
  [403, "Server_Access_Error_Forbidden"],
  [404, "Server_Access_Error_NotFound"],
]);

/**
 * Chooses the message of a failed grant. The message of the server is never shown: it can name internals, and the
 * status already says what the member can do about it.
 *
 * @param error The value the grant mutation rejected with.
 * @returns The locale key for 403 (no longer allowed) and 404 (subject gone), the generic key otherwise.
 */
export function grantFailureKey(error: unknown): TranslationKey {
  if (error instanceof ApiError)
    return grantFailureKeys.get(error.status) ?? "Server_Access_Error_Grant";
  return "Server_Access_Error_Grant";
}

/**
 * Gives a member or a role access to one server by granting View with a single call. There is no staging: the call is
 * atomic and idempotent, and the grant is effective at once.
 *
 * The mutation runs in the server's serial access scope. On success the candidates of that kind are refreshed, so the
 * new subject leaves the suggestions. Success and a 403 (the member lost the right to manage access elsewhere) then run
 * {@link settleAccessChange} from the hook-level `onSettled`.
 *
 * @param serverId The id of the server that gets a new subject.
 * @param callbacks What to do when the member lost View or the right to manage access through the change.
 * @returns The mutation; `mutate` takes the subject.
 */
export function useGrantAccess(
  serverId: string,
  callbacks: AccessSettleCallbacks,
): UseMutationResult<void, Error, GrantAccessVariables> {
  const queryClient = useQueryClient();
  return useMutation<void, Error, GrantAccessVariables>({
    scope: accessMutationScope(serverId),
    mutationFn: ({ kind, subjectId }) =>
      kind === "user"
        ? hostingService.changeServerUserAccess(serverId, subjectId, viewPermission, true)
        : hostingService.changeServerRoleAccess(serverId, Number(subjectId), viewPermission, true),
    onSuccess: (_result, { kind }) => {
      void queryClient.invalidateQueries({
        queryKey: [...hostingQueryKeys.access(serverId), "candidates", kind],
      });
    },
    onSettled: (_result, error) => {
      if (error === null || (error instanceof ApiError && error.status === 403))
        void settleAccessChange(queryClient, serverId, callbacks);
    },
  });
}

/** What {@link useToggleAccess} needs to change one permission of one member or role. */
export interface ToggleAccessVariables {
  /** Whether the subject is a member or a role. */
  kind: AccessSubjectKind;
  /** The member id, or the numeric role id as text. */
  subjectId: string;
  /** The display name, used by the caller for the announcement only. */
  name: string;
  /** The permission id to grant or revoke. */
  permission: string;
  /** True to grant the permission, false to revoke it. */
  grant: boolean;
}

/**
 * Grants or revokes one permission of one member or role with a single call, applied optimistically.
 *
 * `onMutate` cancels the access query by its exact key and writes the change into the cache, so the switch moves at
 * once. `onError` writes the inverse of that one change only. It never restores a snapshot of the whole list, because a
 * snapshot taken before an earlier toggle would undo that toggle as well when two toggles are in flight. Every toggle of
 * a server runs in the server's serial access scope, one after the other. The hook-level `onSettled` runs
 * {@link settleAccessChange} once the last pending toggle has settled (failed or not, so a 403 refreshes too); it runs
 * even when the component that started the toggle is gone, which `mutate` callbacks do not. The last toggle resolves
 * after that refresh has settled, as {@link useRemoveAllAccess} does.
 *
 * Concurrent toggles need `mutateAsync`: the per-call callbacks of `mutate` fire only for the latest call.
 *
 * @param serverId The id of the server whose access changes.
 * @param callbacks What to do when the member lost View or the right to manage access through the change.
 * @returns The mutation; `mutateAsync` takes the toggle.
 */
export function useToggleAccess(
  serverId: string,
  callbacks: AccessSettleCallbacks,
): UseMutationResult<void, Error, ToggleAccessVariables> {
  const queryClient = useQueryClient();
  const mutationKey = ["server-access", serverId, "toggle"] as const;
  const accessKey = hostingQueryKeys.access(serverId);
  return useMutation<void, Error, ToggleAccessVariables>({
    mutationKey,
    scope: accessMutationScope(serverId),
    mutationFn: ({ kind, subjectId, permission, grant }) =>
      kind === "user"
        ? hostingService.changeServerUserAccess(serverId, subjectId, permission, grant)
        : hostingService.changeServerRoleAccess(serverId, Number(subjectId), permission, grant),
    onMutate: async ({ kind, subjectId, permission, grant }) => {
      await queryClient.cancelQueries({ queryKey: accessKey, exact: true });
      queryClient.setQueryData<ServerAccess>(
        accessKey,
        (current) => current && applyOptimistic(current, { kind, subjectId, permission, grant }),
      );
    },
    onError: (_error, { kind, subjectId, permission, grant }) => {
      queryClient.setQueryData<ServerAccess>(
        accessKey,
        (current) =>
          current && applyOptimistic(current, { kind, subjectId, permission, grant: !grant }),
      );
    },
    onSettled: () => {
      // The settling toggle still counts as pending here, so one means it was the last. The last toggle returns the
      // settle step, so `mutateAsync` resolves only after the access list has been refetched and the caller reads the
      // refetched list instead of the optimistic one.
      if (queryClient.isMutating({ mutationKey }) <= 1)
        return settleAccessChange(queryClient, serverId, callbacks);
      return undefined;
    },
  });
}

/** What {@link useRemoveAllAccess} needs to take every stored permission away from one member or role. */
export interface RemoveAllAccessVariables {
  /** Whether the subject is a member or a role. */
  kind: AccessSubjectKind;
  /** The member id, or the numeric role id as text. */
  subjectId: string;
  /** The display name, used by the caller for the announcement only. */
  name: string;
  /** The permission ids stored for the subject. Implied permissions are not part of it and are never revoked. */
  permissions: readonly string[];
}

/** The permissions that grant the right to manage access; the signed-in member's own removal must reach them last. */
const accessManagingPermissions: readonly string[] = [
  manageAccessPermission,
  fullControlPermission,
];

/**
 * Puts the stored permissions of one subject in the order Remove all access revokes them: catalog order, with the two
 * permissions that carry the right to manage access (Manage access, then Full control) last and ids the catalog does
 * not know first. Without that, a member who removes their own access would lose the right to revoke in the middle of
 * the run and the remaining revokes would be refused.
 *
 * @param stored The permission ids stored for the subject.
 * @returns Every stored id exactly once, in revocation order.
 */
export function revocationOrder(stored: readonly string[]): string[] {
  const known = serverPermissionCatalog.map((entry) => entry.id);
  const unknown = stored.filter((id) => !known.includes(id));
  const regular = known.filter(
    (id) => stored.includes(id) && !accessManagingPermissions.includes(id),
  );
  const managing = accessManagingPermissions.filter((id) => stored.includes(id));
  return [...new Set([...unknown, ...regular, ...managing])];
}

/**
 * Chooses the message of a failed Remove all access.
 *
 * @param error The value the mutation rejected with.
 * @returns The locale key for 403 (no longer allowed to manage access), the partial-failure key otherwise.
 */
export function removeFailureKey(error: unknown): TranslationKey {
  if (error instanceof ApiError && error.status === 403) return "Server_Access_Error_Forbidden";
  return "Server_Access_Error_Remove";
}

/**
 * Takes every stored permission away from one member or role.
 *
 * The permissions are revoked one after the other through the existing single-permission calls, in
 * {@link revocationOrder}. The run stops at the first rejection and rethrows it, so some permissions may be gone while
 * others remain. Whatever happened, the hook-level `onSettled` runs {@link settleAccessChange}, so the list always shows
 * what is left and the callback also runs when the panel is gone. The mutation runs in the server's serial access
 * scope. It resolves after that refresh has settled.
 *
 * @param serverId The id of the server whose subject loses access.
 * @param callbacks What to do when the member lost View or the right to manage access through the change.
 * @returns The mutation; use `mutateAsync` so the caller sees the result of this call.
 */
export function useRemoveAllAccess(
  serverId: string,
  callbacks: AccessSettleCallbacks,
): UseMutationResult<void, Error, RemoveAllAccessVariables> {
  const queryClient = useQueryClient();
  return useMutation<void, Error, RemoveAllAccessVariables>({
    mutationKey: ["server-access", serverId, "remove-all"],
    scope: accessMutationScope(serverId),
    mutationFn: async ({ kind, subjectId, permissions }) => {
      for (const permission of revocationOrder(permissions)) {
        if (kind === "user")
          await hostingService.changeServerUserAccess(serverId, subjectId, permission, false);
        else
          await hostingService.changeServerRoleAccess(
            serverId,
            Number(subjectId),
            permission,
            false,
          );
      }
    },
    onSettled: () => settleAccessChange(queryClient, serverId, callbacks),
  });
}
