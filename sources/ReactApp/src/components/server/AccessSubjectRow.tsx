import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import { ActionButton } from "@/components/ActionControl";
import { Disclosure } from "@/components/Disclosure";
import { SwitchField } from "@/components/FormFields";
import { StatePanel, StatusBadge } from "@/components/primitives";
import {
  canAssign,
  effectivePermissions,
  fullControlPermission,
  heldSummaryItems,
  needsViewWarning,
  serverPermissionCatalog,
} from "@/features/hosting/serverAccessModel";
import "@/components/server/access.css";

/** The empty set a row falls back to when no permission of it failed or is pending. */
const noPermissions: ReadonlySet<string> = new Set();

/** The assignable list a row falls back to when the caller passes none: nothing may be changed. */
const noAssignable: readonly string[] = [];

/** One member or role of the access list, reduced to what a row shows. */
export interface AccessSubject {
  /** Whether the subject is a member (`user`) or a role. */
  kind: "user" | "role";
  /** The member id or the role id as text; it keys the row. */
  id: string;
  /** The nickname or role title. It is the title of the row and is rendered as text inside `bdi`. */
  name: string;
  /** The permission ids stored for the subject on this server. */
  permissions: readonly string[];
  /** The short fingerprint that tells members with equal nicknames apart; only members have one. */
  detail?: string | undefined;
  /** The role description; only roles have one. */
  description?: string | undefined;
  /** The number of active members a grant to the role reaches; only roles have one. */
  memberCount?: number | undefined;
}

/** Props of {@link AccessSubjectRow}. */
export interface AccessSubjectRowProps {
  /** The member or role the row shows. */
  subject: AccessSubject;
  /** Whether the subject is the signed-in member; the row then carries the You badge. */
  isSelf: boolean;
  /** Whether the row content is shown. The row is controlled, so a refetch never closes an open row. */
  open: boolean;
  /**
   * Called with the new state when the member toggles the row.
   *
   * @param open Whether the row is now open.
   */
  onOpenChange(open: boolean): void;
  /**
   * Called when the member flips a permission switch. Without it the row shows no switches.
   *
   * @param permission The permission id of the switch.
   * @param grant True when the switch was turned on, false when it was turned off.
   */
  onToggle?: ((permission: string, grant: boolean) => void) | undefined;
  /** The permission ids of this subject whose last change failed; each shows an alert under its switch. */
  failedPermissions?: ReadonlySet<string> | undefined;
  /**
   * The failed permission ids whose change was refused with 403. Their alert says the member may no longer manage
   * access, instead of asking to try again.
   */
  forbiddenPermissions?: ReadonlySet<string> | undefined;
  /** The permission ids of this subject whose change is in flight; each switch is busy and ignores input. */
  pendingPermissions?: ReadonlySet<string> | undefined;
  /** The permission ids the signed-in member may grant and revoke; every other switch is disabled with the reason. */
  assignablePermissions?: readonly string[] | undefined;
  /**
   * Called when the member presses Remove all access. Without it the row shows no such button. The button is
   * unavailable, with a visible reason, while the subject stores a permission the member may not assign.
   */
  onRemoveAll?: (() => void) | undefined;
  /** Content shown after the description, the warning, the switches and the Remove all access action. */
  children?: ReactNode | undefined;
}

/** Props of {@link PermissionSwitches}. */
interface PermissionSwitchesProps {
  /** The permission ids stored for the subject. */
  permissions: readonly string[];
  /** The permission ids whose last change failed. */
  failedPermissions: ReadonlySet<string>;
  /** The failed permission ids that were refused with 403. */
  forbiddenPermissions: ReadonlySet<string>;
  /** The permission ids whose change is in flight. */
  pendingPermissions: ReadonlySet<string>;
  /** The permission ids the signed-in member may grant and revoke. */
  assignablePermissions: readonly string[];
  /**
   * Called when a switch is flipped.
   *
   * @param permission The permission id of the switch.
   * @param grant True when the switch was turned on.
   */
  onToggle(permission: string, grant: boolean): void;
}

/**
 * The eight permission switches of one subject in catalog order. Each is a native switch named by the permission label
 * and described by what the permission allows, and shows the word Granted or Not granted next to the track, so the state
 * never depends on colour alone.
 *
 * With Full control held the other seven show on and disabled with the hint that Full control includes them; their
 * stored grants are untouched. A permission the signed-in member may not assign is disabled with the reason as its hint.
 * A switch whose own request is in flight is busy and ignores input without losing focus, while the others stay
 * operable. A failed change shows its alert directly under the switch.
 *
 * @param props.permissions The permission ids stored for the subject.
 * @param props.failedPermissions The permission ids whose last change failed.
 * @param props.forbiddenPermissions The failed permission ids that were refused with 403.
 * @param props.pendingPermissions The permission ids whose change is in flight.
 * @param props.assignablePermissions The permission ids the member may change.
 * @param props.onToggle Called with the permission id and the new state.
 * @returns The switch grid.
 */
