import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useId, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { CopyButton } from "@/components/CopyButton";
import { TextField } from "@/components/FormFields";
import { StatePanel, StatusBadge } from "@/components/primitives";
import { ApiText } from "@/components/server/ApiText";
import { SettingsPanel } from "@/components/server/SettingsPanel";
import { useSettingsPanelState } from "@/components/server/useSettingsPanelState";
import "@/components/server/credentials.css";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type {
  GameServer,
  GameServerSecretStatus,
  GameServerSecretValue,
} from "@/features/hosting/types";
import { useServerChangeDraft } from "@/features/hosting/useServerChangeDraft";
import {
  credentialValueIssue,
  maxCredentialValueLength,
  type SecretValueIssue,
  type StagedSecret,
} from "@/features/hosting/serverChangeDraft";

/** The number of bullets the masked run shows; fixed, so it never reveals the length of the stored value. */
const maskedBulletCount = 12;

/** The fixed masked run: twelve bullets for the eye, one visually hidden phrase for assistive technology. */
const maskedRun = "•".repeat(maskedBulletCount);

/** How long a revealed value stays visible, in milliseconds. */
const revealDurationMs = 30_000;

/**
 * The one value that is currently revealed.
 *
 * It lives only in component state: never in the query cache, a storage, the URL or an announcement. The name is the
 * display name of the secret, kept so the auto-hide announcement can name it without looking it up again.
 */
interface RevealedSecret {
  /** The key of the revealed secret. */
  key: string;
  /** The display name of the revealed secret. */
  name: string;
  /** The plaintext value. */
  value: string;
  /** The version of the secret the value belongs to. */
  version: number;
  /** The staged entry of the secret when the value was revealed; a different entry hides the value. */
  stagedAtReveal: StagedSecret | undefined;
}

/**
 * Whether a member may type a deliberate value for the credential.
 *
 * A credential the member supplies always takes one; a generated credential takes one only when its game accepts a
 * chosen value for it.
 *
 * @param secret The saved status of the secret.
 * @returns True when the credential shows the value input.
 */
function acceptsMemberValue(secret: GameServerSecretStatus): boolean {
  return !secret.generated || secret.acceptsChosenValue;
}

/** The fixed message key of each value issue; the typed value is never part of a message. */
const issueMessageKeys = {
  blank: "Server_Credentials_Invalid_Blank",
  tooLong: "Server_Credentials_Invalid_TooLong",
  control: "Server_Credentials_Invalid_Control",
  token: "Server_Credentials_Invalid_Token",
} as const satisfies Record<SecretValueIssue, string>;

/** Props of the internal {@link SecretRow}. */
interface SecretRowProps {
  /** The saved status of the secret. */
  secret: GameServerSecretStatus;
  /** Whether the Reveal control is offered to the member. */
  canReveal: boolean;
  /** The revealed value of this secret, or null while it is masked. */
  revealedValue: string | null;
  /** The value that was generated for the member and is shown once, or null when there is none. */
  oneTimeValue: string | null;
  /** Whether the last reveal of this secret failed. */
  revealFailed: boolean;
  /**
   * The sequence number of the focus request that names this secret, or undefined when the request names another
   * secret or there is none. A change of the number moves focus to the row's result.
   */
  focusSeq: number | undefined;
  /** Reveals the value of the secret. */
  onReveal(): void;
  /** Hides the revealed value of the secret. */
  onHide(): void;
}

/**
 * One credential of the server: its name, saved status, the masked or revealed value and the staging controls.
 *
 * A typed value is staged in the Settings draft and never kept in this component; the input shows what the draft
 * holds, so applying or discarding the change empties it. Rotating is a staged change like any other.
 *
 * @param props.secret The saved status of the secret.
 * @param props.canReveal Whether the Reveal control is offered to the member.
 * @param props.revealedValue The revealed value of this secret, or null while it is masked.
 * @param props.oneTimeValue The value that was generated for the member and is shown once, or null.
 * @param props.revealFailed Whether the last reveal of this secret failed.
 * @param props.focusSeq The sequence number of the focus request that names this secret, or undefined.
 * @param props.onReveal Reveals the value of the secret.
 * @param props.onHide Hides the revealed value of the secret.
 */
