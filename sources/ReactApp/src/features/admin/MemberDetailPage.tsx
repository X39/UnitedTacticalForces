import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatDate } from "@/app/format";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import {
  ConfirmDialog,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { setRoleMember } from "@/features/admin/roles";
import {
  loadMemberDetail,
  memberAvatarSource,
  memberInitials,
  memberPermissions,
  memberStatus,
  setMemberClaim,
  updateMemberProfile,
  upsertMembership,
  type MemberClaim,
  type MemberRole,
  type VereinMembershipStatus,
} from "@/features/admin/members";

interface ProfileDraft {
  userId: string;
  nickname?: string;
  email?: string;
  verified?: boolean;
  banned?: boolean;
}

interface MembershipDraft {
  userId: string;
  status?: VereinMembershipStatus;
  memberNumber?: string;
  notes?: string;
  reason?: string;
}

type AuthorityCommand =
  { kind: "role"; id: number; active: boolean } | { kind: "claim"; id: number; active: boolean };

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function isValidMemberId(value: string | undefined): value is string {
  return Boolean(value && guidPattern.test(value));
}

function roleTitle(role: MemberRole, fallback: string): string {
  return role.title?.trim() || formatResource(fallback, role.primaryKey ?? "?");
}

function claimTitle(claim: MemberClaim, fallback: string): string {
  return (
    claim.title?.trim() ||
    claim.identifier?.trim() ||
    formatResource(fallback, claim.primaryKey ?? "?")
  );
}

function claimIdentifier(claim: MemberClaim, fallback: string): string {
  const identifier = claim.identifier?.trim() || fallback;
  return claim.value?.trim() ? `${identifier} = ${claim.value}` : identifier;
}

function matchesRole(role: MemberRole, search: string): boolean {
  const query = search.trim().toLocaleLowerCase();
  return (
    !query ||
    `${role.title ?? ""} ${role.description ?? ""} ${role.primaryKey ?? ""}`
      .toLocaleLowerCase()
      .includes(query)
  );
}

function matchesClaim(claim: MemberClaim, search: string): boolean {
  const query = search.trim().toLocaleLowerCase();
  return (
    !query ||
    `${claim.title ?? ""} ${claim.category ?? ""} ${claim.identifier ?? ""} ${claim.value ?? ""}`
      .toLocaleLowerCase()
      .includes(query)
  );
}

function statusKey(status: ReturnType<typeof memberStatus>): TranslationKey {
  switch (status) {
    case "deleted":
      return "Pages_Command_Members_StatusDeleted";
    case "banned":
      return "Pages_Command_Members_StatusBanned";
    case "verified":
      return "Pages_Command_Members_StatusVerified";
    default:
      return "Pages_Command_Members_StatusUnverified";
  }
}

function statusTone(status: ReturnType<typeof memberStatus>) {
  return status === "verified"
    ? ("success" as const)
    : status === "unverified"
      ? ("warning" as const)
      : ("danger" as const);
}

function membershipStatusKey(status: VereinMembershipStatus | undefined): TranslationKey {
  switch (status) {
    case "Active":
      return "Pages_Command_Members_StatusActive";
    case "Suspended":
      return "Pages_Command_Members_StatusSuspended";
    case "Ended":
      return "Pages_Command_Members_StatusEnded";
    default:
      return "Pages_Command_Members_NoFormalMembership";
  }
}

function membershipTone(status: VereinMembershipStatus | undefined) {
  return status === "Active"
    ? ("success" as const)
    : status === "Suspended"
      ? ("warning" as const)
      : status === "Ended"
        ? ("neutral" as const)
        : ("info" as const);
}

export function Component() {
  const { t } = useTranslation();
  const { user: currentUser } = useAuth();
  const { userId: parameter } = useParams();
  const userId = isValidMemberId(parameter) ? parameter : undefined;
  const permissions = memberPermissions(currentUser);
  const canOpen =
    permissions.profile || permissions.membership || permissions.authority || permissions.roster;
  const queryClient = useQueryClient();
  const [profileState, setProfileState] = useState<ProfileDraft>();
  const [membershipState, setMembershipState] = useState<MembershipDraft>();
  const [roleSearch, setRoleSearch] = useState("");
  const [claimSearch, setClaimSearch] = useState("");
  const [confirmation, setConfirmation] = useState<"ban" | "end-membership">();

  const query = useQuery({
    queryKey: ["command-center", "member", userId],
    queryFn: ({ signal }) => loadMemberDetail(userId!, permissions, signal),
    enabled: Boolean(userId && canOpen),
  });
  const detail = query.data;
  const user = detail?.user;
  const membership = detail?.membership;
  const profileDraft = profileState?.userId === userId ? profileState : undefined;
  const membershipDraft = membershipState?.userId === userId ? membershipState : undefined;
  const nickname = profileDraft?.nickname ?? user?.nickname ?? "";
  const email = profileDraft?.email ?? user?.eMail ?? "";
  const verified = profileDraft?.verified ?? user?.isVerified ?? false;
  const banned = profileDraft?.banned ?? user?.isBanned ?? false;
  const currentMembershipStatus = membershipDraft?.status ?? membership?.status ?? "Active";
  const memberNumber = membershipDraft?.memberNumber ?? membership?.memberNumber ?? "";
  const membershipNotes = membershipDraft?.notes ?? membership?.notes ?? "";
  const membershipReason = membershipDraft?.reason ?? "";
  const profileDirty = Boolean(
    user &&
    ((permissions.nickname && nickname !== (user.nickname ?? "")) ||
      (permissions.email && email !== (user.eMail ?? "")) ||
      (permissions.verify && verified !== Boolean(user.isVerified)) ||
      (permissions.ban && banned !== Boolean(user.isBanned))),
  );
  const membershipDirty = Boolean(
    membershipDraft &&
    (currentMembershipStatus !== (membership?.status ?? "Active") ||
      memberNumber !== (membership?.memberNumber ?? "") ||
      membershipNotes !== (membership?.notes ?? "") ||
      Boolean(membershipReason.trim())),
  );
  useUnsavedChanges(profileDirty || membershipDirty);
  useDocumentTitle(
    user?.nickname
      ? formatResource(t("Pages_Command_Member_PageTitleNamed"), user.nickname)
      : t("Pages_Command_Member_PageTitle"),
  );

  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["command-center", "member", userId] }),
      queryClient.invalidateQueries({ queryKey: ["command-center", "community-snapshot"] }),
      queryClient.invalidateQueries({ queryKey: ["command-center", "authority-snapshot"] }),
      queryClient.invalidateQueries({ queryKey: ["command-center", "authority-role-members"] }),
    ]);
  };
  const profileMutation = useMutation({
    mutationFn: async () => {
      if (!userId) return;
      await updateMemberProfile(userId, {
        ...(permissions.nickname ? { nickname: nickname.trim() } : {}),
        ...(permissions.email ? { eMail: email.trim() } : {}),
        ...(permissions.verify ? { isVerified: verified } : {}),
        ...(permissions.ban ? { isBanned: banned } : {}),
      });
    },
    onSuccess: async () => {
      setProfileState(undefined);
      await invalidate();
    },
  });
  const membershipMutation = useMutation({
    mutationFn: async () => {
      if (!userId) return;
      await upsertMembership(userId, {
        memberNumber: memberNumber.trim() || null,
        status: currentMembershipStatus,
        timeStampStarted: membership?.timeStampStarted ?? new Date().toISOString(),
        timeStampEnded:
          currentMembershipStatus === "Ended"
            ? (membership?.timeStampEnded ?? new Date().toISOString())
            : null,
        notes: membershipNotes.trim(),
        reason: membershipReason.trim(),
      });
    },
    onSuccess: async () => {
      setMembershipState(undefined);
      await invalidate();
    },
  });
  const authorityMutation = useMutation({
    mutationFn: async (command: AuthorityCommand) => {
      if (!userId) return;
      if (command.kind === "role") await setRoleMember(command.id, userId, command.active);
      else await setMemberClaim(userId, command.id, command.active);
    },
    onSuccess: invalidate,
  });

  const setProfile = (change: Omit<Partial<ProfileDraft>, "userId">) =>
    userId &&
    setProfileState((current) => ({
      ...(current?.userId === userId ? current : {}),
      userId,
      ...change,
    }));
  const setMembership = (change: Omit<Partial<MembershipDraft>, "userId">) =>
    userId &&
    setMembershipState((current) => ({
      ...(current?.userId === userId ? current : {}),
      userId,
      ...change,
    }));
  const requestProfileSave = () => {
    if (banned && !user?.isBanned) setConfirmation("ban");
    else profileMutation.mutate();
  };
  const requestMembershipSave = () => {
    if (currentMembershipStatus === "Ended" && membership?.status !== "Ended")
      setConfirmation("end-membership");
    else membershipMutation.mutate();
  };

  const assignedRoleIds = useMemo(
    () => new Set(detail?.assignedRoles.flatMap((role) => role.primaryKey ?? []) ?? []),
    [detail?.assignedRoles],
  );
  const assignedClaimIds = useMemo(
    () => new Set(detail?.assignedClaims.flatMap((claim) => claim.primaryKey ?? []) ?? []),
    [detail?.assignedClaims],
  );
  const assignedRoles = useMemo(
    () =>
      (detail?.assignedRoles ?? [])
        .filter((role) => matchesRole(role, roleSearch))
        .sort((a, b) => (a.title ?? "").localeCompare(b.title ?? "")),
    [detail?.assignedRoles, roleSearch],
  );
  const availableRoles = useMemo(
    () =>
      (detail?.authority?.roles ?? [])
        .filter(
          (entry) => entry.role?.primaryKey != null && !assignedRoleIds.has(entry.role.primaryKey),
        )
        .filter((entry) => entry.role && matchesRole(entry.role, roleSearch))
        .sort((a, b) => (a.role?.title ?? "").localeCompare(b.role?.title ?? "")),
    [assignedRoleIds, detail?.authority?.roles, roleSearch],
  );
  const assignedClaims = useMemo(
    () =>
      (detail?.assignedClaims ?? [])
        .filter((claim) => matchesClaim(claim, claimSearch))
        .sort((a, b) =>
          `${a.category ?? ""}${a.title ?? ""}`.localeCompare(
            `${b.category ?? ""}${b.title ?? ""}`,
          ),
        ),
    [claimSearch, detail?.assignedClaims],
  );
  const availableClaims = useMemo(
    () =>
      (detail?.authority?.claims ?? [])
        .map((entry) => entry.claim)
        .filter(
          (claim) =>
            claim.primaryKey != null && !claim.isPrefix && !assignedClaimIds.has(claim.primaryKey),
        )
        .filter((claim) => matchesClaim(claim, claimSearch))
        .sort((a, b) =>
          `${a.category ?? ""}${a.title ?? ""}`.localeCompare(
            `${b.category ?? ""}${b.title ?? ""}`,
          ),
        ),
    [assignedClaimIds, claimSearch, detail?.authority?.claims],
  );

  if (!canOpen)
    return (
      <StatePanel kind="forbidden" title={t("Pages_Command_Members_NoAccessTitle")}>
        {t("Pages_Command_Members_NoAccessMessage")}
      </StatePanel>
    );
  if (!userId)
    return (
      <StatePanel title={t("Pages_Command_Member_NotFoundTitle")}>
        {t("Pages_Command_Member_NotFoundMessage")}
      </StatePanel>
    );

  const accountStatus = user ? memberStatus(user) : "unverified";
  const avatar = user ? memberAvatarSource(user) : undefined;
  const displayName = user?.nickname?.trim() || t("Pages_Command_Member_UnknownUser");
  const membershipLabel = t(membershipStatusKey(membership?.status));
  const error =
    profileMutation.error ?? membershipMutation.error ?? authorityMutation.error ?? query.error;

  return (
    <section className="member-detail-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_Members")}
        title={
          user
            ? formatResource(t("Pages_Command_Member_HeaderTitleNamed"), displayName)
            : t("Pages_Command_Member_HeaderTitle")
        }
        description={t("Pages_Command_Member_HeaderDescription")}
        actions={
          <>
            <Link className="button button--ghost" to="/command/members">
              {t("Pages_Command_Member_BackToRoster")}
            </Link>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_Member_Refresh")}
            </button>
          </>
        }
      />
      {error && (
        <StatePanel kind="error">
          {formatResource(
            t(query.isError ? "Pages_Command_Member_LoadError" : "Pages_Command_Member_SaveError"),
            error.message,
          )}
        </StatePanel>
      )}
      {profileMutation.isSuccess && (
        <StatePanel kind="success">{t("Pages_Command_Member_ProfileSaved")}</StatePanel>
      )}
      {membershipMutation.isSuccess && (
        <StatePanel kind="success">{t("Pages_Command_Member_MembershipSaved")}</StatePanel>
      )}
      {authorityMutation.isSuccess && (
        <StatePanel kind="success">{t("Pages_Command_Member_AuthoritySaved")}</StatePanel>
      )}
      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_Member_Loading")}</StatePanel>
      ) : !detail || !user ? (
        <StatePanel title={t("Pages_Command_Member_NotFoundTitle")}>
          {t("Pages_Command_Member_NotFoundMessage")}
        </StatePanel>
      ) : (
        <>
          <section
            className="command-panel member-detail-identity"
            aria-label={t("Pages_Command_Member_MetricsAria")}
          >
            {avatar ? (
              <img src={avatar} alt="" width="84" height="84" />
            ) : (
              <span className="member-detail-avatar" aria-hidden="true">
                {memberInitials(displayName)}
              </span>
            )}
            <div className="member-detail-identity__name">
              <span className="kicker">{t("Pages_Command_Member_ProfileKicker")}</span>
              <h2>{displayName}</h2>
              <small>{userId}</small>
            </div>
            <dl className="member-detail-facts">
              <div>
                <dt>{t("Pages_Command_Member_StatusAccount")}</dt>
                <dd>
                  <StatusBadge tone={statusTone(accountStatus)}>
                    {t(statusKey(accountStatus))}
                  </StatusBadge>
                </dd>
              </div>
              <div>
                <dt>{t("Pages_Command_Member_FieldEmail")}</dt>
                <dd>{user.eMail || t("Pages_Command_Member_NoEmail")}</dd>
              </div>
              <div>
                <dt>{t("Pages_Command_Member_StatusVerein")}</dt>
                <dd>
                  <StatusBadge tone={membershipTone(membership?.status)}>
                    {membershipLabel}
                  </StatusBadge>
                </dd>
              </div>
              <div>
                <dt>{t("Pages_Command_Member_StatusAuthority")}</dt>
                <dd>
                  {formatResource(
                    t("Pages_Command_Member_MetricRolesValue"),
                    detail.assignedRoles.length,
                  )}{" "}
                  /{" "}
                  {formatResource(
                    t("Pages_Command_Member_MetricAuthorityDescription"),
                    detail.assignedClaims.length,
                  )}
                </dd>
              </div>
            </dl>
          </section>

          <div className="member-detail-edit-grid">
            <section className="command-panel member-detail-panel">
              <header className="command-panel__header">
                <span className="kicker">{t("Pages_Command_Member_ProfileKicker")}</span>
                <h2>{t("Pages_Command_Member_ProfileTitle")}</h2>
                <StatusBadge tone={statusTone(accountStatus)}>
                  {t(statusKey(accountStatus))}
                </StatusBadge>
              </header>
              <dl className="command-detail-list member-detail-meta">
                <dt>{t("Pages_Command_Member_FieldIdentifier")}</dt>
                <dd>{userId}</dd>
                <dt>{t("Pages_Command_Member_FieldSteam")}</dt>
                <dd>{user.steamId64 || t("Pages_Command_Member_NotSpecified")}</dd>
                <dt>{t("Pages_Command_Member_FieldDiscord")}</dt>
                <dd>
                  {user.discordUsername || user.discordId || t("Pages_Command_Member_NotSpecified")}
                </dd>
                <dt>{t("Pages_Command_Member_FieldDeleted")}</dt>
                <dd>
                  {t(user.isDeleted ? "Pages_Command_Member_Yes" : "Pages_Command_Member_No")}
                </dd>
              </dl>
              {!permissions.profile ? (
                <StatePanel kind="forbidden">
                  {t("Pages_Command_Members_NoAccessMessage")}
                </StatePanel>
              ) : (
                <form
                  className="member-detail-form"
                  onSubmit={(event) => {
                    event.preventDefault();
                    requestProfileSave();
                  }}
                >
                  <label>
                    <span>{t("Pages_Command_Member_FieldNickname")}</span>
                    <input
                      required
                      maxLength={1024}
                      disabled={!permissions.nickname}
                      value={nickname}
                      onChange={(event) => setProfile({ nickname: event.target.value })}
                    />
                  </label>
                  <label>
                    <span>{t("Pages_Command_Member_FieldEmail")}</span>
                    <input
                      type="email"
                      maxLength={1024}
                      disabled={!permissions.email}
                      value={email}
                      onChange={(event) => setProfile({ email: event.target.value })}
                    />
                  </label>
                  <div className="member-detail-toggle-grid">
                    <label>
                      <input
                        type="checkbox"
                        disabled={!permissions.verify}
                        checked={verified}
                        onChange={(event) => setProfile({ verified: event.target.checked })}
                      />
                      <span>{t("Pages_Command_Member_FieldVerified")}</span>
                    </label>
                    <label>
                      <input
                        type="checkbox"
                        disabled={!permissions.ban}
                        checked={banned}
                        onChange={(event) => setProfile({ banned: event.target.checked })}
                      />
                      <span>{t("Pages_Command_Member_FieldBanned")}</span>
                    </label>
                  </div>
                  <div className="form-actions">
                    <button
                      className="button button--primary"
                      type="submit"
                      disabled={!profileDirty || profileMutation.isPending || !nickname.trim()}
                    >
                      {t(
                        profileMutation.isPending
                          ? "Pages_Command_Member_Saving"
                          : "Pages_Command_Member_SaveProfile",
                      )}
                    </button>
                    <button
                      className="button button--ghost"
                      type="button"
                      disabled={!profileDirty || profileMutation.isPending}
                      onClick={() => setProfileState(undefined)}
                    >
                      {t("Common_Cancel")}
                    </button>
                  </div>
                </form>
              )}
            </section>

            <section className="command-panel member-detail-panel">
              <header className="command-panel__header">
                <span className="kicker">{t("Pages_Command_Member_VereinKicker")}</span>
                <h2>{t("Pages_Command_Member_VereinTitle")}</h2>
                <StatusBadge tone={membershipTone(membership?.status)}>
                  {membershipLabel}
                </StatusBadge>
              </header>
              {!permissions.membership ? (
                <StatePanel kind="forbidden">{t("Pages_Command_Member_VereinNoAccess")}</StatePanel>
              ) : (
                <form
                  className="member-detail-form"
                  onSubmit={(event) => {
                    event.preventDefault();
                    requestMembershipSave();
                  }}
                >
                  <label>
                    <span>{t("Pages_Command_Member_FieldMembershipStatus")}</span>
                    <select
                      value={currentMembershipStatus}
                      onChange={(event) =>
                        setMembership({ status: event.target.value as VereinMembershipStatus })
                      }
                    >
                      <option value="Active">{t("Pages_Command_Members_StatusActive")}</option>
                      <option value="Suspended">
                        {t("Pages_Command_Members_StatusSuspended")}
                      </option>
                      <option value="Ended">{t("Pages_Command_Members_StatusEnded")}</option>
                    </select>
                  </label>
                  <label>
                    <span>{t("Pages_Command_Member_FieldMemberNumber")}</span>
                    <input
                      maxLength={64}
                      value={memberNumber}
                      onChange={(event) => setMembership({ memberNumber: event.target.value })}
                    />
                  </label>
                  <label className="member-detail-field--wide">
                    <span>{t("Pages_Command_Member_FieldMembershipNotes")}</span>
                    <textarea
                      rows={4}
                      maxLength={2048}
                      value={membershipNotes}
                      onChange={(event) => setMembership({ notes: event.target.value })}
                    />
                  </label>
                  <label className="member-detail-field--wide">
                    <span>{t("Pages_Command_Member_FieldMembershipReason")}</span>
                    <input
                      value={membershipReason}
                      onChange={(event) => setMembership({ reason: event.target.value })}
                    />
                  </label>
                  {membership && (
                    <p className="member-detail-membership-timeline">
                      {t("Pages_Command_Members_LabelMembershipStart")}:{" "}
                      {formatDate(membership.timeStampStarted)}
                      {membership.timeStampEnded
                        ? ` · ${t("Pages_Command_Members_LabelMembershipEnd")}: ${formatDate(membership.timeStampEnded)}`
                        : ""}
                    </p>
                  )}
                  <div className="form-actions">
                    <button
                      className="button button--primary"
                      type="submit"
                      disabled={membershipMutation.isPending}
                    >
                      {t(
                        membershipMutation.isPending
                          ? "Pages_Command_Member_Saving"
                          : "Pages_Command_Member_SaveMembership",
                      )}
                    </button>
                    <button
                      className="button button--ghost"
                      type="button"
                      disabled={!membershipDirty || membershipMutation.isPending}
                      onClick={() => setMembershipState(undefined)}
                    >
                      {t("Common_Cancel")}
                    </button>
                  </div>
                </form>
              )}
            </section>
          </div>

          <div className="member-detail-authority-grid">
            <AssignmentPanel
              title={t("Pages_Command_Member_RolesTitle")}
              kicker={t("Pages_Command_Member_AuthorityKicker")}
              searchLabel={t("Pages_Command_Member_SearchRoles")}
              search={roleSearch}
              onSearch={setRoleSearch}
              authorized={permissions.authority}
              assignedTitle={t("Pages_Command_Member_AssignedRoles")}
              availableTitle={t("Pages_Command_Member_AvailableRoles")}
              emptyAssigned={t("Pages_Command_Member_NoAssignedRoles")}
              emptyAvailable={t("Pages_Command_Member_NoAvailableRoles")}
              assigned={assignedRoles.map((role) => ({
                id: role.primaryKey,
                title: roleTitle(role, t("Pages_Command_Member_FallbackRoleTitle")),
                detail: role.description || t("Pages_Command_Member_NoDescription"),
                secondary: `#${role.primaryKey ?? "n/a"}`,
              }))}
              available={availableRoles.map((entry) => ({
                id: entry.role!.primaryKey,
                title: roleTitle(entry.role!, t("Pages_Command_Member_FallbackRoleTitle")),
                detail: entry.role!.description || t("Pages_Command_Member_NoDescription"),
                secondary: formatResource(
                  t("Pages_Command_Member_RoleClaimCount"),
                  entry.claims?.length ?? 0,
                ),
              }))}
              pending={authorityMutation.isPending}
              onSet={(id, active) => authorityMutation.mutate({ kind: "role", id, active })}
            />
            <AssignmentPanel
              title={t("Pages_Command_Member_DirectClaimsTitle")}
              kicker={t("Pages_Command_Member_DirectClaimsKicker")}
              searchLabel={t("Pages_Command_Member_SearchClaims")}
              search={claimSearch}
              onSearch={setClaimSearch}
              authorized={permissions.authority}
              assignedTitle={t("Pages_Command_Member_AssignedClaims")}
              availableTitle={t("Pages_Command_Member_AvailableClaims")}
              emptyAssigned={t("Pages_Command_Member_NoAssignedClaims")}
              emptyAvailable={t("Pages_Command_Member_NoAvailableClaims")}
              assigned={assignedClaims.map((claim) => ({
                id: claim.primaryKey,
                title: claimTitle(claim, t("Pages_Command_Member_FallbackClaimTitle")),
                detail: claimIdentifier(claim, t("Pages_Command_Member_NoIdentifier")),
                secondary: claim.category || t("Pages_Command_Member_Uncategorized"),
              }))}
              available={availableClaims.map((claim) => ({
                id: claim.primaryKey,
                title: claimTitle(claim, t("Pages_Command_Member_FallbackClaimTitle")),
                detail: claimIdentifier(claim, t("Pages_Command_Member_NoIdentifier")),
                secondary: claim.category || t("Pages_Command_Member_Uncategorized"),
              }))}
              pending={authorityMutation.isPending}
              onSet={(id, active) => authorityMutation.mutate({ kind: "claim", id, active })}
            />
          </div>
        </>
      )}

      <ConfirmDialog
        open={confirmation != null}
        title={t(
          confirmation === "ban"
            ? "Pages_Command_Member_ConfirmBanTitle"
            : "Pages_Command_Member_ConfirmEndMembershipTitle",
        )}
        description={t(
          confirmation === "ban"
            ? "Pages_Command_Member_ConfirmBanDescription"
            : "Pages_Command_Member_ConfirmEndMembershipDescription",
        )}
        challenge={displayName}
        confirmLabel={t(
          confirmation === "ban"
            ? "Pages_Command_Member_ConfirmBanAction"
            : "Pages_Command_Member_ConfirmEndMembershipAction",
        )}
        cancelLabel={t("Common_Cancel")}
        onCancel={() => setConfirmation(undefined)}
        onConfirm={() => {
          const action = confirmation;
          setConfirmation(undefined);
          if (action === "ban") profileMutation.mutate();
          if (action === "end-membership") membershipMutation.mutate();
        }}
      />
    </section>
  );
}

