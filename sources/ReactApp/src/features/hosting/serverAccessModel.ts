import type { TranslationKey } from "@/app/localizationTypes";
import type { ServerAccess } from "@/features/hosting/types";

/** The permission that includes every other server permission. */
export const fullControlPermission = "sv";

/** The permission that lets a subject see the server at all. */
export const viewPermission = "sv:view";

/** The permission that lets a subject give and remove server permissions. */
export const manageAccessPermission = "sv:manage-access";

/** One server permission of the catalog: its claim id and the locale keys of its label and description. */
export interface ServerPermissionDefinition {
  /** The claim id the API stores and accepts, for example `sv:operate`. */
  readonly id: string;
  /** The locale key of the short permission name. */
  readonly labelKey: TranslationKey;
  /** The locale key of the sentence that says what the permission allows. */
  readonly descriptionKey: TranslationKey;
}

/**
 * The eight delegable server permissions in the fixed display order: Full control first, then View, Operate, Change
 * settings, Manage access, Delete data, Manage credentials and Reveal credentials.
 */
export const serverPermissionCatalog: readonly ServerPermissionDefinition[] = [
  {
    id: fullControlPermission,
    labelKey: "Server_Access_Permission_All",
    descriptionKey: "Server_Access_Permission_All_Description",
  },
  {
    id: viewPermission,
    labelKey: "Server_Access_Permission_View",
    descriptionKey: "Server_Access_Permission_View_Description",
  },
  {
    id: "sv:operate",
    labelKey: "Server_Access_Permission_Operate",
    descriptionKey: "Server_Access_Permission_Operate_Description",
  },
  {
    id: "sv:modify",
    labelKey: "Server_Access_Permission_Modify",
    descriptionKey: "Server_Access_Permission_Modify_Description",
  },
  {
    id: manageAccessPermission,
    labelKey: "Server_Access_Permission_ManageAccess",
    descriptionKey: "Server_Access_Permission_ManageAccess_Description",
  },
  {
    id: "sv:delete-data",
    labelKey: "Server_Access_Permission_DeleteData",
    descriptionKey: "Server_Access_Permission_DeleteData_Description",
  },
  {
    id: "sv:manage-secrets",
    labelKey: "Server_Access_Permission_ManageSecrets",
    descriptionKey: "Server_Access_Permission_ManageSecrets_Description",
  },
  {
    id: "sv:reveal-secrets",
    labelKey: "Server_Access_Permission_RevealSecrets",
    descriptionKey: "Server_Access_Permission_RevealSecrets_Description",
  },
];

/** One entry of a row summary: a stored permission and, when the catalog knows it, the locale key of its label. */
export interface HeldSummaryItem {
  /** The stored permission id. */
  readonly id: string;
  /** The locale key of the label, or null for an id the catalog does not know (the row then shows the raw id). */
  readonly labelKey: TranslationKey | null;
}

/**
 * Tells which permissions a subject effectively holds. Full control includes every permission of the catalog; any
 * other stored permission stands only for itself.
 *
 * @param stored The permission ids stored for the subject on this server.
 * @returns Every catalog id when Full control is stored, otherwise the stored ids as they are.
 */
export function effectivePermissions(stored: readonly string[]): string[] {
  if (stored.includes(fullControlPermission))
    return serverPermissionCatalog.map((permission) => permission.id);
  return [...stored];
}

/**
 * Lists what a row summary shows for the stored permissions: only Full control when it is stored, otherwise the
 * known permissions in catalog order followed by any id the catalog does not know.
 *
 * @param stored The permission ids stored for the subject on this server.
 * @returns The summary entries in display order.
 */
export function heldSummaryItems(stored: readonly string[]): HeldSummaryItem[] {
  const full = serverPermissionCatalog[0];
  if (full && stored.includes(full.id)) return [{ id: full.id, labelKey: full.labelKey }];
  const known: HeldSummaryItem[] = serverPermissionCatalog
    .filter((permission) => stored.includes(permission.id))
    .map((permission) => ({ id: permission.id, labelKey: permission.labelKey }));
  const catalogIds = new Set(serverPermissionCatalog.map((permission) => permission.id));
  const unknown: HeldSummaryItem[] = stored
    .filter((id) => !catalogIds.has(id))
    .map((id) => ({ id, labelKey: null }));
  return [...known, ...unknown];
}

