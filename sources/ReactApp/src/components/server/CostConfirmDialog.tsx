import { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Disclosure } from "@/components/Disclosure";
import { ConfirmDialog } from "@/components/primitives";
import { ApiText } from "@/components/server/ApiText";
import {
  buildCostConfirmationFacts,
  type CostConfirmationFacts,
} from "@/components/server/costConfirmationModel";
import { UnavailableReason } from "@/components/server/UnavailableReason";
import { operationPendingReason } from "@/components/server/unavailableReasonMapping";
import {
  confirmationCopyKeysFor,
  confirmVariantFor,
  dismissKeyFor,
  lookupActionVocabulary,
  pendingKeyFor,
} from "@/features/hosting/actionVocabulary";
import { formatAccruedAmount } from "@/features/hosting/accruedModel";
import { formatInstant } from "@/features/hosting/costAlertFormat";
import { formatHourlyRate, formatMoney } from "@/features/hosting/model";
import { deriveServerState } from "@/features/hosting/serverState";
import type { GameServer, ServerAction } from "@/features/hosting/types";
import { useAccruedLive } from "@/features/hosting/useAccruedLive";
import "@/components/server/server.css";

/** Props of {@link CostConfirmDialog}. */
export interface CostConfirmDialogProps {
  /** The server the action runs on. */
  server: GameServer;
  /** The action that needs confirmation. */
  action: ServerAction;
  /** True while the action request is in flight. */
  busy: boolean;
  /** True after the request failed; the dialog shows the localized request-error copy. */
  requestFailed: boolean;
  /** True when the live server data says the action is no longer available. */
  stale: boolean;
  /** Called with the challenge text (or nothing) when the user confirms. */
  onConfirm(confirmation: string | undefined): void;
  /** Called when the user dismisses the dialog. */
  onDismiss(): void;
}

function CostRow({ facts }: { facts: CostConfirmationFacts }) {
  const { t } = useTranslation();
  if (facts.costRow === "hidden") return null;
  let value: ReactNode;
  switch (facts.costRow) {
    case "estimate":
      value =
        facts.category === "stops" ? (
          <span className="cost-confirm__amount">
            {t("Server_Confirm_CostStopHourly", { hourly: formatHourlyRate(facts.hourly ?? 0) })}
          </span>
        ) : (
          <>
            <span className="cost-confirm__amount">
              {t("Server_Confirm_CostHourlyStart", { hourly: formatHourlyRate(facts.hourly ?? 0) })}
            </span>
            {facts.monthly !== null && (
              <span className="cost-confirm__secondary">
                {t("Server_Confirm_CostMonthly", { monthly: formatMoney(facts.monthly) })}
              </span>
            )}
          </>
        );
      break;
    case "unavailable":
      value = t("Server_Confirm_CostUnavailable");
      break;
    case "keeps-running":
      value = t("Server_Confirm_CostKeepsRunning");
      break;
    case "unknown":
      value = t("Server_Confirm_CostUnknown");
      break;
  }
  return (
    <div className="cost-confirm__fact">
      <dt>{t("Server_Confirm_Fact_Cost")}</dt>
      <dd>{value}</dd>
    </div>
  );
}

/**
 * The session so far, for actions that stop cost. It shows only when the control plane knows the amount, with the
 * time the amount was true and the same formatting as the workspace strip, so the two cannot disagree. A start
 * action, a server without a session and an unknown amount render nothing.
 */
function AccruedRow({ serverId }: { serverId: string }) {
  const { t } = useTranslation();
  const accrued = useAccruedLive(serverId);
  const session = accrued.data?.session;
  if (!session || session.amount === null) return null;
  return (
    <div className="cost-confirm__fact">
      <dt>{t("Server_Workspace_Cost_Accrued")}</dt>
      <dd>
        {t("Server_Confirm_Fact_Accrued", {
          amount: formatAccruedAmount(session.amount, session.currency, true, t),
          time: formatInstant(session.asOf),
        })}
      </dd>
    </div>
  );
}

