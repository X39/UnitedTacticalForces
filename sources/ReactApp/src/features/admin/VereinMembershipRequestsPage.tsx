import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { formatDateTime, formatUtcDate } from "@/app/format";
import { formatResource } from "@/app/i18n";
import type { TranslationKey } from "@/app/localizationTypes";
import { useAuth } from "@/auth/auth";
import { canManageVereinMemberships } from "@/auth/claims";
import { useAppToastOnChange } from "@/components/AppToasts";
import {
  Metric,
  PageHeader,
  StatePanel,
  StatusBadge,
  useUnsavedChanges,
} from "@/components/primitives";
import {
  createMembershipTransition,
  isOpenMembershipRequest,
  loadMembershipRequests,
  membershipRequestMatchesView,
  membershipRequestQueryKeys,
  sortMembershipRequests,
  transitionMembershipRequest,
  type MembershipRequest,
  type MembershipRequestStatus,
  type MembershipRequestView,
} from "@/features/admin/vereinMembershipRequests";

const statusOrder: MembershipRequestStatus[] = [
  "Submitted",
  "AcceptedForContact",
  "Approved",
  "Rejected",
  "Withdrawn",
];

const viewForStatus: Record<MembershipRequestStatus, MembershipRequestView> = {
  Submitted: "submitted",
  AcceptedForContact: "contact",
  Approved: "approved",
  Rejected: "rejected",
  Withdrawn: "withdrawn",
};

const statusLabels: Record<MembershipRequestStatus, TranslationKey> = {
  Submitted: "Pages_Public_Join_RequestStatus_Submitted",
  AcceptedForContact: "Pages_Public_Join_RequestStatus_AcceptedForContact",
  Approved: "Pages_Public_Join_RequestStatus_Approved",
  Rejected: "Pages_Public_Join_RequestStatus_Rejected",
  Withdrawn: "Pages_Public_Join_RequestStatus_Withdrawn",
};

const statusTones: Record<
  MembershipRequestStatus,
  "neutral" | "success" | "warning" | "danger" | "info"
> = {
  Submitted: "warning",
  AcceptedForContact: "info",
  Approved: "success",
  Rejected: "danger",
  Withdrawn: "neutral",
};

type TransitionStatus = Extract<
  MembershipRequestStatus,
  "AcceptedForContact" | "Approved" | "Rejected"
>;

interface DecisionDraft {
  requestId: number | null;
  reason: string;
  memberNumber: string;
  notes: string;
}

function emptyDecisionDraft(request?: MembershipRequest | null): DecisionDraft {
  return {
    requestId: request?.primaryKey ?? null,
    reason: request?.decisionReason ?? "",
    memberNumber: "",
    notes: "",
  };
}

function applicantName(request: MembershipRequest): string {
  return `${request.firstName} ${request.lastName}`.trim();
}

function address(request: MembershipRequest): string {
  return [
    request.addressLine1,
    request.addressLine2,
    `${request.postalCode} ${request.city}`.trim(),
    request.country,
  ]
    .filter(Boolean)
    .join(", ");
}