interface AssignmentItem {
  id: number | undefined;
  title: string;
  detail: string;
  secondary: string;
}

function AssignmentPanel({
  title,
  kicker,
  searchLabel,
  search,
  onSearch,
  authorized,
  assignedTitle,
  availableTitle,
  emptyAssigned,
  emptyAvailable,
  assigned,
  available,
  pending,
  onSet,
}: {
  title: string;
  kicker: string;
  searchLabel: string;
  search: string;
  onSearch(value: string): void;
  authorized: boolean;
  assignedTitle: string;
  availableTitle: string;
  emptyAssigned: string;
  emptyAvailable: string;
  assigned: AssignmentItem[];
  available: AssignmentItem[];
  pending: boolean;
  onSet(id: number, active: boolean): void;
}) {
  const { t } = useTranslation();
  return (
    <section className="command-panel member-assignment-panel">
      <header className="command-panel__header">
        <span className="kicker">{kicker}</span>
        <h2>{title}</h2>
        <StatusBadge>{assigned.length}</StatusBadge>
      </header>
      {!authorized ? (
        <StatePanel kind="forbidden">{t("Pages_Command_Member_AuthorityNoAccess")}</StatePanel>
      ) : (
        <>
          <div className="toolbar member-assignment-search">
            <label className="search-field">
              <span className="sr-only">{searchLabel}</span>
              <input
                value={search}
                placeholder={searchLabel}
                onChange={(event) => onSearch(event.target.value)}
              />
            </label>
          </div>
          <div className="member-assignment-columns">
            <AssignmentList
              title={assignedTitle}
              empty={emptyAssigned}
              items={assigned}
              pending={pending}
              actionLabel={t("Remove")}
              active={false}
              onSet={onSet}
            />
            <AssignmentList
              title={availableTitle}
              empty={emptyAvailable}
              items={available}
              pending={pending}
              actionLabel={t("Add")}
              active
              onSet={onSet}
            />
          </div>
        </>
      )}
    </section>
  );
}

function AssignmentList({
  title,
  empty,
  items,
  pending,
  actionLabel,
  active,
  onSet,
}: {
  title: string;
  empty: string;
  items: AssignmentItem[];
  pending: boolean;
  actionLabel: string;
  active: boolean;
  onSet(id: number, active: boolean): void;
}) {
  return (
    <section className="member-assignment-list">
      <h3>{title}</h3>
      {items.length === 0 ? (
        <p>{empty}</p>
      ) : (
        <ul>
          {items.map((item) => (
            <li key={item.id ?? `${item.title}:${item.detail}`}>
              <div>
                <strong>{item.title}</strong>
                <span>{item.secondary}</span>
                <small>{item.detail}</small>
              </div>
              <button
                className={
                  active
                    ? "button button--primary button--small"
                    : "button button--ghost button--small"
                }
                type="button"
                disabled={pending || item.id == null}
                onClick={() => item.id != null && onSet(item.id, active)}
              >
                {actionLabel}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
