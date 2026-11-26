import { Spinner } from "@fluentui/react-components";
import {
  CheckmarkCircle20Regular,
  DismissCircle20Regular,
  Info20Regular,
  Warning20Regular,
} from "@fluentui/react-icons";
import {
  useEffect,
  useId,
  useRef,
  useState,
  type ComponentPropsWithoutRef,
  type CSSProperties,
  type DragEvent,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import { useTranslation } from "react-i18next";
import ReactMarkdown, { defaultUrlTransform, type Components } from "react-markdown";
import { useBlocker } from "react-router";
import remarkGridTables from "@adobe/remark-gridtables";
import rehypeHighlight from "rehype-highlight";
import rehypeSlug from "rehype-slug";
import remarkDeflist from "remark-deflist";
import remarkDirective from "remark-directive";
import remarkFlexibleMarkers from "remark-flexible-markers";
import remarkGfm from "remark-gfm";
import {
  markdownRehypeOptions,
  preprocessMarkdigBlocks,
  remarkMarkdigAbbreviations,
  remarkMarkdigContainers,
  remarkMarkdigInlineExtensions,
} from "@/app/markdownExtensions";
import { ActionButton, type ActionControlVariant } from "@/components/ActionControl";
import { TextField } from "@/components/FormFields";
import "@/components/shared-primitives.css";

export function PageHeader({
  kicker,
  title,
  description,
  actions,
  meta,
  headingLevel = 1,
}: {
  kicker?: string;
  title: string;
  description?: string;
  actions?: ReactNode;
  /** Optional content shown after the heading and description, such as state or status chips. */
  meta?: ReactNode;
  headingLevel?: 1 | 2 | 3;
}) {
  const Heading = headingLevel === 1 ? "h1" : headingLevel === 2 ? "h2" : "h3";
  return (
    <header className="page-header">
      <div>
        {kicker && <span className="kicker">{kicker}</span>}
        <Heading tabIndex={-1}>{title}</Heading>
        {description && <p>{description}</p>}
        {meta && <div className="page-header__meta">{meta}</div>}
      </div>
      {actions && <div className="page-header__actions">{actions}</div>}
    </header>
  );
}

export type StateKind =
  "loading" | "empty" | "error" | "forbidden" | "success" | "warning" | "info";
const stateIcons = {
  error: <DismissCircle20Regular />,
  forbidden: <DismissCircle20Regular />,
  success: <CheckmarkCircle20Regular />,
  warning: <Warning20Regular />,
  info: <Info20Regular />,
  empty: <Info20Regular />,
  loading: undefined,
};

/**
 * A panel that explains a loading, empty, error, forbidden, success, warning or info state.
 *
 * Every kind except error is a polite status region; error is an assertive alert, so it sets no aria-live that
 * would override the implicit politeness of the role. The icon is decorative and hidden from assistive
 * technology.
 *
 * @param props.kind The state kind; selects the tone and the default icon.
 * @param props.title Optional heading, rendered as an h2.
 * @param props.children The panel body.
 * @param props.action Optional action rendered at the end of the panel.
 * @param props.icon Optional icon that replaces the default icon of the kind, including the loading spinner.
 */
export function StatePanel({
  kind = "empty",
  title,
  children,
  action,
  icon,
}: {
  kind?: StateKind;
  title?: string;
  children: ReactNode;
  action?: ReactNode;
  icon?: ReactNode;
}) {
  return (
    <section
      className={`state-panel state-panel--${kind}`}
      role={kind === "error" ? "alert" : "status"}
      aria-live={kind === "error" ? undefined : "polite"}
    >
      <div className="state-panel__icon" aria-hidden="true">
        {icon !== undefined ? (
          icon
        ) : kind === "loading" ? (
          <Spinner size="small" />
        ) : (
          stateIcons[kind]
        )}
      </div>
      <div>
        {title && <h2>{title}</h2>}
        <div>{children}</div>
      </div>
      {action && <div className="state-panel__action">{action}</div>}
    </section>
  );
}

export function StatusBadge({
  tone = "neutral",
  className,
  children,
}: {
  tone?: "neutral" | "success" | "warning" | "danger" | "info";
  className?: string | undefined;
  children: ReactNode;
}) {
  const classNames = ["status-badge", `status-badge--${tone}`, className].filter(Boolean).join(" ");
  return <span className={classNames}>{children}</span>;
}

export function Metric({
  label,
  value,
  detail,
}: {
  label: string;
  value: ReactNode;
  detail?: string;
}) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong>{value}</strong>
      {detail && <small>{detail}</small>}
    </div>
  );
}

