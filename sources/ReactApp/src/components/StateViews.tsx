import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { StatePanel } from "@/components/primitives";

/**
 * Shows the shared loading panel: a spinner and a polite status text, with no title and no action.
 *
 * Nothing action-related belongs in a loading state, and the panel never moves focus.
 *
 * @param props.label Optional specific loading text; defaults to the shared "Loading..." text.
 */
export function LoadingState({ label }: { label?: string | undefined }) {
  const { t } = useTranslation();
  return <StatePanel kind="loading">{label ?? t("Common_LoadingData")}</StatePanel>;
}

/**
 * Shows the shared empty panel for a list or section that has no content.
 *
 * There is deliberately no generic empty text: the title names what is missing and the body names the next
 * step, so both are required.
 *
 * @param props.title What is missing, for example "No roles yet".
 * @param props.children The next step, for example "Create the first role to give members access."
 * @param props.action Optional single action that performs the next step.
 */
export function EmptyState({
  title,
  children,
  action,
}: {
  title: string;
  children: ReactNode;
  action?: ReactNode | undefined;
}) {
  return (
    <StatePanel kind="empty" title={title} action={action}>
      {children}
    </StatePanel>
  );
}

/**
 * Shows the shared load-error panel with a retry control, announced as an alert.
 *
 * A page that already holds data keeps the stale rows visible below this panel. The retry button is the first
 * tab stop inside the panel; focus is never stolen.
 *
 * @param props.onRetry Called when the viewer asks to try again; it must start a real refetch.
 * @param props.title Optional specific title that replaces the generic one.
 * @param props.children Optional specific body that replaces the generic one.
 * @param props.retryLabel Optional specific retry label that replaces "Try again".
 */
export function ErrorState({
  onRetry,
  title,
  children,
  retryLabel,
}: {
  onRetry(): void;
  title?: string | undefined;
  children?: ReactNode | undefined;
  retryLabel?: string | undefined;
}) {
  const { t } = useTranslation();
  return (
    <StatePanel
      kind="error"
      title={title ?? t("Common_State_LoadErrorTitle")}
      action={
        <ActionButton variant="ghost" onClick={onRetry}>
          {retryLabel ?? t("Common_State_Retry")}
        </ActionButton>
      }
    >
      {children ?? t("Common_State_LoadErrorBody")}
    </StatePanel>
  );
}

/**
 * Shows the shared forbidden panel for a viewer who may not use a page or section.
 *
 * @param props.title Optional specific title that replaces the generic one.
 * @param props.children Optional specific body that replaces the generic one.
 * @param props.action Optional action, such as a sign-in button, rendered in the action area of the panel.
 */
export function ForbiddenState({
  title,
  children,
  action,
}: {
  title?: string | undefined;
  children?: ReactNode | undefined;
  action?: ReactNode | undefined;
}) {
  const { t } = useTranslation();
  return (
    <StatePanel kind="forbidden" title={title ?? t("Common_State_ForbiddenTitle")} action={action}>
      {children ?? t("Common_State_ForbiddenBody")}
    </StatePanel>
  );
}

/**
 * Renders its children only for a viewer the caller has found to be allowed, and the shared forbidden panel
 * otherwise.
 *
 * This component is presentation only. It hides content that the viewer should not see but it never
 * authorizes anything: the API remains the authority, rejects every disallowed request and must not rely on
 * this gate. No request is skipped or trusted because of it.
 *
 * @param props.allowed Whether the viewer holds the claim the surface needs.
 * @param props.children The content to protect.
 * @param props.title Optional specific forbidden title.
 * @param props.body Optional specific forbidden body.
 */
export function AccessBoundary({
  allowed,
  children,
  title,
  body,
}: {
  allowed: boolean;
  children: ReactNode;
  title?: string | undefined;
  body?: ReactNode | undefined;
}) {
  return allowed ? <>{children}</> : <ForbiddenState title={title}>{body}</ForbiddenState>;
}
