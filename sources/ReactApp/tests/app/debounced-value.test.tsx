import { act, renderHook } from "@testing-library/react";
import { useDebouncedValue } from "@/app/useDebouncedValue";
import { afterEach, beforeEach } from "vitest";

describe("useDebouncedValue", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("returns the initial value immediately", () => {
    const { result } = renderHook(() => useDebouncedValue("alpha", 250));

    expect(result.current).toBe("alpha");
  });

  it("returns a new value only after the delay", () => {
    const { result, rerender } = renderHook(({ value }) => useDebouncedValue(value, 250), {
      initialProps: { value: "a" },
    });

    rerender({ value: "ab" });
    expect(result.current).toBe("a");

    act(() => {
      vi.advanceTimersByTime(249);
    });
    expect(result.current).toBe("a");

    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(result.current).toBe("ab");
  });

  it("restarts the timer on each change so only the last value settles", () => {
    const { result, rerender } = renderHook(({ value }) => useDebouncedValue(value, 250), {
      initialProps: { value: "a" },
    });

    rerender({ value: "ab" });
    act(() => {
      vi.advanceTimersByTime(200);
    });
    rerender({ value: "abc" });
    act(() => {
      vi.advanceTimersByTime(200);
    });
    expect(result.current).toBe("a");

    act(() => {
      vi.advanceTimersByTime(50);
    });
    expect(result.current).toBe("abc");
  });

  it("clears the pending timer when the component unmounts", () => {
    const { rerender, unmount } = renderHook(({ value }) => useDebouncedValue(value, 250), {
      initialProps: { value: "a" },
    });
    rerender({ value: "ab" });

    unmount();

    expect(vi.getTimerCount()).toBe(0);
  });
});