function safeUrlTransform(url: string): string {
  if (
    url.startsWith("/") ||
    url.startsWith("#") ||
    /^(https?:|mailto:)/i.test(url) ||
    !/^[a-z][a-z\d+.-]*:/i.test(url)
  )
    return defaultUrlTransform(url);
  return "";
}

function expandShortcodes(markdown: string): string {
  const icons: Record<string, string> = {
    document: "📄",
    extract: "📤",
    host: "🖥️",
    intel: "🔎",
    map: "🗺️",
    medical: "⚕️",
    modpack: "📦",
    objective: "🎯",
    rally: "🚩",
    radio: "📻",
    slot: "🪖",
    stealth: "🥷",
    support: "🛡️",
    time: "⏱️",
    warning: "⚠️",
    info: "ℹ️",
    success: "✅",
    error: "⛔",
  };
  const expandedAlerts = markdown.replace(
    /^>\s*\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*$/gim,
    (_, kind: string) => {
      const markers: Record<string, string> = {
        NOTE: "ℹ️",
        TIP: "💡",
        IMPORTANT: "❗",
        WARNING: "⚠️",
        CAUTION: "⛔",
      };
      return `> **${markers[kind.toUpperCase()]} ${kind.toUpperCase()}**`;
    },
  );
  return expandedAlerts.replace(
    /:([a-z]+):/gi,
    (match, name: string) => icons[name.toLowerCase()] ?? match,
  );
}

function MarkdownLink({ href, children, ...props }: ComponentPropsWithoutRef<"a">) {
  if (!href) return <span>{children}</span>;
  const external = /^https?:\/\//i.test(href);
  return (
    <a href={href} {...props} {...(external ? { rel: "noreferrer" } : {})}>
      {children}
    </a>
  );
}

function MarkdownCodeBlock({ children }: ComponentPropsWithoutRef<"pre">) {
  const { t } = useTranslation();
  const preRef = useRef<HTMLPreElement>(null);
  const [copyState, setCopyState] = useState<"idle" | "copied" | "failed">("idle");
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(preRef.current?.innerText ?? "");
      setCopyState("copied");
    } catch {
      setCopyState("failed");
    }
    window.setTimeout(() => setCopyState("idle"), 1200);
  };
  return (
    <div className="markdown-code-block">
      <pre ref={preRef}>{children}</pre>
      <button type="button" onClick={() => void copy()}>
        {copyState === "copied"
          ? t("React_Markdown_Copied")
          : copyState === "failed"
            ? t("React_Markdown_CopyFailed")
            : t("React_Markdown_Copy")}
      </button>
    </div>
  );
}

function MarkdownExcerptHeading({ children }: { children?: ReactNode }) {
  return <strong className="markdown-excerpt__heading">{children}</strong>;
}

function MarkdownExcerptLink({ children }: ComponentPropsWithoutRef<"a">) {
  return <span className="markdown-excerpt__link">{children}</span>;
}

function MarkdownExcerptCodeBlock({ children }: ComponentPropsWithoutRef<"pre">) {
  return <span className="markdown-excerpt__code">{children}</span>;
}

function MarkdownExcerptImage({ alt }: ComponentPropsWithoutRef<"img">) {
  return alt ? <span>{alt}</span> : null;
}

const markdownComponents: Components = { a: MarkdownLink, pre: MarkdownCodeBlock };
const markdownExcerptComponents: Components = {
  a: MarkdownExcerptLink,
  h1: MarkdownExcerptHeading,
  h2: MarkdownExcerptHeading,
  h3: MarkdownExcerptHeading,
  h4: MarkdownExcerptHeading,
  h5: MarkdownExcerptHeading,
  h6: MarkdownExcerptHeading,
  img: MarkdownExcerptImage,
  pre: MarkdownExcerptCodeBlock,
};

