import type { ReactNode } from "react";
import "@/components/shared-primitives.css";

/** Props of {@link PageStack}. */
export interface PageStackProps {
  /** The blocks of the page body, rendered in order below the page header. */
  children: ReactNode;
  /** Extra class name appended after the `page-stack` class. */
  className?: string | undefined;
}

/**
 * The body wrapper of a page, rendered below its header.
 *
 * It lays the page blocks out in one column with the shared block gap token, so no page sets its own top or bottom
 * margin on its root. The styles live in `shared-primitives.css`, which this module loads itself.
 *
 * @param props The wrapper props.
 * @returns A `div` with the `page-stack` class around the children.
 */
export function PageStack({ children, className }: PageStackProps) {
  return <div className={["page-stack", className].filter(Boolean).join(" ")}>{children}</div>;
}
