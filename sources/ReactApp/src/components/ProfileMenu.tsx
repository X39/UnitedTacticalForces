import { Dismiss20Regular } from "@fluentui/react-icons";
import { useQuery } from "@tanstack/react-query";
import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { formatResource } from "@/app/i18n";
import type { FullUser } from "@/api/types";
import { canAccessCommandCenter } from "@/auth/claims";
import { useAuth } from "@/auth/auth";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { CostAlertMenuItem } from "@/components/server/CostAlertMenuItem";
import { ServerStateSummary } from "@/components/server/ServerStateSummary";
import { UnavailableReason } from "@/components/server/UnavailableReason";
import {
  operatePermissionReason,
  operationPendingReason,
} from "@/components/server/unavailableReasonMapping";
import { useCostConfirmation } from "@/components/server/useCostConfirmation";
import { ActionLabel } from "@/features/hosting/components";
import { deriveServerState } from "@/features/hosting/serverState";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import { useCostAlertArrivalToasts, useCostAlerts } from "@/features/hosting/useCostAlerts";
import { useFleetQuery } from "@/features/hosting/useFleetQuery";
import "@/components/ProfileMenu.css";

export const profileNotificationPollInterval = 15_000;

/** The most cost alert rows the menu lists; the rest collapse into one line. */
const maxVisibleAlertRows = 3;
const allowedAvatarMimeTypes = new Set([
  "image/avif",
  "image/bmp",
  "image/gif",
  "image/jpeg",
  "image/png",
  "image/webp",
]);

export type ProfileMenuMode = "community" | "command";

export function avatarSource(user: FullUser): string | undefined {
  if (!user.avatar) return undefined;
  const mimeType = user.avatarMimeType?.split(";", 1)[0]?.trim().toLocaleLowerCase();
  const safeMimeType = mimeType && allowedAvatarMimeTypes.has(mimeType) ? mimeType : "image/png";
  return `data:${safeMimeType};base64,${user.avatar}`;
}

export function maskAccountIdentifier(value: string): string {
  return value.length <= 6 ? value : `${value.slice(0, 4)}...${value.slice(-4)}`;
}

function claimDisplayName(user: FullUser): string[] {
  return Array.from(
    new Set(
      user.claims
        .map((claim) => claim.title?.trim() || claim.identifier?.trim() || claim.category?.trim())
        .filter((value): value is string => Boolean(value)),
    ),
  );
}

function summarize(values: string[], emptyText: string, additionalTemplate: string): string {
  if (values.length === 0) return emptyText;
  const visible = values.slice(0, 3);
  const summary = visible.join(", ");
  const remaining = values.length - visible.length;
  return remaining > 0 ? formatResource(additionalTemplate, summary, remaining) : summary;
}

function countSummary(count: number, zero: string, one: string, many: string): string {
  if (count === 0) return zero;
  if (count === 1) return one;
  return formatResource(many, count);
}

function initials(displayName: string): string {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "U";
  return parts
    .slice(0, 2)
    .map((part) => part[0]?.toLocaleUpperCase())
    .join("");
}

function formatAttentionCount(count: number): string {
  return count > 99 ? "99+" : String(count);
}

