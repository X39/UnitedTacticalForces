import { useEffect } from "react";
import { useBlocker, type Location } from "react-router";

/** Options of {@link useLeaveGuard}. */
export interface LeaveGuardOptions {
  /** True while the guarded view holds changes that would be lost; nothing is blocked while false. */
  dirty: boolean;
  /**
   * Decides whether a navigation leaves the guarded area. Navigations for which it returns false (for example a
   * search-only change inside the same section) are never blocked. When omitted every navigation is blocked while
   * dirty.
   */
  shouldBlock?:
    ((args: { currentLocation: Location; nextLocation: Location }) => boolean) | undefined;
}

/** The state and controls returned by {@link useLeaveGuard}. */
export interface LeaveGuard {
  /** True while a navigation is held back and the caller should show its leave dialog. */
  blocked: boolean;
  /** Cancels the held navigation and keeps the current location and history unchanged. */
  stay(): void;
  /** Lets the held navigation continue; the caller discards its own draft first. */
  proceed(): void;
}

/**
 * Guards a view against losing unsaved changes.
 *
 * Router navigations (links, `navigate()`, Back and Forward) that satisfy `shouldBlock` are held back while
 * `dirty` is true, and a `beforeunload` handler is registered only while `dirty` is true so closing or reloading
 * the tab shows the browser's native prompt. The hook never uses the browser's blocking confirm prompt and owns no
 * copy: the caller renders its own dialog from `blocked`, and "Review" equals {@link LeaveGuard.stay} followed by the
 * caller opening its review.
 *
 * @param options The dirty flag and the optional leave predicate.
 * @returns The blocked flag with the `stay` and `proceed` controls.
 */
export function useLeaveGuard({ dirty, shouldBlock }: LeaveGuardOptions): LeaveGuard {
  const blocker = useBlocker(({ currentLocation, nextLocation }) => {
    if (!dirty) return false;
    return shouldBlock ? shouldBlock({ currentLocation, nextLocation }) : true;
  });

  useEffect(() => {
    if (!dirty) return;
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", beforeUnload);
    return () => window.removeEventListener("beforeunload", beforeUnload);
  }, [dirty]);

  return {
    blocked: blocker.state === "blocked",
    stay: () => blocker.reset?.(),
    proceed: () => blocker.proceed?.(),
  };
}
