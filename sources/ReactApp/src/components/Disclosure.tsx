import { ChevronDown20Regular } from "@fluentui/react-icons";
import { useId, type ReactNode, type Ref } from "react";
import "@/components/server/form-fields.css";

/** Props of {@link Disclosure}. */
export interface DisclosureProps {
  /** Title shown in the toggle button. It is the accessible name of the button and of the content region. */
  title: ReactNode;
  /** Optional line under the title that stays visible while the disclosure is closed. */
  summary?: ReactNode | undefined;
  /** Whether the content is shown. The component is controlled. */
  open: boolean;
  /**
   * Called with the new state when the user toggles the disclosure.
   *
   * @param open Whether the disclosure is now open.
   */
  onOpenChange(open: boolean): void;
  /** The content revealed while the disclosure is open. */
  children: ReactNode;
  /** Optional extra class name for the outer element. */
  className?: string | undefined;
  /**
   * Wraps the toggle button in an `h2` or `h3` so the disclosure takes part in the heading outline. The heading
   * is named by the title alone. Omit it for a disclosure that is not a section heading.
   */
  headingLevel?: 2 | 3 | undefined;
  /** Optional id of the outer element, for example an anchor target for in-page navigation. */
  id?: string | undefined;
  /**
   * Optional badge between the title and the chevron, for example a staged change count. It describes the toggle
   * button and is never part of its accessible name.
   */
  badge?: ReactNode | undefined;
  /** Visual shape. `inline` is a compact bordered row; `panel` is the header of a raised panel. Defaults to `inline`. */
  variant?: "inline" | "panel" | undefined;
  /** Receives the toggle button so a parent can move focus to the header. */
  buttonRef?: Ref<HTMLButtonElement> | undefined;
}

/**
 * A controlled button disclosure following the WAI-ARIA disclosure pattern.
 *
 * A native button exposes `aria-expanded` and `aria-controls`, is named by the title and described by the always
 * visible summary line and badge, and toggles through `onOpenChange`. The native button provides Enter and Space
 * activation. The content is a labelled region that carries the `hidden` attribute while the disclosure is closed and
 * stays mounted so child state survives a collapse. The chevron is decorative; no native disclosure element and no
 * browser marker triangle is involved. The parent owns the state, so it can open the disclosure programmatically, for
 * example to reveal a field with an error. With `headingLevel` the toggle sits inside a heading, following the
 * WAI-ARIA accordion pattern.
 */
export function Disclosure({
  title,
  summary,
  open,
  onOpenChange,
  children,
  className,
  headingLevel,
  id,
  badge,
  variant = "inline",
  buttonRef,
}: DisclosureProps) {
  const baseId = useId();
  const titleId = `${baseId}-title`;
  const summaryLineId = `${baseId}-summary`;
  const badgeId = `${baseId}-badge`;
  const contentId = `${baseId}-content`;
  const describedBy =
    [summary != null ? summaryLineId : null, badge != null ? badgeId : null]
      .filter((value) => value !== null)
      .join(" ") || undefined;
  const classNames = ["disclosure", variant === "panel" ? "disclosure--panel" : null, className]
    .filter((value) => value)
    .join(" ");
  const button = (
    <button
      type="button"
      className="disclosure__summary"
      ref={buttonRef}
      aria-expanded={open}
      aria-controls={contentId}
      aria-labelledby={titleId}
      aria-describedby={describedBy}
      onClick={() => onOpenChange(!open)}
    >
      <span className="disclosure__title" id={titleId}>
        {title}
      </span>
      {badge != null && (
        <span className="disclosure__badge" id={badgeId}>
          {badge}
        </span>
      )}
      <ChevronDown20Regular className="disclosure__chevron" aria-hidden="true" />
      {summary != null && (
        <span className="disclosure__summary-line" id={summaryLineId}>
          {summary}
        </span>
      )}
    </button>
  );
  const HeadingTag = headingLevel === 2 ? "h2" : headingLevel === 3 ? "h3" : null;
  return (
    <div id={id} className={classNames} data-open={open ? "true" : undefined}>
      {HeadingTag != null ? (
        <HeadingTag className="disclosure__heading">{button}</HeadingTag>
      ) : (
        button
      )}
      <div
        className="disclosure__content"
        id={contentId}
        role="region"
        aria-labelledby={titleId}
        hidden={!open}
      >
        {children}
      </div>
    </div>
  );
}