export function Component() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const authorized = canManageVereinMemberships(user);
  const [view, setView] = useState<MembershipRequestView>("open");
  const [searchDraft, setSearchDraft] = useState("");
  const [search, setSearch] = useState("");
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [draft, setDraft] = useState<DecisionDraft>(() => emptyDecisionDraft());
  const [commandError, setCommandError] = useState<string | null>(null);
  const query = useQuery({
    queryKey: [...membershipRequestQueryKeys.all, { search }],
    queryFn: ({ signal }) => loadMembershipRequests(search, signal),
    enabled: authorized,
  });
  const requests = useMemo(() => query.data ?? [], [query.data]);
  const counts = useMemo(
    () => ({
      all: requests.length,
      open: requests.filter(isOpenMembershipRequest).length,
      approved: requests.filter((request) => request.status === "Approved").length,
    }),
    [requests],
  );
  const visibleRequests = useMemo(
    () =>
      sortMembershipRequests(
        requests.filter((request) => membershipRequestMatchesView(request, view)),
      ),
    [requests, view],
  );
  const selected =
    requests.find((request) => request.primaryKey === selectedId) ?? visibleRequests[0] ?? null;
  const decision =
    selected && draft.requestId !== selected.primaryKey ? emptyDecisionDraft(selected) : draft;

  const dirty = Boolean(
    selected &&
    (decision.reason !== selected.decisionReason ||
      decision.memberNumber.trim().length ||
      decision.notes.trim().length),
  );
  useUnsavedChanges(dirty);

  const transition = useMutation({
    mutationFn: ({ status }: { status: TransitionStatus }) => {
      if (!selected) throw new Error(t("Pages_Command_Verein_MembershipRequests_NoSelectionTitle"));
      const payload = createMembershipTransition(
        status,
        decision.reason,
        decision.memberNumber,
        decision.notes,
      );
      if (!payload)
        throw new Error(t("Pages_Command_Verein_MembershipRequests_RejectReasonRequired"));
      return transitionMembershipRequest(selected.primaryKey, payload);
    },
    onMutate: () => {
      setCommandError(null);
    },
    onSuccess: (updated) => {
      queryClient.setQueryData<MembershipRequest[]>(
        [...membershipRequestQueryKeys.all, { search }],
        (current) =>
          (current ?? []).map((request) =>
            request.primaryKey === updated.primaryKey ? updated : request,
          ),
      );
      setSelectedId(updated.primaryKey);
      setDraft(emptyDecisionDraft(updated));
    },
    onError: (error) => {
      setCommandError(
        formatResource(t("Pages_Command_Verein_MembershipRequests_TransitionError"), error.message),
      );
    },
  });
  useAppToastOnChange(
    transition.isSuccess
      ? t("Pages_Command_Verein_MembershipRequests_TransitionSuccess")
      : undefined,
  );
  useAppToastOnChange(commandError, "error");

  const applySearch = (event: FormEvent) => {
    event.preventDefault();
    setSearch(searchDraft.trim());
  };
  const select = (request: MembershipRequest) => {
    setSelectedId(request.primaryKey);
    setDraft(emptyDecisionDraft(request));
    setCommandError(null);
    transition.reset();
  };
  const selectView = (nextView: MembershipRequestView) => {
    setView(nextView);
    if (selected && membershipRequestMatchesView(selected, nextView)) return;
    const next = sortMembershipRequests(
      requests.filter((request) => membershipRequestMatchesView(request, nextView)),
    )[0];
    setSelectedId(next?.primaryKey ?? null);
    setDraft(emptyDecisionDraft(next));
  };
  const countForView = (candidate: MembershipRequestView) =>
    requests.filter((request) => membershipRequestMatchesView(request, candidate)).length;
  const canTransition = Boolean(selected && isOpenMembershipRequest(selected));
  const canSubmit = (status: TransitionStatus) =>
    Boolean(
      selected &&
      decision.reason.trim() &&
      !transition.isPending &&
      isOpenMembershipRequest(selected) &&
      (status !== "AcceptedForContact" || selected.status === "Submitted"),
    );

  if (!authorized)
    return (
      <StatePanel
        kind="forbidden"
        title={t("Pages_Command_Verein_MembershipRequests_AccessRequiredTitle")}
      >
        {t("Pages_Command_Verein_MembershipRequests_AccessRequiredDescription")}
      </StatePanel>
    );

  return (
    <section className="verein-membership-requests-page">
      <PageHeader
        kicker={t("Shared_CommandCenter_Nav_VereinMembershipRequests")}
        title={t("Pages_Command_Verein_MembershipRequests_PageTitle")}
        description={t("Pages_Command_Verein_MembershipRequests_PageDescription")}
        actions={
          <>
            <button
              className="button button--ghost"
              type="button"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t("Pages_Command_Verein_ActionRefresh")}
            </button>
            <Link className="button button--ghost" to="/verein">
              {t("Pages_Command_Verein_MembershipRequests_PublicHub")}
            </Link>
          </>
        }
      />

      {query.isError && (
        <StatePanel kind="error">
          {formatResource(
            t("Pages_Command_Verein_MembershipRequests_LoadError"),
            query.error.message,
          )}
        </StatePanel>
      )}

      <section
        className="command-metrics verein-membership-requests-metrics"
        aria-label={t("Pages_Command_Verein_MembershipRequests_MetricsAria")}
      >
        <button
          type="button"
          className={view === "all" ? "active" : undefined}
          aria-pressed={view === "all"}
          onClick={() => selectView("all")}
        >
          <Metric
            label={t("Pages_Command_Verein_MembershipRequests_MetricLoadedStatus")}
            value={counts.all}
            detail={t("Pages_Command_Verein_MembershipRequests_MetricLoadedDescription")}
          />
        </button>
        <button
          type="button"
          className={view === "open" ? "active" : undefined}
          aria-pressed={view === "open"}
          onClick={() => selectView("open")}
        >
          <Metric
            label={t("Pages_Command_Verein_MembershipRequests_MetricOpenStatus")}
            value={counts.open}
            detail={t("Pages_Command_Verein_MembershipRequests_MetricOpenDescription")}
          />
        </button>
        <button
          type="button"
          className={view === "approved" ? "active" : undefined}
          aria-pressed={view === "approved"}
          onClick={() => selectView("approved")}
        >
          <Metric
            label={t("Pages_Command_Verein_MembershipRequests_MetricApprovedStatus")}
            value={counts.approved}
            detail={t("Pages_Command_Verein_MembershipRequests_MetricApprovedDescription")}
          />
        </button>
      </section>

      <form
        className="toolbar verein-membership-requests-toolbar"
        aria-label={t("Pages_Command_Verein_MembershipRequests_ToolbarAria")}
        onSubmit={applySearch}
      >
        <label className="search-field">
          <span className="sr-only">
            {t("Pages_Command_Verein_MembershipRequests_SearchPlaceholder")}
          </span>
          <input
            value={searchDraft}
            placeholder={t("Pages_Command_Verein_MembershipRequests_SearchPlaceholder")}
            onChange={(event) => setSearchDraft(event.target.value)}
          />
        </label>
        <button className="button button--ghost" type="submit" disabled={query.isFetching}>
          {t("Pages_Command_Verein_MembershipRequests_ApplySearch")}
        </button>
        <div className="filter-chips">
          <button
            type="button"
            className={view === "all" ? "active" : undefined}
            aria-pressed={view === "all"}
            onClick={() => selectView("all")}
          >
            {t("Pages_Command_Verein_MembershipRequests_FilterAll")} <span>{requests.length}</span>
          </button>
          <button
            type="button"
            className={view === "open" ? "active" : undefined}
            aria-pressed={view === "open"}
            onClick={() => selectView("open")}
          >
            {t("Pages_Command_Verein_MembershipRequests_MetricOpenStatus")}{" "}
            <span>{counts.open}</span>
          </button>
          {statusOrder.map((status) => {
            const statusView = viewForStatus[status];
            return (
              <button
                key={status}
                type="button"
                className={view === statusView ? "active" : undefined}
                aria-pressed={view === statusView}
                onClick={() => selectView(statusView)}
              >
                {t(statusLabels[status])} <span>{countForView(statusView)}</span>
              </button>
            );
          })}
        </div>
      </form>

      {query.isPending ? (
        <StatePanel kind="loading">{t("Pages_Command_Verein_Loading")}</StatePanel>
      ) : (
        <section className="verein-membership-request-workspace">
          <section className="command-panel verein-membership-request-list">
            <header className="command-panel__header">
              <span className="kicker">
                {t("Pages_Command_Verein_MembershipRequests_ListKicker")}
              </span>
              <h2>{t("Pages_Command_Verein_MembershipRequests_ListTitle")}</h2>
              <StatusBadge>{visibleRequests.length}</StatusBadge>
            </header>
            {visibleRequests.length === 0 ? (
              <StatePanel>{t("Pages_Command_Verein_MembershipRequests_NoRows")}</StatePanel>
            ) : (
              <div className="command-table-scroll">
                <table className="command-table verein-membership-request-table">
                  <thead>
                    <tr>
                      <th>{t("Pages_Command_Verein_MembershipRequests_HeaderApplicant")}</th>
                      <th>{t("Pages_Command_Verein_MembershipRequests_HeaderStatus")}</th>
                      <th>{t("Pages_Command_Verein_MembershipRequests_HeaderSubmitted")}</th>
                      <th>{t("Pages_Command_Verein_MembershipRequests_HeaderUpdated")}</th>
                      <th>{t("Pages_Command_Verein_MembershipRequests_HeaderAction")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleRequests.map((request) => (
                      <tr
                        key={request.primaryKey}
                        className={selectedId === request.primaryKey ? "is-selected" : undefined}
                      >
                        <th scope="row">
                          <strong>{applicantName(request)}</strong>
                          <span>{request.userNickname || request.userFk}</span>
                        </th>
                        <td>
                          <StatusBadge tone={statusTones[request.status]}>
                            {t(statusLabels[request.status])}
                          </StatusBadge>
                        </td>
                        <td>{formatDateTime(request.timeStampCreated)}</td>
                        <td>{formatDateTime(request.timeStampUpdated)}</td>
                        <td>
                          <button
                            className="button button--ghost"
                            type="button"
                            onClick={() => select(request)}
                          >
                            {t("Pages_Command_Verein_MembershipRequests_Open")}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <aside
            className="command-panel verein-membership-request-detail"
            aria-label={t("Pages_Command_Verein_MembershipRequests_DetailAria")}
          >
            {!selected ? (
              <StatePanel title={t("Pages_Command_Verein_MembershipRequests_NoSelectionTitle")}>
                {t("Pages_Command_Verein_MembershipRequests_NoSelectionDescription")}
              </StatePanel>
            ) : (
              <>
                <header className="command-panel__header verein-membership-request-detail__header">
                  <span className="kicker">
                    {t("Pages_Command_Verein_MembershipRequests_DetailAria")}
                  </span>
                  <h2>{applicantName(selected)}</h2>
                  <p>{selected.userNickname}</p>
                  <StatusBadge tone={statusTones[selected.status]}>
                    {t(statusLabels[selected.status])}
                  </StatusBadge>
                </header>

                <dl className="verein-membership-request-meta">
                  <div>
                    <dt>{t("Pages_Command_Verein_MembershipRequests_DetailBirthday")}</dt>
                    <dd>{formatUtcDate(selected.dateOfBirth)}</dd>
                  </div>
                  <div>
                    <dt>{t("Pages_Command_Verein_MembershipRequests_DetailEmail")}</dt>
                    <dd>
                      <a href={`mailto:${selected.email}`}>{selected.email}</a>
                    </dd>
                  </div>
                  <div>
                    <dt>{t("Pages_Command_Verein_MembershipRequests_DetailPhone")}</dt>
                    <dd>
                      <a href={`tel:${selected.phoneNumber}`}>{selected.phoneNumber}</a>
                    </dd>
                  </div>
                  <div>
                    <dt>{t("Pages_Command_Verein_MembershipRequests_DetailAddress")}</dt>
                    <dd>{address(selected)}</dd>
                  </div>
                  <div>
                    <dt>{t("Pages_Command_Verein_MembershipRequests_DetailSubmitted")}</dt>
                    <dd>{formatDateTime(selected.timeStampCreated)}</dd>
                  </div>
                  <div>
                    <dt>{t("Pages_Command_Verein_MembershipRequests_DetailReviewedBy")}</dt>
                    <dd>
                      {selected.reviewedByName ||
                        t("Pages_Command_Verein_MembershipRequests_NotReviewed")}
                    </dd>
                  </div>
                </dl>

                <section className="verein-membership-request-section">
                  <span className="kicker">
                    {t("Pages_Command_Verein_MembershipRequests_MotivationKicker")}
                  </span>
                  <p>{selected.motivation}</p>
                </section>

                <section className="verein-membership-request-section">
                  <span className="kicker">
                    {t("Pages_Command_Verein_MembershipRequests_AcknowledgementsKicker")}
                  </span>
                  <ul className="verein-membership-request-ack-list">
                    {(
                      [
                        [
                          selected.acknowledgesMembershipSeparation,
                          "Pages_Command_Verein_MembershipRequests_AckSeparation",
                        ],
                        [
                          selected.acknowledgesDataProcessing,
                          "Pages_Command_Verein_MembershipRequests_AckData",
                        ],
                        [
                          selected.acknowledgesSatzung,
                          "Pages_Command_Verein_MembershipRequests_AckSatzung",
                        ],
                      ] as const
                    ).map(([acknowledged, label]) => (
                      <li key={label}>
                        <StatusBadge tone={acknowledged ? "success" : "danger"}>
                          {t(
                            acknowledged
                              ? "Pages_Command_Verein_MembershipRequests_AckYes"
                              : "Pages_Command_Verein_MembershipRequests_AckNo",
                          )}
                        </StatusBadge>
                        <span>{t(label)}</span>
                      </li>
                    ))}
                  </ul>
                </section>

                {selected.decisionReason && !canTransition && (
                  <section className="verein-membership-request-section">
                    <span className="kicker">
                      {t("Pages_Command_Verein_MembershipRequests_DecisionKicker")}
                    </span>
                    <p>{selected.decisionReason}</p>
                  </section>
                )}

                {canTransition && (
                  <section className="verein-membership-request-form">
                    <label>
                      <span>{t("Pages_Command_Verein_MembershipRequests_Reason")}</span>
                      <textarea
                        rows={4}
                        maxLength={512}
                        value={decision.reason}
                        onChange={(event) => setDraft({ ...decision, reason: event.target.value })}
                      />
                    </label>
                    <label>
                      <span>{t("Pages_Command_Verein_MembershipRequests_MemberNumber")}</span>
                      <input
                        maxLength={64}
                        value={decision.memberNumber}
                        onChange={(event) =>
                          setDraft({ ...decision, memberNumber: event.target.value })
                        }
                      />
                    </label>
                    <label>
                      <span>{t("Pages_Command_Verein_MembershipRequests_Notes")}</span>
                      <textarea
                        rows={3}
                        maxLength={2000}
                        value={decision.notes}
                        onChange={(event) => setDraft({ ...decision, notes: event.target.value })}
                      />
                    </label>
                    <div className="page-header__actions">
                      <button
                        className="button button--ghost"
                        type="button"
                        disabled={!canSubmit("AcceptedForContact")}
                        onClick={() => transition.mutate({ status: "AcceptedForContact" })}
                      >
                        {t("Pages_Command_Verein_MembershipRequests_AcceptForContact")}
                      </button>
                      <button
                        className="button button--primary"
                        type="button"
                        disabled={!canSubmit("Approved")}
                        onClick={() => transition.mutate({ status: "Approved" })}
                      >
                        {t("Pages_Command_Verein_MembershipRequests_Approve")}
                      </button>
                      <button
                        className="button button--danger"
                        type="button"
                        disabled={!canSubmit("Rejected")}
                        onClick={() => transition.mutate({ status: "Rejected" })}
                      >
                        {t("Pages_Command_Verein_MembershipRequests_Reject")}
                      </button>
                    </div>
                  </section>
                )}
              </>
            )}
          </aside>
        </section>
      )}
    </section>
  );
}
