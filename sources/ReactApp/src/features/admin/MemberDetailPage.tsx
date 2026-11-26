import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router";
import { useDocumentTitle } from "@/app/documentTitle";
import { formatDate } from "@/app/format";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { ActionButton, ActionLink } from "@/components/ActionControl";
import { useAppToastOnChange } from "@/components/AppToasts";
import { CheckboxField, SelectField, TextAreaField, TextField } from "@/components/FormFields";
import { PageStack } from "@/components/PageStack";
import {
  ConfirmDialog,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import { SearchField } from "@/components/SearchField";
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from "@/components/StateViews";
import { accountTones, membershipTones, toneOf } from "@/components/statusTones";
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
import "@/features/admin/members.css";

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
    // A missing member resolves to undefined, which a query may not hold; null keeps it a not-found result.
    queryFn: async ({ signal }) => (await loadMemberDetail(userId!, permissions, signal)) ?? null,
    enabled: Boolean(userId && canOpen),
  });
  const detail = query.data ?? undefined;
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
  useAppToastOnChange(
    profileMutation.isSuccess ? t("Pages_Command_Member_ProfileSaved") : undefined,
  );
  useAppToastOnChange(
    membershipMutation.isSuccess ? t("Pages_Command_Member_MembershipSaved") : undefined,
  );
  useAppToastOnChange(
    authorityMutation.isSuccess ? t("Pages_Command_Member_AuthoritySaved") : undefined,
  );

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
      <PageStack>
        <PageHeader
          kicker={t("Shared_CommandCenter_Nav_Members")}
          title={t("Pages_Command_Member_HeaderTitle")}
        />
        <ForbiddenState title={t("Pages_Command_Members_NoAccessTitle")}>
          {t("Pages_Command_Members_NoAccessMessage")}
        </ForbiddenState>
      </PageStack>
    );
  if (!userId)
    return (
      <PageStack>
        <PageHeader
          kicker={t("Shared_CommandCenter_Nav_Members")}
          title={t("Pages_Command_Member_HeaderTitle")}
        />
        <EmptyState
          title={t("Pages_Command_Member_NotFoundTitle")}
          action={
            <ActionLink variant="ghost" to="/command/members">
              {t("Pages_Command_Member_BackToRoster")}
            </ActionLink>
          }
        >
          {t("Pages_Command_Member_NotFoundMessage")}
        </EmptyState>
      </PageStack>
    );

  const accountStatus = user ? memberStatus(user) : "unverified";
  const avatar = user ? memberAvatarSource(user) : undefined;
  const displayName = user?.nickname?.trim() || t("Pages_Command_Member_UnknownUser");
  const membershipLabel = t(membershipStatusKey(membership?.status));
  const accountTone = toneOf(accountTones, accountStatus);
  const membershipTone = toneOf(membershipTones, membership?.status);
  const saveError = profileMutation.error ?? membershipMutation.error ?? authorityMutation.error;

  return (
    <PageStack>
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
            <ActionLink variant="ghost" to="/command/members">
              {t("Pages_Command_Member_BackToRoster")}
            </ActionLink>
            <ActionButton
              variant="ghost"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_Member_Refresh")}
            </ActionButton>
          </>
        }
      />
      {query.isError && (
        <ErrorState onRetry={() => void query.refetch()}>
          {formatResource(t("Pages_Command_Member_LoadError"), query.error.message)}
        </ErrorState>
      )}
      {saveError && !query.isError && (
        <StatePanel kind="error">
          {formatResource(t("Pages_Command_Member_SaveError"), saveError.message)}
        </StatePanel>
      )}
      {query.isPending ? (
        <LoadingState label={t("Pages_Command_Member_Loading")} />
      ) : query.isError && !detail ? null : !detail || !user ? (
        <EmptyState
          title={t("Pages_Command_Member_NotFoundTitle")}
          action={
            <ActionLink variant="ghost" to="/command/members">
              {t("Pages_Command_Member_BackToRoster")}
            </ActionLink>
          }
        >
          {t("Pages_Command_Member_NotFoundMessage")}
        </EmptyState>
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
                  <StatusBadge tone={accountTone}>{t(statusKey(accountStatus))}</StatusBadge>
                </dd>
              </div>
              <div>
                <dt>{t("Pages_Command_Member_FieldEmail")}</dt>
                <dd>{user.eMail || t("Pages_Command_Member_NoEmail")}</dd>
              </div>
              <div>
                <dt>{t("Pages_Command_Member_StatusVerein")}</dt>
                <dd>
                  <StatusBadge tone={membershipTone}>{membershipLabel}</StatusBadge>
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
                <StatusBadge tone={accountTone}>{t(statusKey(accountStatus))}</StatusBadge>
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
                <ForbiddenState>{t("Pages_Command_Members_NoAccessMessage")}</ForbiddenState>
              ) : (
                <form
                  className="member-detail-fields"
                  onSubmit={(event) => {
                    event.preventDefault();
                    requestProfileSave();
                  }}
                >
                  <TextField
                    required
                    maxLength={1024}
                    disabled={!permissions.nickname}
                    label={t("Pages_Command_Member_FieldNickname")}
                    value={nickname}
                    onChange={(event) => setProfile({ nickname: event.target.value })}
                  />
                  <TextField
                    type="email"
                    maxLength={1024}
                    disabled={!permissions.email}
                    label={t("Pages_Command_Member_FieldEmail")}
                    value={email}
                    onChange={(event) => setProfile({ email: event.target.value })}
                  />
                  <div className="member-detail-fields__toggles">
                    <CheckboxField
                      disabled={!permissions.verify}
                      checked={verified}
                      label={t("Pages_Command_Member_FieldVerified")}
                      onChange={(event) => setProfile({ verified: event.target.checked })}
                    />
                    <CheckboxField
                      disabled={!permissions.ban}
                      checked={banned}
                      label={t("Pages_Command_Member_FieldBanned")}
                      onChange={(event) => setProfile({ banned: event.target.checked })}
                    />
                  </div>
                  <div className="member-detail-fields__actions">
                    <ActionButton
                      variant="primary"
                      type="submit"
                      disabled={!profileDirty || profileMutation.isPending || !nickname.trim()}
                    >
                      {t(
                        profileMutation.isPending
                          ? "Pages_Command_Member_Saving"
                          : "Pages_Command_Member_SaveProfile",
                      )}
                    </ActionButton>
                    <ActionButton
                      variant="ghost"
                      disabled={!profileDirty || profileMutation.isPending}
                      onClick={() => setProfileState(undefined)}
                    >
                      {t("Pages_Command_Member_ProfileReset_Dismiss")}
                    </ActionButton>
                  </div>
                </form>
              )}
            </section>

            <section className="command-panel member-detail-panel">
              <header className="command-panel__header">
                <span className="kicker">{t("Pages_Command_Member_VereinKicker")}</span>
                <h2>{t("Pages_Command_Member_VereinTitle")}</h2>
                <StatusBadge tone={membershipTone}>{membershipLabel}</StatusBadge>
              </header>
              {!permissions.membership ? (
                <ForbiddenState>{t("Pages_Command_Member_VereinNoAccess")}</ForbiddenState>
              ) : (
                <form
                  className="member-detail-fields"
                  onSubmit={(event) => {
                    event.preventDefault();
                    requestMembershipSave();
                  }}
                >
                  <SelectField
                    label={t("Pages_Command_Member_FieldMembershipStatus")}
                    value={currentMembershipStatus}
                    options={[
                      { value: "Active", label: t("Pages_Command_Members_StatusActive") },
                      { value: "Suspended", label: t("Pages_Command_Members_StatusSuspended") },
                      { value: "Ended", label: t("Pages_Command_Members_StatusEnded") },
                    ]}
                    onChange={(event) =>
                      setMembership({ status: event.target.value as VereinMembershipStatus })
                    }
                  />
                  <TextField
                    maxLength={64}
                    label={t("Pages_Command_Member_FieldMemberNumber")}
                    value={memberNumber}
                    onChange={(event) => setMembership({ memberNumber: event.target.value })}
                  />
                  <div className="member-detail-fields__wide">
                    <TextAreaField
                      rows={4}
                      maxLength={2048}
                      label={t("Pages_Command_Member_FieldMembershipNotes")}
                      value={membershipNotes}
                      onChange={(event) => setMembership({ notes: event.target.value })}
                    />
                  </div>
                  <div className="member-detail-fields__wide">
                    <TextField
                      label={t("Pages_Command_Member_FieldMembershipReason")}
                      value={membershipReason}
                      onChange={(event) => setMembership({ reason: event.target.value })}
                    />
                  </div>
                  {membership && (
                    <p className="member-detail-fields__timeline">
                      {t("Pages_Command_Members_LabelMembershipStart")}:{" "}
                      {formatDate(membership.timeStampStarted)}
                      {membership.timeStampEnded
                        ? ` · ${t("Pages_Command_Members_LabelMembershipEnd")}: ${formatDate(membership.timeStampEnded)}`
                        : ""}
                    </p>
                  )}
                  <div className="member-detail-fields__actions">
                    <ActionButton
                      variant="primary"
                      type="submit"
                      disabled={membershipMutation.isPending}
                    >
                      {t(
                        membershipMutation.isPending
                          ? "Pages_Command_Member_Saving"
                          : "Pages_Command_Member_SaveMembership",
                      )}
                    </ActionButton>
                    <ActionButton
                      variant="ghost"
                      disabled={!membershipDirty || membershipMutation.isPending}
                      onClick={() => setMembershipState(undefined)}
                    >
                      {t("Pages_Command_Member_MembershipReset_Dismiss")}
                    </ActionButton>
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
        cancelLabel={t(
          confirmation === "ban"
            ? "Pages_Command_Member_ConfirmBan_Dismiss"
            : "Pages_Command_Member_ConfirmEndMembership_Dismiss",
        )}
        initialFocus="dismiss"
        onCancel={() => setConfirmation(undefined)}
        onConfirm={() => {
          const action = confirmation;
          setConfirmation(undefined);
          if (action === "ban") profileMutation.mutate();
          if (action === "end-membership") membershipMutation.mutate();
        }}
      />
    </PageStack>
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
        <ForbiddenState>{t("Pages_Command_Member_AuthorityNoAccess")}</ForbiddenState>
      ) : (
        <>
          <div className="toolbar member-assignment-search">
            <SearchField
              label={searchLabel}
              placeholder={searchLabel}
              clearLabel={t("Common_SearchClear")}
              value={search}
              onChange={onSearch}
            />
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
              <ActionButton
                variant={active ? "primary" : "ghost"}
                size="compact"
                disabled={pending || item.id == null}
                onClick={() => item.id != null && onSet(item.id, active)}
              >
                {actionLabel}
              </ActionButton>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
