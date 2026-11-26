import { useEffect, useRef, type RefObject } from "react";

/**
 * The elements a keyboard user can reach with Tab: links, image-map areas, buttons, inputs (except hidden ones),
 * selects, textareas, summaries, iframes, contenteditable hosts and elements with a tabindex.
 */
const focusableSelector = [
  "a[href]",
  "area[href]",
  "button",
  'input:not([type="hidden"])',
  "select",
  "textarea",
  "summary",
  "iframe",
  '[contenteditable="true"]',
  "[tabindex]",
].join(", ");

/** How many animation frames the initial focus move is retried while the navigation is still transitioning in. */
const focusRetryFrames = 10;

/** Whether an element can take part in the Tab cycle: enabled, not removed by a negative tabindex, not inert. */
function isTabbable(element: HTMLElement): boolean {
  return (
    element.tabIndex >= 0 && !element.matches(":disabled") && element.closest("[inert]") === null
  );
}

/** Every Tab stop inside a container, in document order. */
function tabbableWithin(container: HTMLElement | null): HTMLElement[] {
  return Array.from(container?.querySelectorAll<HTMLElement>(focusableSelector) ?? []).filter(
    isTabbable,
  );
}

/** Inputs of {@link useDrawerFocus}. */
export interface DrawerFocusOptions {
  /** Whether the navigation is currently open; focus is only managed while it is open or just closed. */
  open: boolean;
  /** The open navigation; its tabbable descendants form the Tab cycle. */
  drawer: RefObject<HTMLElement | null>;
  /** The toggle that opens the navigation; it receives focus back when the navigation closes. */
  opener: RefObject<HTMLElement | null>;
  /** The element focused when the navigation opens; defaults to the first tabbable element of the drawer. */
  initialFocus?: RefObject<HTMLElement | null> | undefined;
  /** Puts the opener at the start of the Tab cycle, for a navigation whose toggle stays outside the drawer. */
  includeOpener?: boolean | undefined;
}

/**
 * Manages keyboard focus for a mobile navigation that overlays the page.
 *
 * It moves focus into the navigation when it opens, keeps Tab and Shift+Tab inside it while it is open, and returns
 * focus to the toggle after it closes. Focus that the user moved to an element outside the navigation is left alone.
 * The caller makes the covered content inert; closing is the caller's concern (see `useResponsiveNavigation`).
 *
 * While the navigation is open a `keydown` listener sits on the document, not on an element of the navigation. A click
 * on non-focusable navigation surface (a brand block, the blank area below the last link) leaves focus on the body,
 * and a keydown whose target is the body never reaches an element handler. The listener therefore acts for focus
 * inside the navigation, on its toggle (when `includeOpener` is set) or nowhere (the body). Tab from any other
 * element, such as a profile popover, a language select or a sign-in button, keeps that component's own order and
 * the browser's.
 *
 * The cycle covers `a[href]`, `area[href]`, `button`, `input` other than type hidden, `select`, `textarea`,
 * `summary`, `iframe`, `[contenteditable="true"]` and `[tabindex]`, and skips disabled elements, a negative
 * tabindex and anything inside an `[inert]` subtree. Visibility is the caller's concern: a closed drawer is hidden
 * by CSS and this hook only acts while the navigation is open.
 *
 * @param options The open state, the element refs and the Tab cycle shape.
 */
export function useDrawerFocus({
  open,
  drawer,
  opener,
  initialFocus,
  includeOpener = false,
}: DrawerFocusOptions): void {
  const wasOpen = useRef(false);

  useEffect(() => {
    let retry: number | undefined;
    if (open && !wasOpen.current) {
      const target = initialFocus?.current ?? tabbableWithin(drawer.current)[0] ?? null;
      target?.focus();
      if (target && document.activeElement !== target) {
        // With reduced motion the drawer's visibility transition still reports `hidden` for its first frames, and a
        // hidden element cannot take focus. Retry for a few frames, unless the user already moved focus elsewhere.
        let attemptsLeft = focusRetryFrames;
        const tryAgain = () => {
          const active = document.activeElement;
          if (active && active !== document.body && active !== opener.current) return;
          target.focus();
          if (document.activeElement !== target && --attemptsLeft > 0)
            retry = requestAnimationFrame(tryAgain);
        };
        retry = requestAnimationFrame(tryAgain);
      }
    } else if (!open && wasOpen.current) {
      // This runs after the commit that removed the inert attribute, so the opener can take focus again.
      const active = document.activeElement;
      if (!active || active === document.body || drawer.current?.contains(active))
        opener.current?.focus();
    }
    wasOpen.current = open;
    return () => {
      if (retry !== undefined) cancelAnimationFrame(retry);
    };
  }, [open, drawer, opener, initialFocus]);

  useEffect(() => {
    if (!open) return;
    const cycleTab = (event: KeyboardEvent) => {
      if (event.key !== "Tab" || event.defaultPrevented) return;
      // While a modal dialog is open the browser's modal focus handling owns Tab. Every dialog in this app opens
      // through showModal(), so an open dialog is always a modal one. The dialog:modal selector is avoided
      // because jsdom does not support it.
      if (document.querySelector("dialog[open]")) return;
      const active = document.activeElement;
      const nowhere = !active || active === document.body || active === document.documentElement;
      const onOpener = includeOpener && active !== null && active === opener.current;
      if (!nowhere && !onOpener && !drawer.current?.contains(active)) return;
      const cycle = [
        ...(includeOpener && opener.current ? [opener.current] : []),
        ...tabbableWithin(drawer.current),
      ];
      const first = cycle[0];
      const last = cycle[cycle.length - 1];
      if (!first || !last) return;
      const position = cycle.indexOf(active as HTMLElement);
      if (position < 0) {
        event.preventDefault();
        (event.shiftKey ? last : first).focus();
      } else if (event.shiftKey && position === 0) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && position === cycle.length - 1) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", cycleTab);
    return () => document.removeEventListener("keydown", cycleTab);
  }, [open, drawer, opener, includeOpener]);
}
