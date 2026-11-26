import { useMutation, useQuery } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { formatResource } from "@/app/i18n";
import { ActionButton } from "@/components/ActionControl";
import { useAppToast } from "@/components/AppToasts";
import { ConfirmDialog, StatePanel } from "@/components/primitives";
import { useLeaveGuard } from "@/components/useLeaveGuard";
import { ModPackDiff } from "@/components/server/ModPackDiff";
import { ModPackPanel } from "@/components/server/ModPackPanel";
import { PendingChangesBar } from "@/components/server/PendingChangesBar";
import { ReviewChangesDialog } from "@/components/server/ReviewChangesDialog";
import { SettingsPanel } from "@/components/server/SettingsPanel";
import {
  SettingsPanelStateProvider,
  useSettingsPanelState,
} from "@/components/server/useSettingsPanelState";
import "@/components/server/change.css";
import "@/components/server/sections.css";
import { AutomationPanel } from "@/features/hosting/AutomationPanel";
import { ServerAccessPanel } from "@/features/hosting/ServerAccessPanel";
import { ServerConfigurationPanel } from "@/features/hosting/ServerConfigurationPanel";
import { ServerCostOwnerPanel } from "@/features/hosting/ServerCostOwnerPanel";
import { ServerSecretsPanel } from "@/features/hosting/ServerSecretsPanel";
import { useModPackComparison } from "@/features/hosting/modPackDiff";
import { isDeploymentActive } from "@/features/hosting/serverState";
import {
  validateStagedSecrets,
  validateStagedSettings,
  type SavedServerChanges,
  type StagedSecret,
} from "@/features/hosting/serverChangeDraft";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, GameServerSecretStatus, LifecycleJob } from "@/features/hosting/types";
import {
  ServerChangeDraftProvider,
  useServerChangeDraft,
} from "@/features/hosting/useServerChangeDraft";
import { useApplyServerChanges, useReviewGroups } from "@/features/hosting/useServerChangeApply";
import { leavesWorkspaceSection } from "@/features/hosting/workspaceSections";

/**
 * The Settings section: a heading row with Expand all or Collapse all, followed by collapsible panels for the
 * configuration, the credentials for members who may manage secrets, the automation and the server record with
 * Delete server.
 *
 * The section owns the in-memory draft of staged changes. Editing a configuration value stages it; the sticky
 * pending-changes bar then offers Review changes and Discard changes, and the review dialog applies the staged
 * changes through the existing endpoints. Nothing is saved before the member applies it.
 *
 * Every panel starts collapsed on the first visit of a browser session; the open panels are remembered in
 * `sessionStorage`. A panel opens by itself when the location hash names it, when a change is newly staged in it, when
 * its automation draft is dirty and when a review attempt finds an invalid field in it. Collapse all skips panels
 * that must stay open.
 *
 * The credentials panel is not rendered at all without `canManageSecrets`: no heading, no empty panel and no
 * secrets request. The automation panel keeps its own draft and save row. The server
 * record panel is hidden for an archived server and for a viewer without `canModify`. Deleting needs the confirm
 * dialog and is possible only without a deployment, without pending lifecycle work and without a job that can
 * still be retried or skipped; otherwise the button is unavailable and says why. A failed deletion shows a
 * message in an alert region. A successful deletion leaves for the fleet and replaces the history entry.
 *
 * @param props.server The server whose settings are shown.
 * @param props.jobs The loaded lifecycle jobs of the server, used to decide whether deletion is possible.
 */
export function SettingsSection({ server, jobs }: { server: GameServer; jobs: LifecycleJob[] }) {
  const configuration = useQuery({
    queryKey: hostingQueryKeys.configuration(server.primaryKey),
    queryFn: ({ signal }) => hostingService.configuration(server.primaryKey, signal),
  });
  const entries = configuration.data?.entries;
  const saved = useMemo<SavedServerChanges>(
    () => ({
      modPackId: server.selectedModPackDefinitionId ?? null,
      profile: {
        profileId: server.selectedConfigurationProfileId ?? null,
        revisionId: server.pinnedConfigurationProfileRevisionId ?? null,
      },
      entries,
    }),
    [
      server.selectedModPackDefinitionId,
      server.selectedConfigurationProfileId,
      server.pinnedConfigurationProfileRevisionId,
      entries,
    ],
  );
  return (
    <ServerChangeDraftProvider saved={saved} readOnly={!server.canModify || server.isArchived}>
      <SettingsPanelStateProvider>
        <SettingsSectionContent server={server} jobs={jobs} />
      </SettingsPanelStateProvider>
    </ServerChangeDraftProvider>
  );
}

