import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link, useNavigate } from "react-router";
import { formatResource } from "@/app/i18n";
import type { FullUser } from "@/api/types";
import { canAccessCommandCenter } from "@/auth/claims";
import { useAuth } from "@/auth/auth";
import { ConfirmDialog } from "@/components/primitives";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { ServerAction, ServerNotification } from "@/features/hosting/types";
import "@/components/ProfileMenu.css";

export const profileNotificationPollInterval = 15_000;
const allowedAvatarMimeTypes = new Set([
  "image/avif",
  "image/bmp",
  "image/gif",
  "image/jpeg",
  "image/png",
  "image/webp",
]);

export type ProfileMenuMode = "community" | "command";

interface PendingAction {
  notification: ServerNotification;
  action: ServerAction;
}

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
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const triggerRef = useRef<HTMLButtonElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const popoverRef = useRef<HTMLElement>(null);
  const [open, setOpen] = useState(false);
  const [pendingAction, setPendingAction] = useState<PendingAction>();

  const notifications = useQuery({
    queryKey: hostingQueryKeys.notifications,
    queryFn: ({ signal }) => hostingService.notifications(signal),
    enabled: Boolean(user),
    retry: false,
    refetchInterval: profileNotificationPollInterval,
    refetchIntervalInBackground: false,
  });

  const executeAction = useMutation({
    mutationFn: ({ notification, action }: PendingAction) =>
      hostingService.executeAction(notification.serverId, action.id),
    onSuccess: async (_, { notification }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: hostingQueryKeys.notifications }),
        queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet }),
        queryClient.invalidateQueries({ queryKey: hostingQueryKeys.server(notification.serverId) }),
      ]);
    },
  });

  useEffect(() => {
    if (!open) return;
    closeRef.current?.focus();
    const closeOnEscape = (event: globalThis.KeyboardEvent) => {
      if (event.key !== "Escape") return;
      setOpen(false);
      triggerRef.current?.focus();
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [open]);

  const attentionCount = (notifications.data ?? []).filter(
    (notification) => notification.possibleCost,
  ).length;
  const visibleNotifications = (notifications.data ?? []).slice(0, 5);
  const actionError = executeAction.isError;
  const busyServerId = executeAction.isPending
    ? executeAction.variables?.notification.serverId
    : null;

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

  const requestAction = (notification: ServerNotification, action: ServerAction) => {
    if (!notification.canOperate || !action.enabled || action.pending) return;
    executeAction.reset();
    if (action.confirmationChallenge) {
      close();
      navigate(
        `/command/servers/${encodeURIComponent(notification.serverId)}?action=${encodeURIComponent(action.id)}`,
      );
      return;
    }
    if (action.confirmationText) {
      setPendingAction({ notification, action });
      return;
    }
    executeAction.mutate({ notification, action });
  };

  const trapFocus = (event: KeyboardEvent<HTMLElement>) => {
    if (event.key !== "Tab") return;
    const focusable = Array.from(
      popoverRef.current?.querySelectorAll<HTMLElement>(
        'a[href], button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])',
      ) ?? [],
    );
    if (focusable.length === 0) return;
    const first = focusable[0]!;
    const last = focusable[focusable.length - 1]!;
    if (event.shiftKey && document.activeElement === first) {
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
      <button
        ref={triggerRef}
        className="account-profile__trigger"
        type="button"
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-controls="account-profile-panel"
        aria-label={formatResource(
          t("Components_UtfProfileMenu_TriggerLabel"),
          profile.displayName,
        )}
        onClick={() => {
          if (!open) executeAction.reset();
          setOpen((value) => !value);
        }}
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
      </button>

      {open && (
        <>
          <button
            className="account-profile__scrim"
            type="button"
            tabIndex={-1}
            aria-label={t("Components_UtfProfileMenu_CloseMenu")}
            onClick={() => close(true)}
          />
          <section
            ref={popoverRef}
            id="account-profile-panel"
            className="account-profile__popover"
            role="dialog"
            aria-label={t("Components_UtfProfileMenu_ProfileMenu")}
            onKeyDown={trapFocus}
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
              <button
                ref={closeRef}
                className="account-profile__close"
                type="button"
                aria-label={t("Components_UtfProfileMenu_CloseMenu")}
                onClick={() => close(true)}
              >
                &times;
              </button>
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
              {(notifications.isError || actionError) && (
                <p className="account-profile__notification-message" role="status">
                  {actionError
                    ? t("Hosting_Notification_ActionError")
                    : t("Hosting_Notification_LoadError")}
                </p>
              )}
              {!notifications.isError && !actionError && notifications.isLoading && (
                <p className="account-profile__notification-message" role="status">
                  {t("Common_LoadingData")}
                </p>
              )}
              {!notifications.isError &&
                !notifications.isLoading &&
                visibleNotifications.length === 0 && (
                  <p className="account-profile__notification-message">
                    {t("Hosting_Notification_Empty")}
                  </p>
                )}
              {visibleNotifications.length > 0 && (
                <>
                  <div className="account-profile__notification-list">
                    {visibleNotifications.map((notification) => {
                      const action = notification.costStopAction;
                      const actionDisabled =
                        !notification.canOperate ||
                        !action?.enabled ||
                        Boolean(action.pending) ||
                        busyServerId === notification.serverId;
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
                            <strong>{notification.title}</strong>
                            <span>{notification.statusLabel}</span>
                            {notification.possibleCost && (
                              <small>{t("Hosting_Notification_PossibleCost")}</small>
                            )}
                          </Link>
                          {action && (
                            <button
                              type="button"
                              disabled={actionDisabled}
                              title={action.unavailableReason ?? undefined}
                              onClick={() => requestAction(notification, action)}
                            >
                              {action.pending
                                ? t("Hosting_Notification_ActionPending")
                                : action.label}
                            </button>
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
                <Link
                  className="account-profile__button account-profile__button--primary"
                  to="/command"
                  onClick={() => close()}
                >
                  {t("Components_UtfProfileMenu_CommandCenter")}
                </Link>
              )}
              <Link
                className="account-profile__button account-profile__button--ghost"
                to="/settings"
                onClick={() => close()}
              >
                {t("Components_UtfProfileMenu_AccountSettings")}
              </Link>
              <button
                className="account-profile__button account-profile__button--danger"
                type="button"
                onClick={() => auth.logout()}
              >
                {t("Components_UtfProfileMenu_SignOut")}
              </button>
            </footer>
          </section>
        </>
      )}

      <ConfirmDialog
        open={Boolean(pendingAction)}
        title={pendingAction?.action.label ?? ""}
        description={pendingAction?.action.confirmationText ?? ""}
        confirmLabel={pendingAction?.action.label ?? ""}
        cancelLabel={t("Hosting_Common_Cancel")}
        onCancel={() => setPendingAction(undefined)}
        onConfirm={() => {
          if (pendingAction) executeAction.mutate(pendingAction);
          setPendingAction(undefined);
        }}
      />
    </div>
  );
}
