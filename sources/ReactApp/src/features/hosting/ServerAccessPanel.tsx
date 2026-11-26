import { Person20Regular, PeopleTeam20Regular } from "@fluentui/react-icons";
import { keepPreviousData, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import { ApiError } from "@/api/client";
import { currentLocale } from "@/app/format";
import { useDebouncedValue } from "@/app/useDebouncedValue";
import { useAuth } from "@/auth/auth";
import { ActionButton } from "@/components/ActionControl";
import { useAppToast } from "@/components/AppToasts";
import { SearchField } from "@/components/SearchField";
import type { PickerOption, SearchPickerStatus } from "@/components/SearchPicker";
import { ConfirmDialog } from "@/components/primitives";
import { EmptyState, ErrorState, LoadingState } from "@/components/StateViews";
import { AccessGrantForm, type AccessGrantKind } from "@/components/server/AccessGrantForm";
import { AccessSubjectRow, type AccessSubject } from "@/components/server/AccessSubjectRow";
import { SettingsPanel } from "@/components/server/SettingsPanel";
import { useSettingsPanelState } from "@/components/server/useSettingsPanelState";
import "@/components/server/access.css";
import { limitPickerResults } from "@/features/hosting/pickerLimits";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import {
  canAssign,
  needsViewWarning,
  requiresSelfConfirm,
  serverPermissionCatalog,
  viewPermission,
} from "@/features/hosting/serverAccessModel";
import type {
  GameServer,
  ServerAccess,
  ServerAccessRoleCandidate,
  ServerAccessUserCandidate,
} from "@/features/hosting/types";
import {
  grantFailureKey,
  removeFailureKey,
  type AccessSettleCallbacks,
  useGrantAccess,
  useRemoveAllAccess,
  useToggleAccess,
} from "@/features/hosting/useAccessMutations";

/** The anchor id of the panel; the location hash `#server-access` opens it. */
const accessPanelId = "server-access";

/** How long the search text must rest before the candidates are requested. */
const searchDebounceMs = 250;

/** The number of subjects from which the list offers a filter; the same threshold as the configuration panel (D-16). */
const accessFilterThreshold = 8;

/**
 * Tells whether a subject still has a row in the access list.
 *
 * @param grants The access list, or undefined while it is not loaded.
 * @param key The subject key, `user:<member id>` or `role:<role id>`.
 * @returns True when the list holds the subject.
 */
function isSubjectKeyListed(grants: ServerAccess | undefined, key: string): boolean {
  if (grants === undefined) return false;
  return (
    grants.users.some((entry) => `user:${entry.userId}` === key) ||
    grants.roles.some((entry) => `role:${entry.roleId}` === key)
  );
}

/** The answer of one candidate search, tagged with the kind it was asked for so a stale kind is never mapped. */
type CandidateResult =
  | { kind: "user"; items: ServerAccessUserCandidate[] }
  | { kind: "role"; items: ServerAccessRoleCandidate[] };

/**
 * Searches the candidates of one kind.
 *
 * @param serverId The server that would get the subject.
 * @param kind Whether to search members or roles.
 * @param search The trimmed search text.
 * @param signal Aborts a stale request.
 * @returns The candidates, tagged with the kind that was searched.
 */
async function searchCandidates(
  serverId: string,
  kind: AccessGrantKind,
  search: string,
  signal: AbortSignal,
): Promise<CandidateResult> {
  if (kind === "user")
    return { kind, items: await hostingService.serverUserCandidates(serverId, search, signal) };
  return { kind, items: await hostingService.serverRoleCandidates(serverId, search, signal) };
}

/**
 * Builds the key of one subject-permission pair, the unit a toggle fails and is pending on.
 *
 * @param subject The member or role.
 * @param permission The permission id.
 * @returns The key; the subject part never contains the separator.
 */
function pairKey(subject: AccessSubject, permission: string): string {
  return `${subject.kind}:${subject.id}|${permission}`;
}

/**
 * Copies a set of pair keys without one pair.
 *
 * @param pairs The pair keys.
 * @param pair The key to leave out.
 * @returns A new set, or the same set when the pair was not in it.
 */
function withoutPair(pairs: ReadonlySet<string>, pair: string): ReadonlySet<string> {
  if (!pairs.has(pair)) return pairs;
  const next = new Set(pairs);
  next.delete(pair);
  return next;
}

/**
 * Picks the permission ids of one subject out of a set of pair keys.
 *
 * @param pairs The pair keys of every subject.
 * @param subject The member or role.
 * @returns The permission ids that belong to the subject.
 */
function permissionsOfSubject(
  pairs: ReadonlySet<string>,
  subject: AccessSubject,
): ReadonlySet<string> {
  const prefix = `${subject.kind}:${subject.id}|`;
  return new Set(
    [...pairs].filter((pair) => pair.startsWith(prefix)).map((pair) => pair.slice(prefix.length)),
  );
}

/**
 * Tells whether a subject stays in the list for a filter text. A subject that cannot see the server because it lacks
 * View is never hidden, so a filter can never bury the one entry that needs attention.
 *
 * @param subject The member or role.
 * @param kindLabel The translated kind of the subject (Member or Role), which the filter matches as well.
 * @param needle The trimmed filter text, already lowercased for the locale; empty matches everything.
 * @returns True when the subject is shown.
 */
function matchesFilter(subject: AccessSubject, kindLabel: string, needle: string): boolean {
  if (needle === "" || needsViewWarning(subject.permissions)) return true;
  const locale = currentLocale();
  return (
    subject.name.toLocaleLowerCase(locale).includes(needle) ||
    kindLabel.toLocaleLowerCase(locale).includes(needle)
  );
}

/** Props of {@link ServerAccessPanel}. */
export interface ServerAccessPanelProps {
  /** The server whose direct access grants are shown. */
  server: GameServer;
  /**
   * Called when a change of access took the member's own right to manage access away, after the panel had to go. The
   * caller moves focus to an element that survives the panel, because the focused element is gone with it.
   */
  onAccessLost?: (() => void) | undefined;
}

/**
 * The Access panel of the Settings section.
 *
 * The panel is a collapsible {@link SettingsPanel} with the id `server-access`. It renders only for a member who may
 * manage access to the server: without `canManageAccess` there is no heading, no summary and no request, because the
 * content component that owns the query is not even mounted. An archived server keeps the panel.
 *
 * @param props.server The server whose access is shown.
 * @param props.onAccessLost Called after the member lost the right to manage access through a change made here.
 * @returns The panel, or null for a member who may not manage access.
 */
export function ServerAccessPanel({ server, onAccessLost }: ServerAccessPanelProps) {
  if (server.canManageAccess !== true) return null;
  return <ServerAccessPanelContent server={server} onAccessLost={onAccessLost} />;
}

/**
 * The part of the Access panel that exists only for a member who may manage access.
 *
 * The summary names the number of people and roles with access once the list is loaded; it carries no count while the
 * list loads or after a failed load. The grants are read through a query with `staleTime` 0 that refetches when the
 * window regains focus and when the member opens the panel by hand, so another manager's change shows up without any
 * polling. Each subject is one row; the open state of a row is kept here by subject, so a refetch never closes an open
 * row. The panel never stages anything: it contributes nothing to the pending changes bar, the review dialog or the
 * leave guard.
 *
 * Every access mutation ends in the shared settle step, which refreshes the server, the fleet and the signed-in member
 * and reports a lost right: losing View sends the member to the fleet with a toast, losing only the right to manage
 * access shows a toast and hands focus to the caller through `onAccessLost`.
 *
 * @param props.server The server whose access is shown; the viewer may manage it.
 * @param props.onAccessLost Called after the member lost the right to manage access through a change made here.
 */
function ServerAccessPanelContent({ server, onAccessLost }: ServerAccessPanelProps) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const { showToast } = useAppToast();
  const { isOpen } = useSettingsPanelState();
  const [openRows, setOpenRows] = useState<ReadonlySet<string>>(() => new Set());
  const access = useQuery({
    queryKey: hostingQueryKeys.access(server.primaryKey),
    queryFn: ({ signal }) => hostingService.serverAccess(server.primaryKey, signal),
    staleTime: 0,
    refetchOnWindowFocus: true,
  });
  const { refetch } = access;
  const panelOpen = isOpen(accessPanelId);
  const wasOpen = useRef(panelOpen);

  // Opening the panel by hand refetches the list, which is the backstop for another manager's change. The previous
  // state is kept in a ref that only this effect updates, so the first render and a re-render never refetch.
  useEffect(() => {
    if (panelOpen && !wasOpen.current) void refetch();
    wasOpen.current = panelOpen;
  }, [panelOpen, refetch]);

  const setRowOpen = useCallback((key: string, open: boolean) => {
    setOpenRows((current) => {
      if (current.has(key) === open) return current;
      const next = new Set(current);
      if (open) next.add(key);
      else next.delete(key);
      return next;
    });
  }, []);

  const [kind, setKind] = useState<AccessGrantKind>("user");
  const [query, setQuery] = useState("");
  const [selected, setSelected] = useState<PickerOption | null>(null);
  const [announcement, setAnnouncement] = useState("");
  const [focusSerial, setFocusSerial] = useState(0);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const handledFocus = useRef(0);
  // These run from the hook-level callbacks of the mutations, after this panel may be gone, so they use only values
  // that outlive it: the toast API of the app, the router and the callback of the Settings section.
  const settleCallbacks: AccessSettleCallbacks = {
    onViewLost: () => {
      showToast(t("Server_Access_Toast_ViewLost", { name: server.title }), "info");
      void navigate("/command/servers", { replace: true });
    },
    onManageLost: () => {
      showToast(t("Server_Access_Toast_SelfLost", { name: server.title }), "info");
      onAccessLost?.();
    },
  };
  const grant = useGrantAccess(server.primaryKey, settleCallbacks);
  const toggle = useToggleAccess(server.primaryKey, settleCallbacks);
  const [failedPairs, setFailedPairs] = useState<ReadonlySet<string>>(() => new Set());
  const [forbiddenPairs, setForbiddenPairs] = useState<ReadonlySet<string>>(() => new Set());
  const [pendingPairs, setPendingPairs] = useState<ReadonlySet<string>>(() => new Set());
  const inFlight = useRef<Set<string>>(new Set());
  const [selfRemoval, setSelfRemoval] = useState<{
    subject: AccessSubject;
    permission: string;
  } | null>(null);
  const removeAll = useRemoveAllAccess(server.primaryKey, settleCallbacks);
  const [removal, setRemoval] = useState<{ subject: AccessSubject; isSelf: boolean } | null>(null);
  const [filter, setFilter] = useState("");
  const filterInputRef = useRef<HTMLInputElement | null>(null);
  const [listFocus, setListFocus] = useState(0);
  const listHeadingRef = useRef<HTMLHeadingElement>(null);
  const toggledSubject = useRef<string | null>(null);
  const queryClient = useQueryClient();

  // After Remove all access the row is gone, so focus moves to the list heading. The dialog closes in a child effect of
  // the same commit, which runs first, so the browser's focus restore cannot overwrite this.
  useEffect(() => {
    if (listFocus > 0) listHeadingRef.current?.focus();
  }, [listFocus]);

  // A toggle keeps its row through the optimistic update, but the refetch that follows drops a subject whose last
  // permission is gone, and the focused switch goes with the row. When that happened and focus fell to the document,
  // it moves to the list heading. A member who has moved focus somewhere else on purpose is left alone. The key is
  // dropped as soon as a settled toggle leaves its subject listed, so a later removal for another reason is not a
  // focus move this member caused.
  const grantsForFocus = access.data;
  useEffect(() => {
    const key = toggledSubject.current;
    if (key === null || grantsForFocus === undefined) return;
    if (isSubjectKeyListed(grantsForFocus, key)) return;
    toggledSubject.current = null;
    const active = document.activeElement;
    if (active === null || active === document.body) listHeadingRef.current?.focus();
  }, [grantsForFocus]);

  /** The visible name of a permission; an id the catalog does not know is shown as it is. */
  const permissionLabel = (permission: string): string => {
    const definition = serverPermissionCatalog.find((entry) => entry.id === permission);
    return definition === undefined ? permission : t(definition.labelKey);
  };

  /**
   * Flips one permission of one subject. The change is optimistic in the cache; this only tracks which pair is in
   * flight and which pair failed and announces the result, because `mutate` callbacks fire for the latest call only
   * and toggles overlap.
   */
  const runToggle = async (subject: AccessSubject, permission: string, granted: boolean) => {
    const pair = pairKey(subject, permission);
    if (inFlight.current.has(pair)) return;
    toggledSubject.current = `${subject.kind}:${subject.id}`;
    inFlight.current.add(pair);
    setPendingPairs((current) => new Set(current).add(pair));
    setFailedPairs((current) => withoutPair(current, pair));
    setForbiddenPairs((current) => withoutPair(current, pair));
    try {
      await toggle.mutateAsync({
        kind: subject.kind,
        subjectId: subject.id,
        name: subject.name,
        permission,
        grant: granted,
      });
      setAnnouncement(
        t(granted ? "Server_Access_Announce_On" : "Server_Access_Announce_Off", {
          name: subject.name,
          permission: permissionLabel(permission),
        }),
      );
    } catch (error) {
      setFailedPairs((current) => new Set(current).add(pair));
      // A 403 means the member lost the right to manage access elsewhere; the switch says so instead of "try again".
      if (error instanceof ApiError && error.status === 403)
        setForbiddenPairs((current) => new Set(current).add(pair));
    } finally {
      inFlight.current.delete(pair);
      setPendingPairs((current) => withoutPair(current, pair));
      const settled = queryClient.getQueryData<ServerAccess>(
        hostingQueryKeys.access(server.primaryKey),
      );
      if (
        inFlight.current.size === 0 &&
        isSubjectKeyListed(settled, `${subject.kind}:${subject.id}`)
      )
        toggledSubject.current = null;
    }
  };

  /**
   * Handles a flipped switch. Turning off a permission that could lock the signed-in member out waits for the
   * confirmation dialog, and until it is confirmed the switch does not move; everything else runs at once.
   */
  const changePermission = (
    subject: AccessSubject,
    isSelf: boolean,
    permission: string,
    granted: boolean,
  ) => {
    if (inFlight.current.has(pairKey(subject, permission))) return;
    if (requiresSelfConfirm(isSelf, permission, granted)) setSelfRemoval({ subject, permission });
    else void runToggle(subject, permission, granted);
  };
  const trimmedQuery = query.trim();
  const debouncedQuery = useDebouncedValue(trimmedQuery, searchDebounceMs);
  const candidates = useQuery({
    queryKey: hostingQueryKeys.accessCandidates(server.primaryKey, kind, debouncedQuery),
    queryFn: ({ signal }) => searchCandidates(server.primaryKey, kind, debouncedQuery, signal),
    // Only a text that has rested is searched: right after a kind switch the debounced copy still holds the old text.
    enabled: trimmedQuery.length >= 1 && debouncedQuery === trimmedQuery,
    staleTime: 20_000,
    placeholderData: keepPreviousData,
  });
  const { refetch: refetchCandidates } = candidates;

  // After a grant the picker input is mounted again; focus moves to it once the grant has settled and the input is
  // enabled. The handled serial lives in a ref that only this effect updates.
  useEffect(() => {
    if (focusSerial === handledFocus.current || grant.isPending) return;
    handledFocus.current = focusSerial;
    inputRef.current?.focus();
  }, [focusSerial, grant.isPending]);

  const changeKind = (next: AccessGrantKind) => {
    if (next === kind) return;
    setKind(next);
    setQuery("");
    setSelected(null);
    grant.reset();
  };

  const submitGrant = () => {
    if (selected === null) return;
    grant.mutate(
      { kind, subjectId: selected.id, name: selected.label },
      {
        onSuccess: (_result, variables) => {
          setSelected(null);
          setQuery("");
          setAnnouncement(t("Server_Access_Announce_Granted", { name: variables.name }));
          setFocusSerial((serial) => serial + 1);
        },
      },
    );
  };

  // The candidates are mapped only when they were asked for the current kind, so a placeholder kept from the other
  // flavor is never shown through the wrong mapping.
  const result = trimmedQuery.length >= 1 ? candidates.data : undefined;
  let options: PickerOption[] = [];
  let hasMore = false;
  if (result?.kind === "user" && kind === "user") {
    const limited = limitPickerResults(result.items);
    hasMore = limited.hasMore;
    options = limited.items.map((candidate) => ({
      id: candidate.userId,
      label: candidate.nickname,
      detail: candidate.detail ?? undefined,
      icon: <Person20Regular />,
    }));
  } else if (result?.kind === "role" && kind === "role") {
    const limited = limitPickerResults(result.items);
    hasMore = limited.hasMore;
    options = limited.items.map((candidate) => ({
      id: String(candidate.roleId),
      label: candidate.title,
      detail: t("Server_Access_Role_Members", { count: candidate.memberCount }),
      icon: <PeopleTeam20Regular />,
    }));
  }

  // The status follows the immediate text, not the debounced copy, so the listbox opens without waiting.
  let pickerStatus: SearchPickerStatus;
  if (trimmedQuery.length < 1) pickerStatus = "idle";
  else if (debouncedQuery !== trimmedQuery) pickerStatus = "loading";
  else if (candidates.isError) pickerStatus = "error";
  else if (candidates.isFetching || candidates.data === undefined) pickerStatus = "loading";
  else pickerStatus = "ready";

  const grants = access.data;
  const canGiveView = grants === undefined || canAssign(grants, viewPermission);
  // A refetch that fails while data is cached keeps the data (TanStack Query), so the list, the count and the open rows
  // stay and the failure is shown above them.
  const loaded = !access.isLoading && grants !== undefined;
  const subjectCount = loaded ? grants.users.length + grants.roles.length : 0;
  let summary: string | undefined;
  if (loaded) {
    summary =
      subjectCount === 0
        ? t("Server_Settings_Summary_Access_None")
        : t("Server_Settings_Summary_Access", { count: subjectCount });
  }

  const subjects: AccessSubject[] =
    grants === undefined
      ? []
      : [
          ...grants.users.map((grant): AccessSubject => ({
            kind: "user",
            id: grant.userId,
            name: grant.nickname,
            permissions: grant.permissions,
            detail: grant.detail,
          })),
          ...grants.roles.map((grant): AccessSubject => ({
            kind: "role",
            id: String(grant.roleId),
            name: grant.title,
            permissions: grant.permissions,
            description: grant.description,
            memberCount: grant.memberCount,
          })),
        ];

  let list;
  if (access.isLoading) list = <LoadingState label={t("Server_Access_Loading")} />;
  else if (grants === undefined)
    list = (
      <ErrorState
        title={t("Server_Access_Error_Load")}
        retryLabel={t("Server_Settings_Retry")}
        onRetry={() => void refetch()}
      />
    );
  else if (subjectCount === 0)
    list = (
      <EmptyState title={t("Server_Access_Empty_Title")}>
        {t("Server_Access_Empty_Body")}
      </EmptyState>
    );
  else {
    // The filter is component state only and never part of the URL. Below the threshold nothing is ever filtered, also
    // when a text typed earlier is still held after removals shortened the list.
    const showFilter = subjectCount >= accessFilterThreshold;
    const needle = showFilter ? filter.trim().toLocaleLowerCase(currentLocale()) : "";
    const visible = subjects.filter((subject) =>
      matchesFilter(
        subject,
        t(subject.kind === "user" ? "Server_Access_Subject_Member" : "Server_Access_Subject_Role"),
        needle,
      ),
    );
    const clearFilter = () => {
      setFilter("");
      filterInputRef.current?.focus();
    };
    const rows =
      visible.length === 0 ? (
        <EmptyState
          title={t("Server_Access_Filter_EmptyTitle")}
          action={
            <ActionButton variant="ghost" onClick={clearFilter}>
              {t("Server_Access_Filter_Clear")}
            </ActionButton>
          }
        >
          {t("Server_Access_Filter_EmptyBody")}
        </EmptyState>
      ) : (
        <ul className="access-list">
          {visible.map((subject) => {
            const key = `${subject.kind}:${subject.id}`;
            const isSelf = subject.kind === "user" && subject.id === user?.primaryKey;
            return (
              <AccessSubjectRow
                key={key}
                subject={subject}
                isSelf={isSelf}
                open={openRows.has(key)}
                onOpenChange={(open) => setRowOpen(key, open)}
                failedPermissions={permissionsOfSubject(failedPairs, subject)}
                forbiddenPermissions={permissionsOfSubject(forbiddenPairs, subject)}
                pendingPermissions={permissionsOfSubject(pendingPairs, subject)}
                assignablePermissions={grants.assignablePermissions}
                onToggle={(permission, granted) =>
                  changePermission(subject, isSelf, permission, granted)
                }
                onRemoveAll={() => {
                  removeAll.reset();
                  setRemoval({ subject, isSelf });
                }}
              />
            );
          })}
        </ul>
      );
    list = (
      <>
        {showFilter && (
          <div className="access-filter">
            <SearchField
              label={t("Server_Access_Filter_Label")}
              placeholder={t("Server_Access_Filter_Placeholder")}
              clearLabel={t("Server_Access_Filter_Clear")}
              value={filter}
              onChange={setFilter}
              inputRef={filterInputRef}
            />
            <p className="access-filter__result" aria-live="polite" aria-atomic="true">
              {t("Server_Access_Filter_Result", { count: visible.length })}
            </p>
          </div>
        )}
        {rows}
      </>
    );
  }

  if (access.isError && grants !== undefined)
    list = (
      <>
        <ErrorState
          title={t("Server_Access_Error_Refresh")}
          retryLabel={t("Server_Settings_Retry")}
          onRetry={() => void refetch()}
        />
        {list}
      </>
    );

  /**
   * Revokes every stored permission of the subject of the open dialog. The permissions are read again from the
   * current list, because a refetch since the dialog opened may have changed what is stored. The dialog closes only
   * after the run and the refresh that follows it succeeded; a partial failure keeps it open with the alert.
   */
  const confirmRemoval = async () => {
    if (removal === null) return;
    const { subject } = removal;
    const current = subjects.find(
      (entry) => entry.kind === subject.kind && entry.id === subject.id,
    );
    try {
      await removeAll.mutateAsync({
        kind: subject.kind,
        subjectId: subject.id,
        name: subject.name,
        permissions: current?.permissions ?? [],
      });
    } catch {
      // The mutation state carries the error; the dialog shows it.
      return;
    }
    setRemoval(null);
    setAnnouncement(t("Server_Access_Announce_Removed", { name: subject.name }));
    setListFocus((serial) => serial + 1);
  };
  return (
    <SettingsPanel id={accessPanelId} title={t("Server_Settings_AccessTitle")} summary={summary}>
      <p className="access-intro">{t("Server_Access_Intro")}</p>
      <AccessGrantForm
        kind={kind}
        onKindChange={changeKind}
        query={query}
        onQueryChange={(value) => {
          setQuery(value);
          if (grant.isError) grant.reset();
        }}
        status={pickerStatus}
        options={options}
        hasMore={hasMore}
        selected={selected}
        onSelect={(option) => {
          setSelected(option);
          grant.reset();
        }}
        onClear={() => {
          setSelected(null);
          grant.reset();
        }}
        onRetry={() => void refetchCandidates()}
        pending={grant.isPending}
        canGiveView={canGiveView}
        errorText={grant.isError ? t(grantFailureKey(grant.error)) : undefined}
        onSubmit={submitGrant}
        inputRef={inputRef}
      />
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {announcement}
      </div>
      <h3 ref={listHeadingRef} tabIndex={-1} className="access-list-title">
        {t("Server_Access_List_Title")}
      </h3>
      {list}
      <ConfirmDialog
        open={selfRemoval !== null}
        title={t("Server_Access_Self_Title")}
        description={t("Server_Access_Self_Body", { server: server.title })}
        confirmLabel={t("Server_Access_Self_Confirm")}
        cancelLabel={t("Server_Access_Self_Dismiss")}
        confirmVariant="danger"
        dismissVariant="primary"
        initialFocus="dismiss"
        onConfirm={() => {
          if (selfRemoval === null) return;
          setSelfRemoval(null);
          void runToggle(selfRemoval.subject, selfRemoval.permission, false);
        }}
        onCancel={() => setSelfRemoval(null)}
      >
        {selfRemoval !== null && (
          <p>
            <bdi>{permissionLabel(selfRemoval.permission)}</bdi>
          </p>
        )}
      </ConfirmDialog>
      <ConfirmDialog
        open={removal !== null}
        title={
          removal?.isSelf ? t("Server_Access_Remove_TitleSelf") : t("Server_Access_Remove_Title")
        }
        description={
          removal?.isSelf
            ? t("Server_Access_Remove_BodySelf", { server: server.title })
            : t("Server_Access_Remove_Body", {
                name: removal?.subject.name ?? "",
                server: server.title,
              })
        }
        confirmLabel={
          removeAll.isPending ? t("Server_Access_Remove_Busy") : t("Server_Access_Remove_Confirm")
        }
        cancelLabel={t("Server_Access_Remove_Dismiss")}
        confirmVariant="danger"
        dismissVariant="primary"
        initialFocus="dismiss"
        busy={removeAll.isPending}
        error={removeAll.isError ? t(removeFailureKey(removeAll.error)) : undefined}
        onConfirm={() => void confirmRemoval()}
        onCancel={() => {
          removeAll.reset();
          setRemoval(null);
        }}
      />
    </SettingsPanel>
  );
}