function OwnerRow({ facts }: { facts: CostConfirmationFacts }) {
  const { t } = useTranslation();
  const { owner } = facts;
  let value: string;
  if (owner.kind === "name") value = owner.name ?? "";
  else if (owner.kind === "hidden") value = t("Server_Confirm_OwnerHidden");
  else value = t("Server_Confirm_OwnerUnknown");
  return (
    <div className="cost-confirm__fact">
      <dt>{t("Server_Confirm_Fact_Owner")}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function StopRow({ facts }: { facts: CostConfirmationFacts }) {
  const { t } = useTranslation();
  let value: string;
  if (facts.howToStop === "start-text") value = t("Server_Confirm_StopHowStart");
  else if (facts.minutes !== null)
    value = t("Server_Confirm_StopHowStop", { minutes: facts.minutes });
  else value = t("Server_Confirm_StopHowStopNoMinutes");
  return (
    <div className="cost-confirm__fact">
      <dt>{t("Server_Confirm_Fact_Stop")}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function ServerNote({ action }: { action: ServerAction }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const texts = [action.confirmationText, action.description, action.costDescription].filter(
    (text): text is string => typeof text === "string" && text.trim() !== "",
  );
  if (texts.length === 0) return null;
  return (
    <Disclosure
      className="cost-confirm__note"
      title={t("Server_Confirm_ServerNote")}
      open={open}
      onOpenChange={setOpen}
    >
      <ApiText>{texts.join(" ")}</ApiText>
    </Disclosure>
  );
}

/**
 * The one confirmation dialog for cost-bearing and other lifecycle actions.
 *
 * The dialog is always open; the provider mounts it only while a confirmation request is active. It is a user
 * experience guard only: the API validates permission, challenge and pending state again.
 */
export function CostConfirmDialog({
  server,
  action,
  busy,
  requestFailed,
  stale,
  onConfirm,
  onDismiss,
}: CostConfirmDialogProps) {
  const { t } = useTranslation();
  const facts = buildCostConfirmationFacts(server, action);
  const vocabulary = lookupActionVocabulary(action.id);
  const copy = confirmationCopyKeysFor(action.id);
  const derived = deriveServerState(server);

  let title: string;
  if (copy.dedicated) title = t(copy.titleKey, { name: server.title });
  else if (vocabulary)
    title = t(copy.titleKey, { action: t(vocabulary.label), name: server.title });
  else title = t("Server_Confirm_Title_Unknown", { name: server.title });
  const description = copy.dedicated
    ? t(copy.bodyKey, { name: server.title })
    : t("Server_Confirm_Body_Other");

  let confirmLabel: ReactNode;
  if (busy) confirmLabel = t(pendingKeyFor(action.id));
  else if (vocabulary) confirmLabel = t(vocabulary.label);
  else confirmLabel = <ApiText>{action.label || action.id}</ApiText>;

  const staleReason =
    action.unavailableReason || (action.pending ? operationPendingReason : undefined);
  const challenge = action.confirmationChallenge || undefined;
  return (
    <ConfirmDialog
      open
      className="dialog--cost"
      kicker={t("Server_Confirm_Kicker")}
      title={title}
      description={description}
      bodyLabel={t("Server_Confirm_Kicker")}
      confirmLabel={confirmLabel}
      cancelLabel={t(dismissKeyFor(action.id))}
      confirmVariant={confirmVariantFor(action.id)}
      initialFocus="dismiss"
      busy={busy}
      {...(challenge ? { challenge, challengeHelper: t("Server_Confirm_ChallengeHelper") } : {})}
      {...(requestFailed ? { error: t("Server_Confirm_RequestError") } : {})}
      confirmDisabled={stale}
      onConfirm={() => onConfirm(challenge)}
      onCancel={onDismiss}
    >
      <dl className="cost-confirm__facts">
        <CostRow facts={facts} />
        {facts.category === "stops" && <AccruedRow serverId={server.primaryKey} />}
        <OwnerRow facts={facts} />
        <StopRow facts={facts} />
      </dl>
      {!copy.dedicated && <ServerNote action={action} />}
      {stale && (
        <p className="cost-confirm__stale">
          <UnavailableReason
            reason={staleReason}
            stateLabel={t(derived.labelKey)}
            launchDisabled={
              action.id.toLowerCase() === "start" ? server.isLaunchDisabled : undefined
            }
          />
        </p>
      )}
    </ConfirmDialog>
  );
}
