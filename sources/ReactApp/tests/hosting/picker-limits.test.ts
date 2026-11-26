import { describe, expect, it } from "vitest";
import {
  limitPickerResults,
  pickerRequestTake,
  pickerResultLimit,
} from "@/features/hosting/pickerLimits";

describe("picker result limits", () => {
  it("requests one match more than it shows", () => {
    expect(pickerRequestTake).toBe(pickerResultLimit + 1);
  });

  it("keeps a full-limit answer whole and reports nothing further", () => {
    const items = Array.from({ length: pickerResultLimit }, (_, index) => index);

    expect(limitPickerResults(items)).toEqual({ items, hasMore: false });
  });

  it("cuts a longer answer to the limit and reports that more exist", () => {
    const items = Array.from({ length: pickerRequestTake }, (_, index) => index);

    const result = limitPickerResults(items);

    expect(result.items).toHaveLength(pickerResultLimit);
    expect(result.items.at(-1)).toBe(pickerResultLimit - 1);
    expect(result.hasMore).toBe(true);
  });

  it("handles an empty answer", () => {
    expect(limitPickerResults([])).toEqual({ items: [], hasMore: false });
  });
});