function MarkdownContent({ children, components }: { children: string; components: Components }) {
  return (
    <ReactMarkdown
      remarkPlugins={[
        [remarkGfm, { singleTilde: false }],
        remarkGridTables,
        remarkDeflist,
        remarkDirective,
        remarkFlexibleMarkers,
        remarkMarkdigContainers,
        remarkMarkdigInlineExtensions,
        remarkMarkdigAbbreviations,
      ]}
      rehypePlugins={[rehypeSlug, rehypeHighlight]}
      remarkRehypeOptions={markdownRehypeOptions}
      urlTransform={safeUrlTransform}
      skipHtml
      components={components}
    >
      {expandShortcodes(preprocessMarkdigBlocks(children))}
    </ReactMarkdown>
  );
}

export function Markdown({ children }: { children: string }) {
  return (
    <div className="markdown">
      <MarkdownContent components={markdownComponents}>{children}</MarkdownContent>
    </div>
  );
}

export function MarkdownExcerpt({
  children,
  fallback,
  maximumLines = 3,
}: {
  children: string;
  fallback?: ReactNode;
  maximumLines?: number;
}) {
  const source = children.trim();
  if (!source && (fallback === undefined || fallback === null)) return null;
  const lineCount = Number.isFinite(maximumLines) ? Math.max(1, Math.floor(maximumLines)) : 3;
  const style = {
    "--markdown-excerpt-max-height": `${lineCount * 1.55}em`,
  } as CSSProperties;

  return (
    <div className="markdown markdown--excerpt" style={style}>
      {source ? (
        <MarkdownContent components={markdownExcerptComponents}>{source}</MarkdownContent>
      ) : (
        fallback
      )}
    </div>
  );
}

interface ConfirmDialogProps {
  open: boolean;
  title: ReactNode;
  description: string;
  confirmLabel: ReactNode;
  cancelLabel: string;
  challenge?: string;
  onConfirm(): void;
  onCancel(): void;
  /** Replaces the default kicker text above the title. */
  kicker?: string | undefined;
  /** Visual variant of the confirm button; defaults to the danger variant. */
  confirmVariant?: ActionControlVariant | undefined;
  /** Extra content rendered between the description and the challenge field. */
  children?: ReactNode;
  /** Where focus lands when the dialog opens; "auto" keeps the browser default. */
  initialFocus?: "auto" | "dismiss" | undefined;
  /** Marks a request in flight: dismissal and confirmation are blocked and a spinner is shown. */
  busy?: boolean | undefined;
  /** Error text shown in an alert region inside the dialog. */
  error?: string | undefined;
  /** Extra class appended to the dialog element. */
  className?: string | undefined;
  /** Helper text linked to the challenge input as its description. */
  challengeHelper?: string | undefined;
  /** Blocks confirmation without hiding the button, for example when the request is stale. */
  confirmDisabled?: boolean | undefined;
  /**
   * A third action rendered after the confirm button, for example "Review changes" in a leave dialog. It is a
   * plain button, so it never submits the dialog, and it is disabled while a request is in flight.
   */
  extraAction?: { label: ReactNode; variant: ActionControlVariant; onClick(): void } | undefined;
  /**
   * Accessible name of the body. When set the body is a focusable `region` with this name, so a scrollable body
   * (for example a review list) can be reached and scrolled with the keyboard. Without it the body stays a plain
   * block.
   */
  bodyLabel?: string | undefined;
  /**
   * Visible reason the confirm action is unavailable. While set the confirm stays focusable with `aria-disabled`,
   * the reason is linked as its description, and activation does not call `onConfirm`.
   */
  confirmUnavailableReason?: ReactNode | undefined;
  /**
   * Closes the dialog through `onCancel` when the member clicks the backdrop outside the dialog box. The click
   * counts only when the pointer went down and up on the backdrop, so a drag that starts inside the dialog never
   * dismisses it, and it is ignored while a request is in flight. Off by default: a destructive confirmation
   * stays open until it is answered, so only dialogs whose cancel outcome is the safe one should opt in.
   */
  closeOnBackdrop?: boolean | undefined;
  /**
   * Cancels the browser default for a file drag or drop anywhere on the dialog, backdrop included, that no inner drop
   * zone has already handled. Without it a file dropped a few pixels beside the zone makes the browser open the file
   * and replace the app. Off by default; only dialogs that invite a file drop should opt in. Drags that carry no
   * files, such as selected text, stay untouched.
   */
  blockFileDrops?: boolean | undefined;
  /**
   * Where the confirm action sits in the button row. `end` renders dismiss, confirm, extra action; `start`
   * renders confirm, dismiss, extra action so a destructive action leads the row. Defaults to `end`.
   */
  confirmPlacement?: "end" | "start" | undefined;
  /** Visual variant of the dismiss button; defaults to the ghost variant. */
  dismissVariant?: ActionControlVariant | undefined;
}