function PermissionSwitches({
  permissions,
  failedPermissions,
  forbiddenPermissions,
  pendingPermissions,
  assignablePermissions,
  onToggle,
}: PermissionSwitchesProps) {
  const { t } = useTranslation();
  const held = effectivePermissions(permissions);
  const fullControlHeld = permissions.includes(fullControlPermission);
  return (
    <div className="access-switches">
      {serverPermissionCatalog.map((permission) => {
        const included = fullControlHeld && permission.id !== fullControlPermission;
        const assignable = canAssign({ assignablePermissions }, permission.id);
        const pending = pendingPermissions.has(permission.id);
        let hint = t(permission.descriptionKey);
        if (included) hint = t("Server_Access_IncludedInFull");
        else if (!assignable) hint = t("Server_Access_NotAssignable");
        return (
          <SwitchField
            key={permission.id}
            label={t(permission.labelKey)}
            hint={hint}
            onLabel={t("Server_Access_State_On")}
            offLabel={t("Server_Access_State_Off")}
            checked={held.includes(permission.id)}
            disabled={included || !assignable}
            aria-busy={pending ? true : undefined}
            error={
              failedPermissions.has(permission.id)
                ? t(
                    forbiddenPermissions.has(permission.id)
                      ? "Server_Access_Error_Forbidden"
                      : "Server_Access_Error_Change",
                  )
                : undefined
            }
            onChange={(event) => {
              if (!pending) onToggle(permission.id, event.target.checked);
            }}
          />
        );
      })}
    </div>
  );
}

/**
 * The held permissions of one subject as an `Intl.ListFormat` string. Full control stands alone; an id the catalog
 * does not know is shown as its raw id in a `code` element.
 *
 * @param props.permissions The permission ids stored for the subject.
 * @returns The list, or nothing for a subject without permissions.
 */
function HeldSummary({ permissions }: { permissions: readonly string[] }) {
  const { t } = useTranslation();
  const items = heldSummaryItems(permissions);
  if (items.length === 0) return null;
  const parts = new Intl.ListFormat(currentLocale(), { type: "unit" }).formatToParts(
    items.map((item) => item.id),
  );
  return (
    <span className="access-row__held">
      {parts.map((part, position) => {
        if (part.type === "literal") return part.value;
        const item = items[parts.slice(0, position).filter((p) => p.type === "element").length];
        if (!item) return null;
        return item.labelKey === null ? <code key={position}>{item.id}</code> : t(item.labelKey);
      })}
    </span>
  );
}

/**
 * One subject of the access list on an inline disclosure.
 *
 * The row is an `li` with an `h3` toggle named by the subject name. It is closed by default and the app never opens
 * it. The badges say what the subject is (Member or Role), whether it is the signed-in member, and whether it holds
 * permissions without View, in which case it cannot see the server and an in-row warning says so. The always visible
 * summary lists the held permissions, and a role also shows how many members it reaches while a member shows the
 * short fingerprint that separates equal nicknames. Names and descriptions are rendered as text, wrap and are never
 * cut off. The content ends with the ghost button Remove all access, which only reports the press: the dialog and the
 * revokes belong to the caller.
 *
 * @param props The subject, whether it is the signed-in member, the open state and the extra content.
 * @returns The list item.
 */
export function AccessSubjectRow({
  subject,
  isSelf,
  open,
  onOpenChange,
  onToggle,
  onRemoveAll,
  failedPermissions = noPermissions,
  forbiddenPermissions = noPermissions,
  pendingPermissions = noPermissions,
  assignablePermissions = noAssignable,
  children,
}: AccessSubjectRowProps) {
  const { t } = useTranslation();
  const warnNoView = needsViewWarning(subject.permissions);
  const badges = (
    <span className="access-row__badges">
      <StatusBadge tone="neutral">
        {subject.kind === "user"
          ? t("Server_Access_Subject_Member")
          : t("Server_Access_Subject_Role")}
      </StatusBadge>
      {isSelf && <StatusBadge tone="info">{t("Server_Access_You")}</StatusBadge>}
      {warnNoView && (
        <StatusBadge tone="warning">{t("Server_Access_Badge_CannotView")}</StatusBadge>
      )}
    </span>
  );
  const meta =
    subject.kind === "role" && subject.memberCount !== undefined
      ? t("Server_Access_Role_Members", { count: subject.memberCount })
      : subject.kind === "user" && subject.detail
        ? subject.detail
        : null;
  // A stored permission outside the assignable list cannot be revoked by this member, so the whole removal is blocked.
  const removalBlocked = subject.permissions.some(
    (permission) => !canAssign({ assignablePermissions }, permission),
  );
  const summary = (
    <>
      <HeldSummary permissions={subject.permissions} />
      {meta !== null && <span className="access-row__meta">{meta}</span>}
    </>
  );
  return (
    <li className="access-row">
      <Disclosure
        variant="inline"
        headingLevel={3}
        title={<bdi>{subject.name}</bdi>}
        badge={badges}
        summary={summary}
        open={open}
        onOpenChange={onOpenChange}
      >
        <div className="access-row__content">
          {subject.kind === "role" && subject.description && (
            <p className="access-row__description">{subject.description}</p>
          )}
          {warnNoView && (
            <StatePanel kind="warning">
              {t("Server_Access_NoView_Warning", { name: subject.name })}
            </StatePanel>
          )}
          {onToggle !== undefined && (
            <PermissionSwitches
              permissions={subject.permissions}
              failedPermissions={failedPermissions}
              forbiddenPermissions={forbiddenPermissions}
              pendingPermissions={pendingPermissions}
              assignablePermissions={assignablePermissions}
              onToggle={onToggle}
            />
          )}
          {onRemoveAll !== undefined && subject.permissions.length > 0 && (
            <div className="access-row__remove">
              <ActionButton
                variant="ghost"
                unavailableReason={
                  removalBlocked ? t("Server_Access_Remove_NotAssignable") : undefined
                }
                onClick={onRemoveAll}
              >
                {t("Server_Access_Remove")}
              </ActionButton>
            </div>
          )}
          {children}
        </div>
      </Disclosure>
    </li>
  );
}
