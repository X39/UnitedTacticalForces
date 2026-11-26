import { useEffect, type Dispatch, type SetStateAction } from "react";

/**
 * Closes a mobile navigation on Escape and when the viewport grows past the narrow layout, and locks the page scroll
 * while the navigation is open on a narrow viewport.
 *
 * Escape closes one layer at a time. Inner layers such as a popover listen on the document or on their own element and
 * call `preventDefault()` for the Escape they handle. This hook listens on the window, which runs after the document
 * listeners for the same event, and ignores an Escape that was already handled, so the navigation closes last.
 *
 * @param open Whether the navigation is currently open; nothing is registered while it is closed.
 * @param setOpen Setter that closes the navigation.
 * @param mediaQuery The query that describes the narrow layout in which the navigation is an overlay.
 */
export function useResponsiveNavigation(
  open: boolean,
  setOpen: Dispatch<SetStateAction<boolean>>,
  mediaQuery = "(max-width: 1180px)",
) {
  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !event.defaultPrevented) setOpen(false);
    };
    const responsiveNavigation = window.matchMedia(mediaQuery);
    const handleViewportChange = (event: MediaQueryListEvent) => {
      if (!event.matches) setOpen(false);
    };
    const previousOverflow = document.body.style.overflow;
    if (responsiveNavigation.matches) document.body.style.overflow = "hidden";
    window.addEventListener("keydown", handleKeyDown);
    responsiveNavigation.addEventListener("change", handleViewportChange);

    return () => {
      window.removeEventListener("keydown", handleKeyDown);
      responsiveNavigation.removeEventListener("change", handleViewportChange);
      document.body.style.overflow = previousOverflow;
    };
  }, [mediaQuery, open, setOpen]);
}
