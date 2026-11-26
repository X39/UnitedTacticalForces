import {
  ArrowSyncCircle20Regular,
  Cloud20Regular,
  DocumentSettings20Regular,
  History20Regular,
  ReceiptMoney20Regular,
  Server20Regular,
  ShieldLock20Regular,
} from "@fluentui/react-icons";
import { useId, useState } from "react";
import { useTranslation } from "react-i18next";
import { currentLocale } from "@/app/format";
import type { TranslationKey } from "@/app/localizationTypes";
import { Disclosure } from "@/components/Disclosure";
import { StatusBadge } from "@/components/primitives";
import { EmptyState, LoadingState } from "@/components/StateViews";
import { humanizeToken } from "@/features/hosting/model";
import type { HostingOperation } from "@/features/hosting/types";

export type HostingAuditTone = "neutral" | "success" | "warning" | "danger" | "info";

type HostingAuditCategory =
  "lifecycle" | "worker" | "cloud" | "configuration" | "security" | "finance";

interface AuditEvidence {
  label: string;
  value: string;
}

export interface ParsedHostingAuditDetail {
  narrative: string | null;
  failure: string | null;
  fields: AuditEvidence[];
}

const structuredDetailKeyPattern = /^[a-z][a-z\d _.()-]{0,48}$/i;
const failureDetailKeyPattern = /^(failure|failure reason|error|fault)$/i;

const categoryTranslationKeys: Record<HostingAuditCategory, TranslationKey> = {
  lifecycle: "React_ServerAudit_CategoryLifecycle",
  worker: "React_ServerAudit_CategoryWorker",
  cloud: "React_ServerAudit_CategoryCloud",
  configuration: "React_ServerAudit_CategoryConfiguration",
  security: "React_ServerAudit_CategorySecurity",
  finance: "React_ServerAudit_CategoryFinance",
};

const statusTranslationKeys: Record<HostingAuditTone, TranslationKey> = {
  neutral: "React_ServerAudit_StatusRecorded",
  success: "React_ServerAudit_StatusSucceeded",
  warning: "React_ServerAudit_StatusWarning",
  danger: "React_ServerAudit_StatusFailed",
  info: "React_ServerAudit_StatusInProgress",
};

/**
 * Separates the API's legacy semicolon-delimited evidence from human-readable audit prose.
 */
export function parseHostingAuditDetail(
  detail: string | null | undefined,
): ParsedHostingAuditDetail {
  if (!detail?.trim()) return { narrative: null, failure: null, fields: [] };

  const narrative: string[] = [];
  const fields: AuditEvidence[] = [];
  let failure: string | null = null;

  for (const rawPart of detail.split(";")) {
    const part = rawPart.trim();
    if (!part) continue;

    const separatorIndex = part.indexOf("=");
    const key = separatorIndex > 0 ? part.slice(0, separatorIndex).trim() : "";
    const value = separatorIndex > 0 ? part.slice(separatorIndex + 1).trim() : "";
    if (!key || !value || !structuredDetailKeyPattern.test(key)) {
      narrative.push(part);
      continue;
    }

    if (failureDetailKeyPattern.test(key)) {
      if (!/^(none|null|n\/a)$/i.test(value)) failure = value;
      continue;
    }
    fields.push({ label: humanizeToken(key), value });
  }

  return {
    narrative: narrative.length > 0 ? narrative.join("; ") : null,
    failure,
    fields,
  };
}

/**
 * Maps audit outcomes to the shared status tones used throughout the command center.
 */
export function hostingAuditTone(operation: HostingOperation): HostingAuditTone {
  const status = operation.status.toLowerCase();
  const kind = operation.kind.toLowerCase();
  if (/fail|error|denied/.test(status) || kind.includes("failed")) return "danger";
  if (/warn|degraded/.test(status) || kind.includes("degraded")) return "warning";
  if (/pending|queued|running|requested|started|claimed|provisioning/.test(status)) return "info";
  if (/success|succeed|complete|recovered|released|stopped|terminated/.test(status))
    return "success";
  return "neutral";
}

function hostingAuditCategory(operation: HostingOperation): HostingAuditCategory {
  const kind = operation.kind.toLowerCase();
  if (/cost|ledger|payment|refund|allowance|finance/.test(kind)) return "finance";
  if (/access|secret|credential/.test(kind)) return "security";
  if (/configuration|preservation|file/.test(kind)) return "configuration";
  if (/worker|command/.test(kind)) return "worker";
  if (/deployment|provider|resource|cleanup|dangling|reconciliation/.test(kind)) return "cloud";
  return "lifecycle";
}

