import { act, renderHook } from "@testing-library/react";
import { StrictMode, type ReactNode } from "react";
import { useFleetSubmitted } from "@/features/hosting/useFleetSubmitted";

/*
 * Submitted fleet cards (D-12). A card is submitted from the moment its action was accepted until the next
 * successful fleet fetch lands, and never while the fleet query is in error. The state is derived from the
 * query's dataUpdatedAt, so nothing has to clear it.
 */

interface HookProps {
  dataUpdatedAt: number;
  isError: boolean;
}

function renderSubmitted(initial: HookProps, options: { strict?: boolean } = {}) {
  const wrapper = options.strict
    ? ({ children }: { children: ReactNode }) => <StrictMode>{children}</StrictMode>
    : undefined;
  return renderHook(
    ({ dataUpdatedAt, isError }: HookProps) => useFleetSubmitted(dataUpdatedAt, isError),
    { initialProps: initial, ...(wrapper ? { wrapper } : {}) },
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("useFleetSubmitted", () => {
  it("keeps a card submitted until the fleet data advances", () => {
    const { result, rerender } = renderSubmitted({ dataUpdatedAt: 100, isError: false });
    expect(result.current.submittedIds.size).toBe(0);

    act(() => result.current.markSubmitted("a"));
    expect(result.current.submittedIds.has("a")).toBe(true);

    rerender({ dataUpdatedAt: 100, isError: false });
    expect(result.current.submittedIds.has("a")).toBe(true);

    rerender({ dataUpdatedAt: 200, isError: false });
    expect(result.current.submittedIds.has("a")).toBe(false);
    expect(result.current.submittedIds.size).toBe(0);
  });

  it("submits no card while the fleet query is in error", () => {
    const { result, rerender } = renderSubmitted({ dataUpdatedAt: 100, isError: false });
    act(() => result.current.markSubmitted("a"));
    expect(result.current.submittedIds.has("a")).toBe(true);

    rerender({ dataUpdatedAt: 100, isError: true });
    expect(result.current.submittedIds.size).toBe(0);
  });

  it("keeps cards marked at different times independent", () => {
    const { result, rerender } = renderSubmitted({ dataUpdatedAt: 100, isError: false });
    act(() => result.current.markSubmitted("a"));

    rerender({ dataUpdatedAt: 200, isError: false });
    expect(result.current.submittedIds.has("a")).toBe(false);

    act(() => result.current.markSubmitted("b"));
    expect(result.current.submittedIds.has("b")).toBe(true);
    expect(result.current.submittedIds.has("a")).toBe(false);

    rerender({ dataUpdatedAt: 200, isError: false });
    expect(result.current.submittedIds.has("b")).toBe(true);

    rerender({ dataUpdatedAt: 300, isError: false });
    expect(result.current.submittedIds.has("b")).toBe(false);
  });

  it("records the dataUpdatedAt that was current when each server was marked", () => {
    const { result, rerender } = renderSubmitted({ dataUpdatedAt: 100, isError: false });
    act(() => result.current.markSubmitted("a"));
    act(() => result.current.markSubmitted("b"));
    expect(result.current.markedAt.get("a")).toBe(100);
    expect(result.current.markedAt.get("b")).toBe(100);

    rerender({ dataUpdatedAt: 250, isError: false });
    act(() => result.current.markSubmitted("c"));
    expect(result.current.markedAt.get("c")).toBe(250);
  });

  it("counts a repeated mark once and prunes marks older than the current data", () => {
    const { result, rerender } = renderSubmitted({ dataUpdatedAt: 100, isError: false });
    act(() => result.current.markSubmitted("a"));
    act(() => result.current.markSubmitted("a"));
    expect([...result.current.submittedIds]).toEqual(["a"]);
    expect(result.current.markedAt.size).toBe(1);

    rerender({ dataUpdatedAt: 200, isError: false });
    expect(result.current.markedAt.has("a")).toBe(true);

    act(() => result.current.markSubmitted("b"));
    expect(result.current.markedAt.has("a")).toBe(false);
    expect([...result.current.markedAt.keys()]).toEqual(["b"]);
  });

  it("keeps markSubmitted stable and renders under StrictMode without warnings", () => {
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const { result, rerender } = renderSubmitted(
      { dataUpdatedAt: 100, isError: false },
      { strict: true },
    );
    const first = result.current.markSubmitted;

    act(() => result.current.markSubmitted("a"));
    expect(result.current.markSubmitted).toBe(first);
    rerender({ dataUpdatedAt: 200, isError: false });
    expect(result.current.markSubmitted).toBe(first);
    rerender({ dataUpdatedAt: 200, isError: true });
    expect(result.current.markSubmitted).toBe(first);

    act(() => result.current.markSubmitted("b"));
    expect(result.current.markedAt.get("b")).toBe(200);

    expect(error).not.toHaveBeenCalled();
    expect(warn).not.toHaveBeenCalled();
  });
});
