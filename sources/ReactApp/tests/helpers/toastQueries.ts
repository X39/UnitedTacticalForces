/*
 * Toasts sit inside the persistent polite and assertive live regions of the toast viewport and carry no role of their
 * own (WR-04). Tests therefore locate a toast by its text and read its politeness from the region around it instead of
 * querying role status or role alert.
 */
import { screen } from "@testing-library/react";

/**
 * Finds the toast that shows the given text.
 *
 * @param text The exact text or a pattern of the toast message.
 * @returns The `.app-toast` element that contains the message.
 * @throws When no toast shows the text, or when the text is not inside a toast.
 */
export async function findToast(text: string | RegExp): Promise<HTMLElement> {
  const message = await screen.findByText(text);
  const toast = message.closest<HTMLElement>(".app-toast");
  if (!toast) throw new Error(`The text ${String(text)} is not inside a toast.`);
  return toast;
}

/**
 * Reads how a toast is announced.
 *
 * @param toast A toast element returned by {@link findToast}.
 * @returns The `aria-live` value of the closest live region around the toast, or null when it has none.
 */
export function toastPoliteness(toast: HTMLElement): string | null {
  return toast.closest("[aria-live]")?.getAttribute("aria-live") ?? null;
}