function AuditCategoryIcon({ category }: { category: HostingAuditCategory }) {
  switch (category) {
    case "worker":
      return <Server20Regular />;
    case "cloud":
      return <Cloud20Regular />;
    case "configuration":
      return <DocumentSettings20Regular />;
    case "security":
      return <ShieldLock20Regular />;
    case "finance":
      return <ReceiptMoney20Regular />;
    case "lifecycle":
      return <ArrowSyncCircle20Regular />;
    default:
      return <History20Regular />;
  }
}

function dateValue(value: string): Date | null {
  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? null : date;
}

function formatAuditDay(value: string): string {
  const date = dateValue(value);
  if (!date) return value;
  return new Intl.DateTimeFormat(currentLocale(), { dateStyle: "full" }).format(date);
}

function formatAuditTime(value: string): string {
  const date = dateValue(value);
  if (!date) return value;
  return new Intl.DateTimeFormat(currentLocale(), { timeStyle: "short" }).format(date);
}

function formatAuditMoment(value: string): string {
  const date = dateValue(value);
  if (!date) return value;
  return new Intl.DateTimeFormat(currentLocale(), {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

function normalizedEvidenceKey(value: string): string {
  return value.toLowerCase().replace(/[^a-z\d]/g, "");
}

function isProjectedEvidence(field: AuditEvidence, operation: HostingOperation): boolean {
  const key = normalizedEvidenceKey(field.label);
  if (key === "provider" && operation.providerIdentifier) return true;
  if ((key === "kind" || key === "resourcekind") && operation.resourceKind) return true;
  if (
    (key === "identity" || key === "resource" || key === "resourceid") &&
    operation.resourceIdentifier
  )
    return true;
  if ((key === "state" || key === "stage" || key === "providerstate") && operation.providerState)
    return true;
  return false;
}

/**
 * Renders the technical evidence of one hosting operation as a collapsible list of identifiers and parsed detail.
 *
 * The record id, event kind, status code and every id the operation carries come first, followed by the parsed
 * detail fields that the operation does not already project. The audit feed and the expanded audit entries of the
 * Activity section both render it, so the evidence stays identical wherever it is shown.
 *
 * @param props.operation The hosting operation whose evidence is listed.
 */
export function HostingAuditEvidence({ operation }: { operation: HostingOperation }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const detail = parseHostingAuditDetail(operation.detail);
  const evidence: AuditEvidence[] = [
    { label: t("React_ServerAudit_RecordId"), value: operation.primaryKey },
    { label: t("React_ServerAudit_EventKind"), value: operation.kind },
    { label: t("React_ServerAudit_StatusCode"), value: operation.status },
    ...(operation.resourceIdentifier
      ? [{ label: t("React_ServerAudit_ResourceId"), value: operation.resourceIdentifier }]
      : []),
    ...(operation.deploymentId
      ? [{ label: t("React_ServerAudit_DeploymentId"), value: operation.deploymentId }]
      : []),
    ...(operation.lifecycleJobId
      ? [{ label: t("React_ServerAudit_LifecycleJobId"), value: operation.lifecycleJobId }]
      : []),
    ...(operation.workerId
      ? [{ label: t("React_ServerAudit_WorkerId"), value: operation.workerId }]
      : []),
    ...(operation.traceId
      ? [{ label: t("React_ServerAudit_TraceId"), value: operation.traceId }]
      : []),
    ...(operation.spanId
      ? [{ label: t("React_ServerAudit_SpanId"), value: operation.spanId }]
      : []),
    ...detail.fields.filter((field) => !isProjectedEvidence(field, operation)),
  ];
  return (
    <Disclosure
      className="next-hosting-audit-evidence"
      title={
        <>
          {t("React_ServerAudit_TechnicalEvidence")}
          <small>{evidence.length}</small>
        </>
      }
      open={open}
      onOpenChange={setOpen}
    >
      <dl>
        {evidence.map((field, index) => (
          <div key={`${field.label}-${index}`}>
            <dt>{field.label}</dt>
            <dd>
              <code>{field.value}</code>
            </dd>
          </div>
        ))}
      </dl>
    </Disclosure>
  );
}

function HostingAuditEntry({
  operation,
  compact,
}: {
  operation: HostingOperation;
  compact: boolean;
}) {
  const { t } = useTranslation();
  const tone = hostingAuditTone(operation);
  const category = hostingAuditCategory(operation);
  const detail = parseHostingAuditDetail(operation.detail);
  const Heading = compact ? "h3" : "h4";
  const noticeTone =
    tone === "warning" ? "warning" : tone === "danger" || detail.failure ? "danger" : null;
  const notice = detail.failure ?? (noticeTone ? detail.narrative : null);
  const narrative = detail.narrative && detail.narrative !== notice ? detail.narrative : null;

  return (
    <article
      className={`next-hosting-audit-entry${compact ? " next-hosting-audit-entry--compact" : ""}`}
      data-tone={tone}
    >
      <div className="next-hosting-audit-entry__rail" aria-hidden="true">
        <span data-category={category}>
          <AuditCategoryIcon category={category} />
        </span>
      </div>
      <div className="next-hosting-audit-entry__body">
        <header className="next-hosting-audit-entry__header">
          <div className="next-hosting-audit-entry__title">
            <span>{t(categoryTranslationKeys[category])}</span>
            <Heading>{operation.summary || humanizeToken(operation.kind)}</Heading>
          </div>
          <div className="next-hosting-audit-entry__outcome">
            <StatusBadge tone={tone}>{t(statusTranslationKeys[tone])}</StatusBadge>
            <time
              dateTime={operation.timeStampCreated}
              title={formatAuditMoment(operation.timeStampCreated)}
            >
              {compact
                ? formatAuditMoment(operation.timeStampCreated)
                : formatAuditTime(operation.timeStampCreated)}
            </time>
          </div>
        </header>

        <dl className="next-hosting-audit-context">
          <div>
            <dt>{t("React_ServerAudit_Source")}</dt>
            <dd>{operation.providerIdentifier || t("React_ServerAudit_ControlPlane")}</dd>
          </div>
          {operation.resourceKind && (
            <div>
              <dt>{t("React_ServerAudit_ResourceType")}</dt>
              <dd>{humanizeToken(operation.resourceKind)}</dd>
            </div>
          )}
          {operation.providerState && (
            <div>
              <dt>{t("React_ServerAudit_ProviderState")}</dt>
              <dd>{humanizeToken(operation.providerState)}</dd>
            </div>
          )}
        </dl>

        {narrative && <p>{narrative}</p>}
        {notice && noticeTone && (
          <div className="next-hosting-audit-notice" data-tone={noticeTone}>
            <strong>
              {t(
                noticeTone === "warning"
                  ? "React_ServerAudit_StatusWarning"
                  : "React_ServerAudit_Failure",
              )}
            </strong>
            <span>{notice}</span>
          </div>
        )}

        {!compact && <HostingAuditEvidence operation={operation} />}
      </div>
    </article>
  );
}

function sortedOperations(operations: HostingOperation[]): HostingOperation[] {
  return [...operations].sort((left, right) => {
    const leftTime = dateValue(left.timeStampCreated)?.valueOf() ?? 0;
    const rightTime = dateValue(right.timeStampCreated)?.valueOf() ?? 0;
    return rightTime - leftTime;
  });
}

/**
 * Renders hosting audit records as a compact activity feed or a date-grouped evidence trail.
 */
export function HostingAuditFeed({
  operations,
  compact = false,
  loading = false,
}: {
  operations: HostingOperation[];
  compact?: boolean;
  loading?: boolean;
}) {
  const { t } = useTranslation();
  const feedId = useId();
  if (loading) return <LoadingState label={t("React_ServerAudit_Loading")} />;
  if (operations.length === 0)
    return (
      <EmptyState title={t("Hosting_Detail_OperationsEmptyTitle")}>
        {t("Hosting_Detail_OperationsEmptyBody")}
      </EmptyState>
    );

  const sorted = sortedOperations(operations);
  if (compact)
    return (
      <ol className="next-hosting-audit-list next-hosting-audit-list--compact">
        {sorted.map((operation) => (
          <li key={operation.primaryKey}>
            <HostingAuditEntry operation={operation} compact />
          </li>
        ))}
      </ol>
    );

  const groups = new Map<string, HostingOperation[]>();
  for (const operation of sorted) {
    const day = formatAuditDay(operation.timeStampCreated);
    groups.set(day, [...(groups.get(day) ?? []), operation]);
  }
  const recordLabel = t(
    operations.length === 1
      ? "Pages_Command_Audit_Count_Singular_AuditRecord"
      : "Pages_Command_Audit_Count_Plural_AuditRecord",
  );

  return (
    <div className="next-hosting-audit-feed">
      <header className="next-hosting-audit-feed__summary">
        <div>
          <strong>{operations.length}</strong>
          <span>{recordLabel}</span>
        </div>
        <span>{t("React_ServerAudit_NewestFirst")}</span>
      </header>
      <div className="next-hosting-audit-days">
        {[...groups.entries()].map(([day, dayOperations], groupIndex) => {
          const headingId = `${feedId}-day-${groupIndex}`;
          return (
            <section className="next-hosting-audit-day" aria-labelledby={headingId} key={day}>
              <h3 id={headingId}>{day}</h3>
              <ol className="next-hosting-audit-list">
                {dayOperations.map((operation) => (
                  <li key={operation.primaryKey}>
                    <HostingAuditEntry operation={operation} compact={false} />
                  </li>
                ))}
              </ol>
            </section>
          );
        })}
      </div>
    </div>
  );
}
