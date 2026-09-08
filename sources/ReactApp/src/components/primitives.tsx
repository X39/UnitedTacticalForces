import { Spinner } from "@fluentui/react-components";
import {
  CheckmarkCircle20Regular,
  DismissCircle20Regular,
  Info20Regular,
  Warning20Regular,
} from "@fluentui/react-icons";
import {
  useEffect,
  useRef,
  useState,
  type ComponentPropsWithoutRef,
  type CSSProperties,
  type ReactNode,
} from "react";
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
import { ActionButton } from "@/components/ActionControl";

export function PageHeader({
  kicker,
  title,
  description,
  actions,
  headingLevel = 1,
}: {
  kicker?: string;
  title: string;
  description?: string;
  actions?: ReactNode;
  headingLevel?: 1 | 2 | 3;
}) {
  const Heading = headingLevel === 1 ? "h1" : headingLevel === 2 ? "h2" : "h3";
  return (
    <header className="page-header">
      <div>
        {kicker && <span className="kicker">{kicker}</span>}
        <Heading>{title}</Heading>
        {description && <p>{description}</p>}
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

export function StatePanel({
  kind = "empty",
  title,
  children,
  action,
}: {
  kind?: StateKind;
  title?: string;
  children: ReactNode;
  action?: ReactNode;
}) {
  return (
    <section
      className={`state-panel state-panel--${kind}`}
      role={kind === "error" ? "alert" : "status"}
      aria-live="polite"
    >
      <div className="state-panel__icon" aria-hidden="true">
        {kind === "loading" ? <Spinner size="small" /> : stateIcons[kind]}
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
  children,
}: {
  tone?: "neutral" | "success" | "warning" | "danger" | "info";
  children: ReactNode;
}) {
  return <span className={`status-badge status-badge--${tone}`}>{children}</span>;
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

export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel,
  cancelLabel,
  challenge,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  title: string;
  description: string;
  confirmLabel: string;
  cancelLabel: string;
  challenge?: string;
  onConfirm(): void;
  onCancel(): void;
}) {
  const { t } = useTranslation();
  const dialogRef = useRef<HTMLDialogElement>(null);
  const challengeRef = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (challengeRef.current) challengeRef.current.value = "";
    if (open && !dialogRef.current?.open) dialogRef.current?.showModal();
    if (!open && dialogRef.current?.open) dialogRef.current.close();
  }, [open]);
  return (
    <dialog
      ref={dialogRef}
      className="dialog"
      onCancel={(event) => {
        event.preventDefault();
        onCancel();
      }}
    >
      <form
        method="dialog"
        onSubmit={(event) => {
          event.preventDefault();
          if (challenge && challengeRef.current?.value !== challenge) return;
          onConfirm();
        }}
      >
        <span className="kicker">{t("React_Confirm_Kicker")}</span>
        <h2>{title}</h2>
        <p>{description}</p>
        {challenge && (
          <label className="field">
            <span>
              {t("React_Confirm_TypePrefix")} <strong>{challenge}</strong>{" "}
              {t("React_Confirm_TypeSuffix")}
            </span>
            <input ref={challengeRef} autoComplete="off" />
          </label>
        )}
        <div className="dialog__actions">
          <ActionButton variant="ghost" type="button" onClick={onCancel}>
            {cancelLabel}
          </ActionButton>
          <ActionButton variant="danger" type="submit">
            {confirmLabel}
          </ActionButton>
        </div>
      </form>
    </dialog>
  );
}

export function useUnsavedChanges(isDirty: boolean): void {
  const { t } = useTranslation();
  const blocker = useBlocker(isDirty);
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