function SecretRow({
  secret,
  canReveal,
  revealedValue,
  oneTimeValue,
  revealFailed,
  focusSeq,
  onReveal,
  onHide,
}: SecretRowProps) {
  const { t } = useTranslation();
  const {
    draft,
    stageSecret,
    unstageSecret,
    readOnly,
    applying,
    reviewAttempt,
    dismissOneTimeValue,
    announce,
  } = useServerChangeDraft();
  const inputRef = useRef<HTMLInputElement>(null);
  const valueRef = useRef<HTMLDivElement>(null);
  const statusRef = useRef<HTMLParagraphElement>(null);
  const onceLabelId = useId();
  const [blurred, setBlurred] = useState(false);
  const [showTyped, setShowTyped] = useState(false);
  const staged = draft.secrets.get(secret.key);
  // The typed value is masked again as soon as the staged value leaves the draft.
  if (showTyped && staged?.kind !== "set") setShowTyped(false);
  const disabled = readOnly || applying;
  const typedValue = staged?.kind === "set" ? staged.value : "";
  const name = secret.displayName;
  const acceptsValue = acceptsMemberValue(secret);
  const issue =
    staged?.kind === "set" ? credentialValueIssue(staged.value, secret.valueFormat) : undefined;
  const issueMessage =
    issue !== undefined && (blurred || reviewAttempt > 0)
      ? t(issueMessageKeys[issue], { max: maxCredentialValueLength })
      : undefined;
  const oneTime = oneTimeValue !== null;
  const shown = revealedValue !== null;
  // D-15: a focus request that arrives after the member's own apply moves focus to the one-time block of this row
  // when it shows one and to the status line otherwise, then scrolls it into view. A request that was already there
  // when the row mounted is not repeated, so a remount or a background refetch never moves focus.
  const handledFocusSeq = useRef(focusSeq);
  useEffect(() => {
    if (focusSeq === undefined || focusSeq === handledFocusSeq.current) return;
    handledFocusSeq.current = focusSeq;
    let frame = 0;
    // The review dialog closes and a collapsed panel opens first; one retry covers a panel that is still hidden.
    const focusResult = (retry: boolean) => {
      const target = valueRef.current ?? statusRef.current;
      if (!target) return;
      target.focus();
      if (document.activeElement !== target && retry) {
        frame = requestAnimationFrame(() => focusResult(false));
        return;
      }
      if (typeof target.scrollIntoView === "function")
        target.scrollIntoView({ block: "nearest", behavior: "auto" });
    };
    frame = requestAnimationFrame(() => focusResult(true));
    return () => cancelAnimationFrame(frame);
  }, [focusSeq]);
  const showReveal = canReveal && secret.revealable && secret.isConfigured && !oneTime;
  const statusText =
    staged?.kind === "set"
      ? t("Server_Credentials_StagedSet")
      : staged?.kind === "rotate"
        ? t("Server_Credentials_RotateStaged")
        : secret.isConfigured
          ? t("Server_Credentials_Configured", { version: secret.version })
          : t("React_ServerWorkspace_NotSet");
  // The context button of the edit actions. One button element holds its place whichever label it carries.
  const contextKind =
    staged?.kind === "set" ? "undo" : secret.generated ? (staged ? "keep" : "rotate") : null;
  const contextLabels = {
    undo: {
      label: t("Server_Change_Field_Undo"),
      ariaLabel: t("Server_Change_Field_Undo_Name", { name }),
    },
    keep: {
      label: t("Server_Credentials_RotateUndo"),
      ariaLabel: t("Server_Credentials_RotateUndo_Name", { name }),
    },
    rotate: {
      label: t("Server_Credentials_Rotate"),
      ariaLabel: t("Server_Credentials_Rotate_Name", { name }),
    },
  } as const;
  const contextLabel = contextKind ? contextLabels[contextKind] : null;
  const runContextAction = () => {
    if (contextKind === "undo") {
      unstageSecret(secret.key);
      inputRef.current?.focus();
    } else if (contextKind === "keep") unstageSecret(secret.key);
    else if (contextKind === "rotate") stageSecret(secret.key, { kind: "rotate" });
  };
  return (
    <article className="credential">
      <header className="credential__head">
        <h3 className="credential__name">
          <ApiText>{name}</ApiText>
        </h3>
        {/* Always rendered, so staging never changes how the name wraps; hidden while nothing is staged. */}
        <span
          className={`credential__badge-slot${staged ? "" : " credential__badge-slot--idle"}`}
          aria-hidden={staged ? undefined : true}
        >
          <StatusBadge tone="info">{t("Server_Change_Field_Changed")}</StatusBadge>
        </span>
      </header>
      <p ref={statusRef} tabIndex={-1} className="credential__status">
        {statusText}
      </p>
      {secret.requiredForStart && !secret.isConfigured && (
        <StatePanel kind="warning">{t("Server_Credentials_RequiredBeforeStart")}</StatePanel>
      )}
      {(secret.isConfigured || oneTime) && (
        <div
          ref={oneTime ? valueRef : undefined}
          className="credential__value"
          role={oneTime ? "group" : undefined}
          aria-labelledby={oneTime ? onceLabelId : undefined}
          tabIndex={oneTime ? -1 : undefined}
        >
          <div className="credential__value-text">
            {oneTime ? (
              <>
                <span id={onceLabelId} className="credential__once-label">
                  {t("Server_Credentials_OneTime_Label")}
                </span>
                <code className="credential__code credential__code--once">{oneTimeValue}</code>
              </>
            ) : shown ? (
              <code className="credential__code">{revealedValue}</code>
            ) : (
              <>
                <span className="credential__mask" aria-hidden="true">
                  {maskedRun}
                </span>
                <span className="sr-only">{t("Server_Credentials_Masked")}</span>
              </>
            )}
          </div>
          {(oneTime || showReveal) && (
            <div className="credential__value-actions">
              {oneTime ? (
                <>
                  <CopyButton
                    value={oneTimeValue}
                    label={t("Server_Credentials_Copy")}
                    copiedMessage={t("Server_Credentials_CopiedAnnounce", { name })}
                  />
                  <ActionButton
                    variant="ghost"
                    size="compact"
                    aria-label={t("Server_Credentials_Hide_Name", { name })}
                    onClick={() => {
                      dismissOneTimeValue(secret.key);
                      announce(t("Server_Credentials_HideAnnounce", { name }));
                    }}
                  >
                    {t("Server_Credentials_Hide")}
                  </ActionButton>
                </>
              ) : (
                <ActionButton
                  variant="ghost"
                  size="compact"
                  aria-label={
                    shown
                      ? t("Server_Credentials_Hide_Name", { name })
                      : t("Server_Credentials_Reveal_Name", { name })
                  }
                  onClick={shown ? onHide : onReveal}
                >
                  {shown ? t("Server_Credentials_Hide") : t("Server_Credentials_Reveal")}
                </ActionButton>
              )}
            </div>
          )}
        </div>
      )}
      {revealFailed && (
        <p className="credential__error" role="alert">
          {t("Server_Credentials_RevealError")}
        </p>
      )}
      {(acceptsValue || secret.generated) && (
        <div className="credential__edit">
          {acceptsValue && (
            <TextField
              ref={inputRef}
              type={showTyped ? "text" : "password"}
              autoComplete="new-password"
              spellCheck={false}
              label={t("Server_Credentials_SetLabel", { name })}
              hint={
                secret.generated
                  ? t("Server_Credentials_SetHintGenerated")
                  : t("Server_Credentials_SetHint")
              }
              value={typedValue}
              error={issueMessage}
              disabled={disabled}
              onBlur={() => setBlurred(true)}
              onChange={(event) => {
                const value = event.target.value;
                if (value.length === 0) unstageSecret(secret.key);
                else
                  stageSecret(secret.key, {
                    kind: "set",
                    value,
                    externalProvider: secret.externalProvider,
                  });
              }}
            />
          )}
          <div className="credential__edit-actions">
            <span className="credential__slot">
              {contextLabel && (
                <ActionButton
                  variant="ghost"
                  size="compact"
                  disabled={disabled}
                  aria-label={contextLabel.ariaLabel}
                  onClick={runContextAction}
                >
                  {contextLabel.label}
                </ActionButton>
              )}
            </span>
            <span className="credential__toggle-slot">
              {acceptsValue && (
                <ActionButton
                  variant="ghost"
                  size="compact"
                  disabled={disabled || staged?.kind !== "set"}
                  aria-pressed={showTyped}
                  aria-label={
                    showTyped
                      ? t("Server_Credentials_HideTyped_Name", { name })
                      : t("Server_Credentials_ShowTyped_Name", { name })
                  }
                  onClick={() => setShowTyped(!showTyped)}
                >
                  {showTyped
                    ? t("Server_Credentials_HideTyped")
                    : t("Server_Credentials_ShowTyped")}
                </ActionButton>
              )}
            </span>
          </div>
        </div>
      )}
    </article>
  );
}

