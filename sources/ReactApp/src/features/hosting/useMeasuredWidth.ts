import { useEffect, useRef, useState, type RefObject } from "react";

/**
 * Measures the content width of an element with a `ResizeObserver`.
 *
 * Where no `ResizeObserver` exists (for example in a test DOM) the fallback width is used and never changes. The
 * hook starts no timer: the width changes only when the observer reports a new size.
 *
 * @typeParam T The element type the returned ref is attached to.
 * @param fallbackPx The width in pixels used before the first measurement and when the observer is missing.
 * @returns The ref to attach to the element and its current width in whole pixels.
 */
export function useMeasuredWidth<T extends Element>(
  fallbackPx: number,
): [RefObject<T | null>, number] {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(fallbackPx);

  useEffect(() => {
    const element = ref.current;
    if (element === null || typeof ResizeObserver === "undefined") return undefined;
    const observer = new ResizeObserver((entries) => {
      const measured = entries[entries.length - 1]?.contentRect.width ?? 0;
      if (measured > 0) setWidth(Math.round(measured));
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  return [ref, width];
}
