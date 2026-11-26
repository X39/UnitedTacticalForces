import { Checkmark16Regular, Copy20Regular } from "@fluentui/react-icons";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import "@/components/server/workspace-shell.css";

/** How long the confirmed state stays visible after a successful copy, in milliseconds. */
const copiedDurationMs = 2000;

type CopyState = "idle" | "copied" | "failed";

/** Props of {@link CopyButton}. */
export interface CopyButtonProps {
  /** The text written to the clipboard when the button is activated. */
  value: string;
  /** The visible label of the button while it is idle, for example "Copy address". */
  label: string;
  /** The message a polite live region announces after a successful copy. */
  copiedMessage: string;
}

/**
 * A button that copies a text to the clipboard.
 *
 * The text is copied only inside the click handler of an explicit activation, never on focus, hover or load.
 * After a successful copy the icon and the label confirm the result for two seconds and a polite status region
 * announces `copiedMessage`. When the clipboard is missing, unavailable in an insecure context or denied by the
 * browser, the status region shows a visible failure text until the next attempt; nothing is thrown.
 *
 * @param props.value The text to copy.
 * @param props.label The visible idle label.
 * @param props.copiedMessage The announcement after a successful copy.
 */
export function CopyButton({ value, label, copiedMessage }: CopyButtonProps) {
  const { t } = useTranslation();
  const [state, setState] = useState<CopyState>("idle");
  const timer = useRef<number | undefined>(undefined);
  const mounted = useRef(false);

  const clearTimer = useCallback(() => {
    if (timer.current !== undefined) {
      window.clearTimeout(timer.current);
      timer.current = undefined;
    }
  }, []);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      clearTimer();
    };
  }, [clearTimer]);

  const copy = useCallback(async () => {
    clearTimer();
    const clipboard: Clipboard | undefined = navigator.clipboard;
    try {
      if (!clipboard) throw new Error("The clipboard is not available.");
      await clipboard.writeText(value);
    } catch {
      if (mounted.current) setState("failed");
      return;
    }
    if (!mounted.current) return;
    setState("copied");
    timer.current = window.setTimeout(() => {
      timer.current = undefined;
      setState("idle");
    }, copiedDurationMs);
  }, [clearTimer, value]);

  const copied = state === "copied";
  const Icon = copied ? Checkmark16Regular : Copy20Regular;
  return (
    <span className="copy-button">
      <ActionButton variant="ghost" size="compact" onClick={() => void copy()}>
        <Icon aria-hidden="true" />
        {copied ? t("Shared_Copy_Done") : label}
      </ActionButton>
      <span role="status" className="copy-button__status">
        {copied && <span className="sr-only">{copiedMessage}</span>}
        {state === "failed" && t("Shared_Copy_Failed")}
      </span>
    </span>
  );
}
