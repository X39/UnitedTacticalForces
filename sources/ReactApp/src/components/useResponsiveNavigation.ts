import { useEffect, type Dispatch, type SetStateAction } from "react";

export function useResponsiveNavigation(
  open: boolean,
  setOpen: Dispatch<SetStateAction<boolean>>,
  mediaQuery = "(max-width: 1180px)",
) {
  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    const responsiveNavigation = window.matchMedia(mediaQuery);
    const handleViewportChange = (event: MediaQueryListEvent) => {
      if (!event.matches) setOpen(false);
    };
    const previousOverflow = document.body.style.overflow;
    if (responsiveNavigation.matches) document.body.style.overflow = "hidden";
    document.addEventListener("keydown", handleKeyDown);
    responsiveNavigation.addEventListener("change", handleViewportChange);

    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      responsiveNavigation.removeEventListener("change", handleViewportChange);
      document.body.style.overflow = previousOverflow;
    };
  }, [mediaQuery, open, setOpen]);
}