/** Props of {@link ServerSecretsPanel}. */
export interface ServerSecretsPanelProps {
  /** The server whose credentials are shown. */
  server: GameServer;
  /**
   * Asks the panel to focus and scroll to the result of a credential change the member just applied: the one-time
   * value block of the row of `key` when it shows one, otherwise its status line. A new `seq` makes a new request;
   * the same `seq` is never repeated.
   */
  focusRequest?: { key: string; seq: number } | undefined;
}

/**
 * The Credentials panel of the Settings section.
 *
 * The panel is a collapsible {@link SettingsPanel} with the id `server-credentials`. Its summary names the number of
 * credentials once they are loaded and is absent while loading or after a failed load; a failed load shows the error
 * with a retry inside the collapsed panel without opening it. A staged credential value pins the panel and shows the
 * staged count as its badge.
 *
 * Every configured secret shows a fixed masked run, never a partial value or its length. A new value or a rotation
 * is staged in the Settings draft instead of being saved, so it is reviewed without showing the value and applied
 * through the existing endpoint. Members with `canRevealSecrets` can reveal one value at a time; it is fetched on
 * demand through a mutation that is not kept in the cache, held only in component state and hidden again after 30
 * seconds, when the tab becomes hidden, on unmount, when another value is revealed and when the staged change of its
 * secret changes after the reveal. It stays in place while a change of its secret is already staged.
 *
 * A value that an applied rotation generated is different: it is held beside the draft in the Settings provider,
 * shown once in its row with Copy value and Hide value, and never fetched again. It has no timer, because a member
 * without the reveal permission could not get it back; it goes away on Hide value, when a change of that credential
 * is staged, when a newer saved version arrives and when the section unmounts. Announcements name the credential and
 * never carry the value. The panel renders nothing for a member without `canManageSecrets`, and it never prints the
 * text of an error.
 *
 * After the member's own apply of a credential change, a focus request moves focus to the result in the row of that
 * credential: the one-time value block when the apply generated a value, otherwise the status line. The request opens
 * the panel when the member closed it. Nothing else moves focus.
 *
 * @param props.server The server whose credentials are shown.
 * @param props.focusRequest The credential whose result takes focus after the member's own apply.
 */
