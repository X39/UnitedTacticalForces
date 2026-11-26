import { useEffect, useLayoutEffect, useRef, type RefObject } from "react";

/** Attribute on a fleet card's article whose value is the server's primary key. */
const cardAttribute = "data-server-card";

/** Attribute on a fleet card's Open server link whose value is the server's primary key. */
const linkAttribute = "data-server-id";

/**
 * Tells whether focus is currently lost, meaning it rests on the body, on nothing or on a detached element.
 *
 * @returns True when no connected element other than the body holds focus.
 */
function isFocusLost(): boolean {
  const active = document.activeElement;
  return active === null || active === document.body || !active.isConnected;
}

/**
 * Keeps keyboard focus on a fleet card that changes section after a refetch (UI-SPEC "After a confirmed action"
 * rule 5).
 *
 * The hook remembers the card that last contained focus through one `focusin` listener on the document, so a
 * conditionally rendered container never matters. After every change of `signature` it checks, before the browser
 * paints, whether focus was lost because the focused control unmounted; only then it focuses the remembered
 * server's Open server link inside the container. Focus that rests on any other connected element is never moved.
 * The link is found by comparing the `data-server-id` attribute value as a string; the id is never interpolated
 * into a selector, so untrusted ids cannot break or redirect the lookup.
 *
 * @param containerRef Ref to the element that holds every fleet section. It is read only inside effects and event
 *   handlers.
 * @param signature A value that changes whenever the set or placement of cards changes, for example a string of
 *   server ids per section.
 */
export function useFleetFocusFallback(
  containerRef: RefObject<HTMLElement | null>,
  signature: string,
): void {
  const lastFocusedId = useRef<string | null>(null);

  useEffect(() => {
    const onFocusIn = (event: FocusEvent) => {
      const container = containerRef.current;
      const target = event.target;
      if (!container || !(target instanceof Element) || !container.contains(target)) return;
      lastFocusedId.current =
        target.closest(`[${cardAttribute}]`)?.getAttribute(cardAttribute) ?? null;
    };
    document.addEventListener("focusin", onFocusIn);
    return () => document.removeEventListener("focusin", onFocusIn);
  }, [containerRef]);

  useLayoutEffect(() => {
    const id = lastFocusedId.current;
    const container = containerRef.current;
    if (id === null || !container || !isFocusLost()) return;
    for (const link of container.querySelectorAll<HTMLElement>(`a[${linkAttribute}]`)) {
      if (link.getAttribute(linkAttribute) === id) {
        link.focus();
        return;
      }
    }
  }, [containerRef, signature]);
}