function ConfirmDialogForm({
  title,
  description,
  confirmLabel,
  cancelLabel,
  challenge,
  onConfirm,
  onCancel,
  kicker,
  confirmVariant = "danger",
  children,
  busy = false,
  error,
  challengeHelper,
  confirmDisabled = false,
  extraAction,
  bodyLabel,
  confirmUnavailableReason,
  confirmPlacement = "end",
  dismissVariant = "ghost",
  ids,
}: Omit<
  ConfirmDialogProps,
  "open" | "initialFocus" | "className" | "closeOnBackdrop" | "blockFileDrops"
> & {
  ids: { title: string; description: string; body: string };
}) {
  const { t } = useTranslation();
  const [typed, setTyped] = useState("");
  const errorRef = useRef<HTMLParagraphElement>(null);
  useEffect(() => {
    if (error) errorRef.current?.focus();
  }, [error]);
  const challengeUnmet = Boolean(challenge) && typed !== challenge;
  const reasonGiven =
    confirmUnavailableReason !== null &&
    confirmUnavailableReason !== undefined &&
    confirmUnavailableReason !== false &&
    confirmUnavailableReason !== "";
  const blocked = busy || confirmDisabled || challengeUnmet || reasonGiven;
  const confirmButton = (
    <ActionButton
      variant={confirmVariant}
      type="submit"
      unavailableReason={confirmUnavailableReason}
      aria-disabled={blocked ? true : undefined}
      onClick={(event) => {
        if (blocked) event.preventDefault();
      }}
    >
      {busy ? (
        <>
          <span aria-hidden="true">
            <Spinner size="tiny" />
          </span>
          {confirmLabel}
        </>
      ) : (
        confirmLabel
      )}
    </ActionButton>
  );
  return (
    <form
      method="dialog"
      onSubmit={(event) => {
        event.preventDefault();
        // The dialog confirms itself; a form that wraps the page must never also treat this as its own submit.
        event.stopPropagation();
        if (blocked) return;
        onConfirm();
      }}
    >
      <span className="kicker">{kicker ?? t("React_Confirm_Kicker")}</span>
      <h2 id={ids.title}>{title}</h2>
      <p id={ids.description}>{description}</p>
      {children && (
        <div
          id={ids.body}
          className="dialog__body"
          {...(bodyLabel ? { role: "region", "aria-label": bodyLabel, tabIndex: 0 } : {})}
        >
          {children}
        </div>
      )}
      {challenge && (
        <TextField
          autoComplete="off"
          value={typed}
          onChange={(event) => setTyped(event.target.value)}
          label={
            <>
              {t("React_Confirm_TypePrefix")}{" "}
              <strong>
                <bdi>{challenge}</bdi>
              </strong>{" "}
              {t("React_Confirm_TypeSuffix")}
            </>
          }
          hint={challengeHelper}
        />
      )}
      {error && (
        <p ref={errorRef} className="dialog__error" role="alert" tabIndex={-1}>
          {error}
        </p>
      )}
      <div className="dialog__actions">
        {confirmPlacement === "start" && confirmButton}
        <ActionButton
          variant={dismissVariant}
          type="button"
          onClick={onCancel}
          disabled={busy}
          data-dialog-dismiss=""
        >
          {cancelLabel}
        </ActionButton>
        {confirmPlacement !== "start" && confirmButton}
        {extraAction && (
          <ActionButton
            variant={extraAction.variant}
            type="button"
            onClick={extraAction.onClick}
            disabled={busy}
          >
            {extraAction.label}
          </ActionButton>
        )}
      </div>
    </form>
  );
}

/**
 * Cancels the browser default for a file drag or drop that no inner drop zone handled, so the browser never opens the
 * dropped file in place of the app. A zone that took the event has already prevented the default and keeps its
 * own drop effect.
 *
 * @param event The drag or drop event that bubbled up to the dialog.
 */
function blockStrayFileDrag(event: DragEvent<HTMLElement>): void {
  if (event.defaultPrevented || !event.dataTransfer?.types.includes("Files")) return;
  event.preventDefault();
  event.dataTransfer.dropEffect = "none";
}