/**
 * Lists the label keys of the stored permissions for a row summary. An id the catalog does not know has no label
 * key and is left out.
 *
 * @param stored The permission ids stored for the subject on this server.
 * @returns Only the Full control key when Full control is stored, otherwise the keys in catalog order.
 */
export function heldSummaryKeys(stored: readonly string[]): TranslationKey[] {
  return heldSummaryItems(stored).flatMap((item) =>
    item.labelKey === null ? [] : [item.labelKey],
  );
}

/**
 * Tells whether a subject holds permissions it cannot use because it lacks View. Without View or Full control a
 * subject cannot even list the server, so the grants are inert and the row warns about it.
 *
 * @param stored The permission ids stored for the subject on this server.
 * @returns True when something is stored but neither View nor Full control is.
 */
export function needsViewWarning(stored: readonly string[]): boolean {
  return (
    stored.length > 0 && !stored.includes(fullControlPermission) && !stored.includes(viewPermission)
  );
}

/**
 * Tells whether the signed-in member may grant or revoke a permission. Only the assignable list decides: a
 * permission may be delegable in general and still be out of reach for this member.
 *
 * @param access The access list; only its assignable permissions are read.
 * @param permission The permission id.
 * @returns True when the permission is in `assignablePermissions`.
 */
export function canAssign(
  access: { readonly assignablePermissions: readonly string[] },
  permission: string,
): boolean {
  return access.assignablePermissions.includes(permission);
}

/**
 * Tells whether a change needs a confirmation first because it could lock the signed-in member out. Only removing Full
 * control, View or Manage access from the member's own row asks: without them the member cannot open the server or
 * undo the change. Granting never asks, and neither does removing any other permission or touching another subject.
 *
 * @param isSelf Whether the row is the signed-in member's own row.
 * @param permission The permission id of the switch.
 * @param grant True when the permission is granted, false when it is removed.
 * @returns True when the change must be confirmed before it runs.
 */
export function requiresSelfConfirm(isSelf: boolean, permission: string, grant: boolean): boolean {
  return (
    isSelf &&
    !grant &&
    (permission === fullControlPermission ||
      permission === viewPermission ||
      permission === manageAccessPermission)
  );
}

/** One permission change of one subject, as the optimistic cache update and its inverse describe it. */
export interface OptimisticAccessChange {
  /** Whether the subject is a member (`user`) or a role. */
  readonly kind: "user" | "role";
  /** The member id, or the numeric role id as text. */
  readonly subjectId: string;
  /** The permission id that changes. */
  readonly permission: string;
  /** True to add the permission, false to remove it. */
  readonly grant: boolean;
}

/**
 * Applies one permission change to one subject of an access list without touching anything else. The subject is never
 * deleted, even when its last permission is removed: a row must not vanish under the pointer, so the subject leaves the
 * list only when the refetch after the change says so. Applying the inverse change restores the pair.
 *
 * @param access The access list to start from; it is not modified.
 * @param change The permission change to apply.
 * @returns A new access list in which only the addressed subject has a different permission array.
 */
export function applyOptimistic(
  access: ServerAccess,
  change: OptimisticAccessChange,
): ServerAccess {
  const update = (permissions: string[]): string[] => {
    if (change.grant)
      return permissions.includes(change.permission)
        ? permissions
        : [...permissions, change.permission];
    return permissions.includes(change.permission)
      ? permissions.filter((permission) => permission !== change.permission)
      : permissions;
  };
  if (change.kind === "user")
    return {
      ...access,
      users: access.users.map((grant) =>
        grant.userId === change.subjectId
          ? { ...grant, permissions: update(grant.permissions) }
          : grant,
      ),
    };
  return {
    ...access,
    roles: access.roles.map((grant) =>
      String(grant.roleId) === change.subjectId
        ? { ...grant, permissions: update(grant.permissions) }
        : grant,
    ),
  };
}