export function ServerSecretsPanel({ server, focusRequest }: ServerSecretsPanelProps) {
  const { t } = useTranslation();
  const { open } = useSettingsPanelState();
  const { draft, announce, oneTimeValues, dismissOneTimeValue } = useServerChangeDraft();
  const announcedOneTime = useRef<Set<string>>(new Set());
  const [revealed, setRevealed] = useState<RevealedSecret | null>(null);
  const [failedKey, setFailedKey] = useState<string | null>(null);
  const statuses = useQuery({
    queryKey: hostingQueryKeys.secrets(server.primaryKey),
    queryFn: ({ signal }) => hostingService.secrets(server.primaryKey, signal),
    enabled: server.canManageSecrets === true,
  });
  const reveal = useMutation({
    mutationFn: (key: string) => hostingService.revealSecret(server.primaryKey, key),
    gcTime: 0,
  });
  const secrets = statuses.data ?? [];
  const focusRequestSeq = focusRequest?.seq;
  // A collapsed panel cannot take focus, so a request opens it first; the row focuses on the next frame.
  useEffect(() => {
    if (focusRequestSeq !== undefined && focusRequestSeq > 0) open("server-credentials");
  }, [focusRequestSeq, open]);

  // The value is shown only while its secret is still configured at the same version and its staged entry is the one
  // seen at reveal time; any other state hides it by dropping it, so changing or undoing a staged change can never
  // bring an old value back.
  const current = revealed ? secrets.find((secret) => secret.key === revealed.key) : undefined;
  if (
    revealed &&
    (!current ||
      !current.isConfigured ||
      current.version !== revealed.version ||
      draft.secrets.get(revealed.key) !== revealed.stagedAtReveal)
  )
    setRevealed(null);

  // A one-time value is shown only while its secret is configured and not older than the value; it is judged
  // against loaded data only, so a loading list never drops it.
  const { freshOneTime, staleOneTimeKeys } = useMemo(() => {
    const fresh = new Map<string, GameServerSecretValue>();
    const stale: string[] = [];
    if (!statuses.data) return { freshOneTime: fresh, staleOneTimeKeys: stale };
    for (const value of oneTimeValues.values()) {
      const saved = statuses.data.find((candidate) => candidate.key === value.key);
      if (!saved || !saved.isConfigured || (saved.version ?? 0) > value.version)
        stale.push(value.key);
      else fresh.set(value.key, value);
    }
    return { freshOneTime: fresh, staleOneTimeKeys: stale };
  }, [oneTimeValues, statuses.data]);
  useEffect(() => {
    for (const key of staleOneTimeKeys) dismissOneTimeValue(key);
  }, [staleOneTimeKeys, dismissOneTimeValue]);
  // Each newly arrived value is announced once, by credential name only.
  useEffect(() => {
    const announced = announcedOneTime.current;
    for (const key of announced) if (!freshOneTime.has(key)) announced.delete(key);
    const messages: string[] = [];
    for (const key of freshOneTime.keys()) {
      if (announced.has(key)) continue;
      announced.add(key);
      const saved = statuses.data?.find((candidate) => candidate.key === key);
      if (saved)
        messages.push(t("Server_Credentials_OneTime_Announce", { name: saved.displayName }));
    }
    if (messages.length > 0) announce(messages.join(" "));
  }, [freshOneTime, statuses.data, announce, t]);

  // The timer and the visibility listener belong to the one revealed value and are removed when it goes away.
  useEffect(() => {
    if (!revealed) return;
    const { name } = revealed;
    const timer = window.setTimeout(() => {
      setRevealed(null);
      announce(t("Server_Credentials_HideAnnounce", { name }));
    }, revealDurationMs);
    const onVisibility = () => {
      if (document.visibilityState === "hidden") setRevealed(null);
    };
    document.addEventListener("visibilitychange", onVisibility);
    return () => {
      window.clearTimeout(timer);
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [revealed, announce, t]);

  const revealValue = async (secret: GameServerSecretStatus) => {
    if (reveal.isPending) return;
    const stagedAtReveal = draft.secrets.get(secret.key);
    setFailedKey(null);
    try {
      const result = await reveal.mutateAsync(secret.key);
      setRevealed({
        key: result.key,
        name: secret.displayName,
        value: result.value,
        version: result.version,
        stagedAtReveal,
      });
      announce(t("Server_Credentials_RevealAnnounce", { name: secret.displayName }));
    } catch {
      setRevealed(null);
      setFailedKey(secret.key);
    } finally {
      // The value was copied into component state above; the mutation state must not hold it any longer.
      reveal.reset();
    }
  };
  const hideValue = (secret: GameServerSecretStatus) => {
    setRevealed(null);
    announce(t("Server_Credentials_HideAnnounce", { name: secret.displayName }));
  };

  // Without the right there is nothing to show: no heading, no summary and no request (the query is disabled).
  if (!server.canManageSecrets) return null;
  const loaded = !statuses.isLoading && !statuses.isError && statuses.data !== undefined;
  let body;
  if (statuses.isLoading)
    body = <StatePanel kind="loading">{t("Server_Credentials_Loading")}</StatePanel>;
  else if (statuses.isError)
    body = (
      <StatePanel
        kind="error"
        action={
          <ActionButton variant="ghost" onClick={() => void statuses.refetch()}>
            {t("Server_Settings_Retry")}
          </ActionButton>
        }
      >
        {t("Server_Credentials_LoadError")}
      </StatePanel>
    );
  else
    body = (
      <div className="credentials">
        <p className="credentials__description">{t("Server_Credentials_Description")}</p>
        {secrets.length === 0 ? (
          <StatePanel kind="empty">{t("Server_Credentials_Empty")}</StatePanel>
        ) : (
          secrets.map((secret) => (
            <SecretRow
              key={secret.key}
              secret={secret}
              canReveal={server.canRevealSecrets === true}
              revealedValue={revealed?.key === secret.key ? revealed.value : null}
              oneTimeValue={freshOneTime.get(secret.key)?.value ?? null}
              revealFailed={failedKey === secret.key}
              focusSeq={focusRequest?.key === secret.key ? focusRequest.seq : undefined}
              onReveal={() => void revealValue(secret)}
              onHide={() => hideValue(secret)}
            />
          ))
        )}
      </div>
    );
  return (
    <SettingsPanel
      id="server-credentials"
      title={t("Server_Settings_CredentialsTitle")}
      summary={
        loaded ? t("Server_Settings_Summary_Credentials", { count: secrets.length }) : undefined
      }
      badge={
        draft.secrets.size > 0 ? (
          <StatusBadge tone="info">
            {t("Server_Change_PanelCount", { count: draft.secrets.size })}
          </StatusBadge>
        ) : undefined
      }
      stagedKeys={[...draft.secrets.keys()]}
    >
      {body}
    </SettingsPanel>
  );
}