/**
 * A modal confirmation dialog.
 *
 * The additive props support a busy request (Escape, dismissal and confirmation are blocked and a native close
 * re-opens the dialog), an error alert, extra body content, a challenge with helper text and an initial focus
 * on the dismiss button. `closeOnBackdrop` maps a backdrop click to `onCancel`, `confirmPlacement` orders the confirm
 * action before the dismiss button and `dismissVariant` restyles the dismiss button.
 */
export function ConfirmDialog({
  open,
  className,
  initialFocus = "auto",
  busy = false,
  closeOnBackdrop = false,
  blockFileDrops = false,
  onCancel,
  ...formProps
}: ConfirmDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const openRef = useRef(open);
  const pointerStartedOnBackdrop = useRef(false);
  /** Whether a pointer event hit the backdrop: the dialog element itself, outside the dialog box. */
  const isOnBackdrop = (event: { target: EventTarget; clientX: number; clientY: number }) => {
    const dialog = dialogRef.current;
    if (!dialog || event.target !== dialog) return false;
    const box = dialog.getBoundingClientRect();
    return (
      event.clientX < box.left ||
      event.clientX > box.right ||
      event.clientY < box.top ||
      event.clientY > box.bottom
    );
  };
  const baseId = useId();
  const ids = {
    title: `${baseId}-title`,
    description: `${baseId}-description`,
    body: `${baseId}-body`,
  };
  useEffect(() => {
    openRef.current = open;
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (open && !dialog.open) {
      dialog.showModal();
      if (initialFocus === "dismiss") {
        dialog.querySelector<HTMLElement>("[data-dialog-dismiss]")?.focus();
      }
    }
    if (!open && dialog.open) dialog.close();
  }, [open, initialFocus]);
  const describedBy = formProps.children ? `${ids.description} ${ids.body}` : ids.description;
  // The dialog renders into the document body: a dialog declared inside a page form would nest its own
  // `form method="dialog"` in that form, and the nested form never receives its submit event.
  const dialog = (
    <dialog
      ref={dialogRef}
      className={["dialog", className].filter(Boolean).join(" ")}
      aria-labelledby={ids.title}
      aria-describedby={describedBy}
      aria-busy={busy || undefined}
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) onCancel();
      }}
      onPointerDown={(event) => {
        pointerStartedOnBackdrop.current = isOnBackdrop(event);
      }}
      onClick={(event) => {
        const startedOnBackdrop = pointerStartedOnBackdrop.current;
        pointerStartedOnBackdrop.current = false;
        if (closeOnBackdrop && !busy && startedOnBackdrop && isOnBackdrop(event)) onCancel();
      }}
      onClose={() => {
        const dialog = dialogRef.current;
        if (openRef.current && dialog && !dialog.open) dialog.showModal();
      }}
      onDragOver={blockFileDrops ? blockStrayFileDrag : undefined}
      onDrop={blockFileDrops ? blockStrayFileDrag : undefined}
    >
      <ConfirmDialogForm
        key={open ? "open" : "closed"}
        {...formProps}
        busy={busy}
        onCancel={onCancel}
        ids={ids}
      />
    </dialog>
  );
  return createPortal(dialog, document.body);
}

/**
 * Guards a dirty form against leaving the page: a native prompt before the document unloads and a confirm before a
 * router navigation to another path or query.
 *
 * A change of the URL fragment alone stays on the same page, so it is never blocked: activating the skip link of the
 * layout must not ask the member to discard what they typed (WCAG 2.1.1, 3.2.2).
 *
 * @param isDirty Whether the form holds changes that would be lost.
 */
export function useUnsavedChanges(isDirty: boolean): void {
  const { t } = useTranslation();
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      isDirty &&
      (currentLocation.pathname !== nextLocation.pathname ||
        currentLocation.search !== nextLocation.search),
  );
  useEffect(() => {
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (!isDirty) return;
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", beforeUnload);
    return () => window.removeEventListener("beforeunload", beforeUnload);
  }, [isDirty]);
  useEffect(() => {
    if (blocker.state !== "blocked") return;
    if (window.confirm(t("React_UnsavedChanges"))) blocker.proceed();
    else blocker.reset();
  }, [blocker, t]);
}