/**
 * Picks the credential whose result takes focus after the member applied credential changes.
 *
 * A rotation wins because it is the change that shows a one-time value; among equals the display order of the secrets
 * list decides, and a staged key the list does not know yet comes last.
 *
 * @param staged The staged credential changes of the draft.
 * @param order The saved credential statuses in display order, or undefined while they are not loaded.
 * @returns The key to focus, or null when no credential change is staged.
 */
function credentialFocusKey(
  staged: ReadonlyMap<string, StagedSecret>,
  order: readonly GameServerSecretStatus[] | undefined,
): string | null {
  const listed = (order ?? []).map((secret) => secret.key).filter((key) => staged.has(key));
  const keys = [...listed, ...[...staged.keys()].filter((key) => !listed.includes(key))];
  return keys.find((key) => staged.get(key)?.kind === "rotate") ?? keys[0] ?? null;
}

/** The content of the Settings section, rendered inside the draft provider. */
function SettingsSectionContent({ server, jobs }: { server: GameServer; jobs: LifecycleJob[] }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { showToast } = useAppToast();
  const { anyCollapsibleOpen, collapseAll, expandAll, open } = useSettingsPanelState();
  const {
    draft,
    count,
    saved,
    announcement,
    reviewAttempt,
    discard,
    removeItem,
    markReviewAttempt,
    announce,
  } = useServerChangeDraft();
  // The same query key as the credentials panel, so both read one cached list.
  const secretStatuses = useQuery({
    queryKey: hostingQueryKeys.secrets(server.primaryKey),
    queryFn: ({ signal }) => hostingService.secrets(server.primaryKey, signal),
    enabled: server.canManageSecrets === true,
  });
  const secrets = secretStatuses.data;
  const groups = useReviewGroups(server);
  // One comparison serves the dialog extra and the Apply block, so the two cannot disagree.
  const comparison = useModPackComparison(
    server.primaryKey,
    saved.modPackId,
    draft.modPack ? draft.modPack.to : undefined,
  );
  const sectionRef = useRef<HTMLDivElement>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const previousCount = useRef(0);
  const appliedEverything = useRef(false);
  // The automation panel keeps its own draft; it reports its dirty flag and hands over the way to discard it.
  const [automationDirty, setAutomationDirty] = useState(false);
  const automationDiscard = useRef<() => void>(() => undefined);
  const registerAutomationDiscard = useCallback((discardAutomation: () => void) => {
    automationDiscard.current = discardAutomation;
  }, []);
  const [reviewOpen, setReviewOpen] = useState(false);
  const [discardOpen, setDiscardOpen] = useState(false);
  const [headingFocus, setHeadingFocus] = useState(0);
  // D-15: the credential whose result takes focus after a complete apply. A complete run keeps no per-item results
  // and the draft is empty afterwards, so the key is captured when the apply starts.
  const credentialToFocus = useRef<string | null>(null);
  const [focusRequest, setFocusRequest] = useState<{ key: string; seq: number } | undefined>(
    undefined,
  );
  const applier = useApplyServerChanges(server, {
    onAllApplied: (applied) => {
      appliedEverything.current = true;
      setReviewOpen(false);
      showToast(
        t("Server_Change_Toast_Applied", { count: applied, name: server.title }),
        "success",
      );
      const key = credentialToFocus.current;
      credentialToFocus.current = null;
      if (key === null) setHeadingFocus((current) => current + 1);
      else setFocusRequest((current) => ({ key, seq: (current?.seq ?? 0) + 1 }));
    },
  });
  const applyChanges = () => {
    credentialToFocus.current = credentialFocusKey(draft.secrets, secrets);
    applier.apply();
  };
  // The dialog closes by itself when the draft empties, for example after the saved data caught up.
  if (reviewOpen && count === 0) setReviewOpen(false);
  if (discardOpen && count === 0) setDiscardOpen(false);
  useEffect(() => {
    if (headingFocus > 0) headingRef.current?.focus();
  }, [headingFocus]);
  // Invalid fields are derived from the staged values once the member tried to review; nothing is stored.
  const countInvalid = () =>
    validateStagedSettings(draft, saved.entries ?? []).length +
    validateStagedSecrets(draft, secrets ?? []).length;
  const invalidCount = reviewAttempt > 0 ? countInvalid() : 0;
  // The panel of the first invalid field opens first, because a field in a collapsed panel cannot take focus; the
  // field is focused on the next frame, once the panel is shown.
  useEffect(() => {
    if (reviewAttempt === 0) return;
    const invalid = sectionRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]');
    if (!invalid) return;
    const panelId = invalid.closest<HTMLElement>("[data-settings-panel]")?.dataset.settingsPanel;
    if (panelId) open(panelId);
    // The panel opens through a state update that may commit after the next frame, and a field inside a collapsed
    // panel cannot take focus, so the frame waits (a few frames at most) until the field is no longer hidden.
    let frame = 0;
    let framesLeft = 10;
    const focusWhenShown = () => {
      if (invalid.closest("[hidden]") !== null && framesLeft > 0) {
        framesLeft -= 1;
        frame = requestAnimationFrame(focusWhenShown);
        return;
      }
      invalid.focus();
    };
    frame = requestAnimationFrame(focusWhenShown);
    return () => cancelAnimationFrame(frame);
  }, [reviewAttempt, open]);
  // Count transitions are announced with fixed copy only; the toast speaks after a complete apply.
  useEffect(() => {
    const before = previousCount.current;
    previousCount.current = count;
    if (before === 0 && count > 0) announce(t("Server_Change_Announce_Staged"));
    else if (before > 0 && count === 0) {
      if (appliedEverything.current) appliedEverything.current = false;
      else announce(t("Server_Change_Announce_Empty"));
    }
  }, [count, announce, t]);
  const review = () => {
    if (countInvalid() > 0) markReviewAttempt();
    else setReviewOpen(true);
  };
  // D-10: every path out of Settings asks first while changes are staged. The guard stays active while an apply
  // runs, and a navigation that stays on the Settings section (for example the ?action= cleanup) is never held.
  const guard = useLeaveGuard({
    dirty: count > 0 || automationDirty,
    shouldBlock: ({ currentLocation, nextLocation }) =>
      leavesWorkspaceSection(currentLocation, nextLocation, "settings"),
  });
  // One dialog serves all three cases: staged changes only, automation settings only, or both. Review exists only
  // while staged changes do, because the automation draft has nothing to review.
  const leaveDescription =
    count > 0 && automationDirty
      ? t("Server_Automation_Leave_BodyBoth")
      : automationDirty
        ? t("Server_Automation_Leave_Body")
        : t("Server_Change_Leave_Body", { count });
  const closeReview = () => {
    setReviewOpen(false);
    applier.clearResults();
  };
  const [deleteOpen, setDeleteOpen] = useState(false);
  const deletion = useMutation({
    mutationFn: () => hostingService.delete(server.primaryKey),
    onSuccess: () => navigate("/command/servers", { replace: true }),
    onError: () => setDeleteOpen(false),
  });
  const canDelete =
    server.canModify &&
    !server.isArchived &&
    !server.deployment &&
    server.pendingJobCount === 0 &&
    jobs.every((job) => !job.canRetry && !job.canSkip);
  const showRecord = server.canModify && !server.isArchived;
  return (
    <div
      ref={sectionRef}
      className={count > 0 ? "workspace-section workspace-section--staged" : "workspace-section"}
    >
      <div className="workspace-section__head">
        <h2 ref={headingRef} tabIndex={-1} className="workspace-section__heading">
          {t("Server_Workspace_Section_Settings")}
        </h2>
        <ActionButton
          variant="ghost"
          size="compact"
          onClick={anyCollapsibleOpen ? collapseAll : expandAll}
        >
          {anyCollapsibleOpen
            ? t("Server_Settings_Panels_CollapseAll")
            : t("Server_Settings_Panels_ExpandAll")}
        </ActionButton>
      </div>
      {server.isArchived ? (
        <StatePanel kind="info">{t("Server_Change_ReadOnly_Archived")}</StatePanel>
      ) : (
        !server.canModify && (
          <StatePanel kind="info">
            {server.canManageAccess === true
              ? t("Server_Change_ReadOnly_AccessOnly")
              : t("Server_Change_ReadOnly")}
          </StatePanel>
        )
      )}
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {announcement}
      </div>
      <ModPackPanel server={server} />
      <ServerConfigurationPanel
        serverId={server.primaryKey}
        gameIdentifier={server.gameIdentifier}
        canModify={server.canModify}
      />
      <ServerSecretsPanel server={server} focusRequest={focusRequest} />
      <AutomationPanel
        server={server}
        onDirtyChange={setAutomationDirty}
        registerDiscard={registerAutomationDiscard}
      />
      <ServerAccessPanel
        server={server}
        onAccessLost={() => setHeadingFocus((current) => current + 1)}
      />
      <ServerCostOwnerPanel server={server} />
      {showRecord && (
        <SettingsPanel
          id="server-record"
          title={t("Server_Settings_RecordTitle")}
          summary={t("Server_Settings_Summary_Record")}
          className="workspace-panel--record"
        >
          <p>{t("Server_Settings_RecordBody")}</p>
          <ActionButton
            variant="danger"
            disabled={deletion.isPending}
            unavailableReason={canDelete ? undefined : t("Hosting_Detail_DeleteUnavailable")}
            onClick={() => setDeleteOpen(true)}
          >
            {t("Hosting_Detail_DeleteServer")}
          </ActionButton>
          {deletion.isError && (
            <p className="workspace-panel__error" role="alert">
              {t("Server_Settings_DeleteError")}
            </p>
          )}
          <ConfirmDialog
            open={deleteOpen}
            busy={deletion.isPending}
            title={t("Hosting_Detail_DeleteServer")}
            description={formatResource(t("Hosting_Detail_DeleteServerConfirm"), server.title)}
            confirmLabel={
              deletion.isPending
                ? t("React_ServerWorkspace_Deleting")
                : t("Hosting_Detail_DeleteServer")
            }
            cancelLabel={t("Server_Action_ArchiveRecord_Dismiss")}
            onCancel={() => setDeleteOpen(false)}
            onConfirm={() => deletion.mutate()}
          />
        </SettingsPanel>
      )}
      <PendingChangesBar
        count={count}
        onReview={review}
        onDiscard={() => setDiscardOpen(true)}
        alert={
          invalidCount > 0 ? (
            <p role="alert" className="pending-changes-bar__alert">
              {t("Server_Change_Invalid", { count: invalidCount })}
            </p>
          ) : undefined
        }
      />
      <ConfirmDialog
        open={discardOpen}
        title={t("Server_Change_DiscardDialog_Title")}
        description={t("Server_Change_DiscardDialog_Body", { count })}
        cancelLabel={t("Server_Change_KeepEditing")}
        confirmLabel={t("Server_Change_Discard")}
        confirmVariant="danger"
        closeOnBackdrop
        initialFocus="dismiss"
        onCancel={() => setDiscardOpen(false)}
        onConfirm={() => {
          discard();
          setDiscardOpen(false);
          setHeadingFocus((current) => current + 1);
        }}
      />
      <ConfirmDialog
        open={guard.blocked}
        className="dialog--leave"
        title={t("Server_Change_Leave_Title")}
        description={leaveDescription}
        cancelLabel={t("Server_Change_Leave_Stay")}
        confirmLabel={t("Server_Change_Leave_Discard")}
        confirmVariant="danger"
        confirmPlacement="start"
        dismissVariant="primary"
        closeOnBackdrop
        initialFocus="dismiss"
        onCancel={guard.stay}
        onConfirm={() => {
          if (count > 0) discard();
          if (automationDirty) automationDiscard.current();
          guard.proceed();
        }}
        extraAction={
          count > 0
            ? {
                label: t("Server_Change_Review"),
                variant: "gold",
                onClick: () => {
                  guard.stay();
                  review();
                },
              }
            : undefined
        }
      />
      <ReviewChangesDialog
        open={reviewOpen}
        serverTitle={server.title}
        running={isDeploymentActive(server)}
        groups={groups}
        count={count}
        results={applier.results}
        busy={applier.pending}
        applyBlockedReason={
          comparison.status === "loading" ? t("Server_Change_Blocked_Comparing") : undefined
        }
        renderItemExtra={(item) =>
          item.id === "modPack" ? <ModPackDiff comparison={comparison} /> : null
        }
        onApply={applyChanges}
        onDismiss={closeReview}
        onRemoveItem={(id) => {
          removeItem(id);
          if (count <= 1) closeReview();
        }}
      />
    </div>
  );
}