export function ProfileMenu({ mode = "community" }: { mode?: ProfileMenuMode }) {
  const { t } = useTranslation();
  const auth = useAuth();
  const user = auth.user;
  const { request } = useCostConfirmation();
  const triggerRef = useRef<HTMLButtonElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const popoverRef = useRef<HTMLElement>(null);
  const [open, setOpen] = useState(false);
  const attentionDescriptionId = useId();

  const notifications = useQuery({
    queryKey: hostingQueryKeys.notifications,
    queryFn: ({ signal }) => hostingService.notifications(signal),
    enabled: Boolean(user),
    retry: false,
    refetchInterval: profileNotificationPollInterval,
    refetchIntervalInBackground: false,
  });

  // Cost alerts reach the menu as socket frames written into the alerts cache; this hook never polls.
  const costAlerts = useCostAlerts();
  useCostAlertArrivalToasts();

  // The fleet is only needed to describe the notified servers, so it is not loaded for an empty list.
  const fleet = useFleetQuery(
    Boolean(user) && ((notifications.data?.length ?? 0) > 0 || costAlerts.alerts.length > 0),
  );
  const serversById = useMemo(
    () => new Map((fleet.data ?? []).map((server) => [server.primaryKey, server])),
    [fleet.data],
  );

  useEffect(() => {
    if (!open) return;
    closeRef.current?.focus();
    const closeOnEscape = (event: globalThis.KeyboardEvent) => {
      if (event.key !== "Escape") return;
      // Marks this Escape as handled so an open shell navigation (its listener runs later, on the window) stays open
      // and closes on the next Escape. This is safe because the popover closes itself before it requests a cost
      // confirmation, so no dialog can be open while this listener exists.
      event.preventDefault();
      setOpen(false);
      triggerRef.current?.focus();
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [open]);

  // The badge counts distinct servers: one that may still cost and has an alert counts once.
  const attentionCount = new Set([
    ...(notifications.data ?? [])
      .filter((notification) => notification.possibleCost)
      .map((notification) => notification.serverId),
    ...costAlerts.alerts.map((alert) => alert.serverId),
  ]).size;
  const visibleNotifications = (notifications.data ?? []).slice(0, 5);
  const visibleAlerts = costAlerts.alerts.slice(0, maxVisibleAlertRows);
  const hiddenAlertCount = costAlerts.alerts.length - visibleAlerts.length;
  const hasRows = visibleAlerts.length > 0 || visibleNotifications.length > 0;

  // After a dismissal the alert row is removed by the alerts cache. Focus then moves to the first control of the
  // row that followed it, or to the close button when none did, once the removal has rendered.
  const [alertFocus, setAlertFocus] = useState<{ removedId: string; nextId: string | null } | null>(
    null,
  );
  const handledAlertFocus = useRef<typeof alertFocus>(null);
  useEffect(() => {
    if (!alertFocus || handledAlertFocus.current === alertFocus) return;
    if (costAlerts.alerts.some((alert) => alert.id === alertFocus.removedId)) return;
    handledAlertFocus.current = alertFocus;
    const nextRow =
      alertFocus.nextId === null
        ? undefined
        : Array.from(
            popoverRef.current?.querySelectorAll<HTMLElement>(
              "[data-cost-alert-id], [data-auto-stop-alert-id]",
            ) ?? [],
          ).find(
            (row) => (row.dataset.costAlertId ?? row.dataset.autoStopAlertId) === alertFocus.nextId,
          );
    const target = nextRow?.querySelector<HTMLElement>("a[href], button");
    (target ?? closeRef.current)?.focus();
  }, [alertFocus, costAlerts.alerts]);

  const profile = useMemo(() => {
    if (!user) return undefined;
    const commandAccess = canAccessCommandCenter(user);
    const displayName = user.nickname?.trim() || t("Components_UtfProfileMenu_DefaultMemberName");
    const roleTitles = user.roles
      .map((role) => role.title?.trim())
      .filter((title): title is string => Boolean(title));
    const claimTitles = claimDisplayName(user);
    const steamLinked = Boolean(user.steamId64?.trim());
    const discordLinked = Boolean(user.discordId?.trim() || user.discordUsername?.trim());
    return {
      commandAccess,
      displayName,
      avatar: avatarSource(user),
      initials: initials(displayName),
      verification: user.isVerified
        ? t("Components_UtfProfileMenu_VerificationVerified")
        : t("Components_UtfProfileMenu_VerificationUnverified"),
      primaryRole:
        roleTitles[0] ??
        (commandAccess
          ? t("Components_UtfProfileMenu_PrimaryRoleSummary_Command")
          : t("Components_UtfProfileMenu_PrimaryRoleSummary_Member")),
      roleSummary: countSummary(
        user.roles.length,
        t("Components_UtfProfileMenu_RoleSummary_Zero"),
        t("Components_UtfProfileMenu_RoleSummary_One"),
        t("Components_UtfProfileMenu_RoleSummary_Many_Count"),
      ),
      roleDetail: summarize(
        roleTitles,
        t("Components_UtfProfileMenu_NoRoleAssignments"),
        t("Components_UtfProfileMenu_AdditionalItemsCount"),
      ),
      claimSummary: countSummary(
        user.claims.length,
        t("Components_UtfProfileMenu_ClaimSummary_Zero"),
        t("Components_UtfProfileMenu_ClaimSummary_One"),
        t("Components_UtfProfileMenu_ClaimSummary_Many_Count"),
      ),
      claimDetail: summarize(
        claimTitles,
        t("Components_UtfProfileMenu_NoClaimAssignments"),
        t("Components_UtfProfileMenu_AdditionalItemsCount"),
      ),
      linkedCount: Number(steamLinked) + Number(discordLinked),
      steamLinked,
      steamSummary: steamLinked
        ? maskAccountIdentifier(user.steamId64!)
        : t("Components_UtfProfileMenu_LinkStatus_NotLinked"),
      discordLinked,
      discordSummary:
        user.discordUsername?.trim() ||
        (discordLinked
          ? t("Components_UtfProfileMenu_DiscordLinkedTitle")
          : t("Components_UtfProfileMenu_LinkStatus_NotLinked")),
    };
  }, [t, user]);

  if (!user || !profile) return null;

  const close = (restoreFocus = false) => {
    setOpen(false);
    if (restoreFocus) requestAnimationFrame(() => triggerRef.current?.focus());
  };

  const moveFocusAfterDismissal = (alertId: string) => {
    const index = costAlerts.alerts.findIndex((alert) => alert.id === alertId);
    setAlertFocus({
      removedId: alertId,
      nextId: index < 0 ? null : (costAlerts.alerts[index + 1]?.id ?? null),
    });
  };

  const moveFocus = (event: KeyboardEvent<HTMLElement>) => {
    const isArrow = event.key === "ArrowDown" || event.key === "ArrowUp";
    if (event.key !== "Tab" && !isArrow) return;
    const focusable = Array.from(
      popoverRef.current?.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])',
      ) ?? [],
    );
    if (focusable.length === 0) return;
    const first = focusable[0]!;
    const last = focusable[focusable.length - 1]!;
    if (isArrow) {
      // Arrow keys walk the same rows as Tab and wrap at both ends, so the popover never needs the mouse.
      event.preventDefault();
      const current = focusable.indexOf(document.activeElement as HTMLElement);
      const step = event.key === "ArrowDown" ? 1 : -1;
      const next = current < 0 ? (step > 0 ? 0 : focusable.length - 1) : current + step;
      focusable[(next + focusable.length) % focusable.length]!.focus();
    } else if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  const shellBadge =
    mode === "command"
      ? t("Components_UtfProfileMenu_ShellBadge_CommandCenter")
      : profile.commandAccess
        ? t("Components_UtfProfileMenu_ShellBadge_CommandAccess")
        : t("Components_UtfProfileMenu_ShellBadge_Community");

  return (
    <div className={`account-profile account-profile--${mode}`}>
      <ActionButton
        ref={triggerRef}
        className="account-profile__trigger"
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-controls="account-profile-panel"
        aria-describedby={attentionCount > 0 ? attentionDescriptionId : undefined}
        aria-label={formatResource(
          t("Components_UtfProfileMenu_TriggerLabel"),
          profile.displayName,
        )}
        onClick={() => setOpen((value) => !value)}
      >
        <span className="account-profile__avatar" aria-hidden="true">
          {profile.avatar ? <img src={profile.avatar} alt="" /> : <span>{profile.initials}</span>}
          {attentionCount > 0 && (
            <span className="account-profile__attention-badge">
              {formatAttentionCount(attentionCount)}
            </span>
          )}
        </span>
        <span className="account-profile__trigger-copy">
          <strong>{profile.displayName}</strong>
          <span>{profile.verification}</span>
        </span>
      </ActionButton>
      {attentionCount > 0 && (
        <span id={attentionDescriptionId} className="sr-only">
          {t("Server_Alerts_Menu_Badge", { count: attentionCount })}
        </span>
      )}

      {open && (
        <>
          <ActionButton
            className="account-profile__scrim"
            tabIndex={-1}
            aria-label={t("Components_UtfProfileMenu_CloseMenu")}
            onClick={() => close(true)}
          >
            {null}
          </ActionButton>
          <section
            ref={popoverRef}
            id="account-profile-panel"
            className="account-profile__popover"
            role="dialog"
            aria-label={t("Components_UtfProfileMenu_ProfileMenu")}
            onKeyDown={moveFocus}
          >
            <header className="account-profile__hero">
              <div className="account-profile__identity">
                <span className="account-profile__kicker">
                  {mode === "command"
                    ? t("Components_UtfProfileMenu_IdentityKicker_CommandCenter")
                    : t("Components_UtfProfileMenu_IdentityKicker_Member")}
                </span>
                <h2>{profile.displayName}</h2>
                <p>{profile.primaryRole}</p>
                <div
                  className="account-profile__badges"
                  aria-label={t("Components_UtfProfileMenu_AccountStatusLabel")}
                >
                  <span
                    className={`account-profile__badge account-profile__badge--${user.isVerified ? "success" : "warning"}`}
                  >
                    <span /> {profile.verification}
                  </span>
                  <span className="account-profile__badge account-profile__badge--gold">
                    <span /> {shellBadge}
                  </span>
                </div>
              </div>
              <span
                className="account-profile__avatar account-profile__avatar--large"
                aria-hidden="true"
              >
                {profile.avatar ? (
                  <img src={profile.avatar} alt="" />
                ) : (
                  <span>{profile.initials}</span>
                )}
              </span>
              <ActionButton
                ref={closeRef}
                iconOnly
                variant="ghost"
                size="compact"
                className="account-profile__close"
                aria-label={t("Components_UtfProfileMenu_CloseMenu")}
                onClick={() => close(true)}
              >
                <Dismiss20Regular aria-hidden="true" />
              </ActionButton>
            </header>

            <div
              className="account-profile__metrics"
              aria-label={t("Components_UtfProfileMenu_AccountAccessSummary")}
            >
              <div>
                <strong>{user.roles.length}</strong>
                <span>{t("Components_UtfProfileMenu_RolesLabel")}</span>
              </div>
              <div>
                <strong>{user.claims.length}</strong>
                <span>{t("Components_UtfProfileMenu_ClaimsLabel")}</span>
              </div>
              <div>
                <strong>{profile.linkedCount}</strong>
                <span>{t("Components_UtfProfileMenu_LinkedLabel")}</span>
              </div>
            </div>

            <section className="account-profile__section account-profile__section--notifications">
              <div className="account-profile__notification-heading">
                <div className="account-profile__section-title">
                  {t("Hosting_Notification_Title")}
                </div>
                {attentionCount > 0 && <span>{attentionCount}</span>}
              </div>
              {notifications.isError && (
                <p className="account-profile__notification-message" role="status">
                  {t("Hosting_Notification_LoadError")}
                </p>
              )}
              {!notifications.isError && notifications.isLoading && (
                <p className="account-profile__notification-message" role="status">
                  {t("Common_LoadingData")}
                </p>
              )}
              {!notifications.isError &&
                !notifications.isLoading &&
                !costAlerts.loading &&
                !hasRows && (
                  <p className="account-profile__notification-message">
                    {t("Hosting_Notification_Empty")}
                  </p>
                )}
              {hasRows && (
                <>
                  <div className="account-profile__notification-list">
                    {visibleAlerts.map((alert) => (
                      <CostAlertMenuItem
                        key={alert.id}
                        alert={alert}
                        server={serversById.get(alert.serverId)}
                        onNavigate={() => close()}
                        returnFocus={triggerRef}
                        onDismissed={moveFocusAfterDismissal}
                      />
                    ))}
                    {hiddenAlertCount > 0 && (
                      <p className="account-profile__notification-message">
                        {t("Server_Alerts_Menu_More", { count: hiddenAlertCount })}
                      </p>
                    )}
                    {visibleNotifications.map((notification) => {
                      const action = notification.costStopAction;
                      const derived = deriveServerState(serversById.get(notification.serverId), {
                        notification,
                      });
                      const unavailable =
                        !notification.canOperate || !action?.enabled || Boolean(action.pending);
                      const reason = !notification.canOperate
                        ? operatePermissionReason
                        : action?.unavailableReason ||
                          (action?.pending ? operationPendingReason : null);
                      return (
                        <article
                          key={notification.serverId}
                          className={
                            notification.possibleCost
                              ? "account-profile__notification account-profile__notification--attention"
                              : "account-profile__notification"
                          }
                        >
                          <Link
                            className="account-profile__notification-body"
                            to={`/command/servers/${encodeURIComponent(notification.serverId)}`}
                            onClick={() => close()}
                          >
                            <strong>
                              <bdi>{notification.title}</bdi>
                            </strong>
                          </Link>
                          <ServerStateSummary derived={derived} />
                          {action && (
                            <ActionButton
                              variant="ghost"
                              size="compact"
                              unavailableReason={
                                unavailable ? (
                                  <UnavailableReason
                                    reason={reason}
                                    stateLabel={t(derived.labelKey)}
                                  />
                                ) : undefined
                              }
                              onClick={() => {
                                close();
                                request({
                                  serverId: notification.serverId,
                                  actionId: action.id,
                                  source: "notification",
                                  returnFocus: triggerRef,
                                });
                              }}
                            >
                              {action.pending ? (
                                t("Server_Action_InProgress")
                              ) : (
                                <ActionLabel action={action} />
                              )}
                            </ActionButton>
                          )}
                        </article>
                      );
                    })}
                  </div>
                  <Link
                    className="account-profile__notification-link"
                    to="/command/servers"
                    onClick={() => close()}
                  >
                    {t("Hosting_Notification_ViewServers")}
                  </Link>
                </>
              )}
            </section>

            <section className="account-profile__section">
              <div className="account-profile__section-title">
                {t("Components_UtfProfileMenu_Authority")}
              </div>
              <div className="account-profile__detail-row">
                <span>
                  <strong>{profile.roleSummary}</strong>
                  <small>{profile.roleDetail}</small>
                </span>
                <b>
                  {profile.commandAccess
                    ? t("Components_UtfProfileMenu_BadgeOperator")
                    : t("Components_UtfProfileMenu_BadgeMember")}
                </b>
              </div>
              <div className="account-profile__detail-row">
                <span>
                  <strong>{profile.claimSummary}</strong>
                  <small>{profile.claimDetail}</small>
                </span>
                <b>
                  {user.claims.length > 0
                    ? t("Components_UtfProfileMenu_BadgeClaims")
                    : t("Components_UtfProfileMenu_BadgeNone")}
                </b>
              </div>
            </section>

            <section className="account-profile__section">
              <div className="account-profile__section-title">
                {t("Components_UtfProfileMenu_LinkedAccounts")}
              </div>
              <div className="account-profile__detail-row">
                <span>
                  <strong>{t("Components_UtfProfileMenu_SteamProvider")}</strong>
                  <small>{profile.steamSummary}</small>
                </span>
                <b className={profile.steamLinked ? "is-linked" : "is-missing"}>
                  {t(
                    profile.steamLinked
                      ? "Components_UtfProfileMenu_LinkBadge_Linked"
                      : "Components_UtfProfileMenu_LinkBadge_Missing",
                  )}
                </b>
              </div>
              <div className="account-profile__detail-row">
                <span>
                  <strong>{t("Components_UtfProfileMenu_DiscordProvider")}</strong>
                  <small>{profile.discordSummary}</small>
                </span>
                <b className={profile.discordLinked ? "is-linked" : "is-missing"}>
                  {t(
                    profile.discordLinked
                      ? "Components_UtfProfileMenu_LinkBadge_Linked"
                      : "Components_UtfProfileMenu_LinkBadge_Missing",
                  )}
                </b>
              </div>
            </section>

            <footer className="account-profile__actions">
              {profile.commandAccess && (
                <ActionLink variant="gold" fullWidth to="/command" onClick={() => close()}>
                  {t("Components_UtfProfileMenu_CommandCenter")}
                </ActionLink>
              )}
              <ActionLink variant="ghost" fullWidth to="/settings" onClick={() => close()}>
                {t("Components_UtfProfileMenu_AccountSettings")}
              </ActionLink>
              <ActionButton
                variant="danger"
                fullWidth
                className="account-profile__sign-out"
                onClick={() => auth.logout()}
              >
                {t("Components_UtfProfileMenu_SignOut")}
              </ActionButton>
            </footer>
          </section>
        </>
      )}
    </div>
  );
}
