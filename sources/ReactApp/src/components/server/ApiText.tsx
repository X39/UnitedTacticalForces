import type { ReactNode } from "react";

/**
 * Wraps text that originates from the API.
 *
 * API-originated prose is English; marking it `lang="en"` keeps assistive technology from reading it with the
 * wrong voice in a German view. Children are rendered as React content, never as HTML.
 *
 * @param props.children The API-provided text or the element that contains it.
 */
export function ApiText({ children }: { children: ReactNode }) {
  return <span lang="en">{children}</span>;
}
