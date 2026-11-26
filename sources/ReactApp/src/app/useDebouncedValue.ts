import { useEffect, useState } from "react";

/**
 * Returns `value` only after it has stayed unchanged for `delayMs` milliseconds.
 *
 * The first render returns the initial value immediately. Every change restarts the timer, and the pending timer is
 * cleared when the value changes again or the component unmounts, so a fast typist produces one settled value. The
 * state is set inside the timer callback and never in the effect body, which keeps the hook safe under the React
 * hooks lint rules.
 *
 * @param value The fast changing input, for example the text of a search field.
 * @param delayMs The quiet period in milliseconds before a new value is returned.
 * @returns The latest value that stayed stable for the whole delay.
 */
export function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}
