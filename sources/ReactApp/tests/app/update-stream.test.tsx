import { act, renderHook } from "@testing-library/react";
import { StrictMode, type ReactNode } from "react";
import { beforeEach } from "vitest";
import {
  createCoalescer,
  nextDelayMs,
  parseUpdateFrame,
  toSocketUrl,
  useUpdateStream,
  type UpdateStreamFrame,
  type UpdateStreamOptions,
} from "@/app/updateStream";
import { FakeWebSocket } from "../helpers/fakeWebSocket";

/*
 * Reconnecting update stream client (D-15, D-16). Every retry delay is exact because Math.random is fixed at 0.5,
 * which makes the jitter factor 1.
 */

const apiBase = new URL("http://localhost:3000/api/");

let visibility: DocumentVisibilityState = "visible";

function streamOptions(overrides: Partial<UpdateStreamOptions> = {}): UpdateStreamOptions {
  return {
    baseUrl: apiBase,
    path: "game-servers/fleet",
    enabled: true,
    onMessage: vi.fn<(frame: UpdateStreamFrame) => void>(),
    ...overrides,
  };
}

function renderStream(initial: UpdateStreamOptions, options: { strict?: boolean } = {}) {
  const wrapper = options.strict
    ? ({ children }: { children: ReactNode }) => <StrictMode>{children}</StrictMode>
    : undefined;
  return renderHook((props: UpdateStreamOptions) => useUpdateStream(props), {
    initialProps: initial,
    ...(wrapper ? { wrapper } : {}),
  });
}

function latestSocket(): FakeWebSocket {
  const socket = FakeWebSocket.instances.at(-1);
  if (!socket) throw new Error("Expected a socket.");
  return socket;
}

function liveSockets(): FakeWebSocket[] {
  return FakeWebSocket.instances.filter(
    (socket) => !socket.closedByClient && socket.readyState !== FakeWebSocket.CLOSED,
  );
}

function advance(milliseconds: number) {
  act(() => {
    vi.advanceTimersByTime(milliseconds);
  });
}

function goOnline() {
  act(() => {
    window.dispatchEvent(new Event("online"));
  });
}

function changeVisibility(next: DocumentVisibilityState) {
  visibility = next;
  act(() => {
    document.dispatchEvent(new Event("visibilitychange"));
  });
}

describe("update stream helpers", () => {
  it("builds ws and wss subscription urls with the encoded path", () => {
    expect(toSocketUrl(new URL("http://localhost:3000/api/"), "game-servers/fleet")).toBe(
      "ws://localhost:3000/api/update-stream/subscribe?path=game-servers%2Ffleet",
    );
    expect(toSocketUrl(new URL("https://utf.example.test/api/"), "game-servers/fleet")).toBe(
      "wss://utf.example.test/api/update-stream/subscribe?path=game-servers%2Ffleet",
    );
    expect(toSocketUrl(new URL("https://utf.example.test/api"), "events/1")).toBe(
      "wss://utf.example.test/api/update-stream/subscribe?path=events%2F1",
    );
  });

  it("follows the 1, 2, 4, 8, 16 and 30 second schedule with 20 percent jitter", () => {
    const bases = [1000, 2000, 4000, 8000, 16000, 30000, 30000];
    expect(bases.map((_, attempt) => nextDelayMs(attempt, () => 0.5))).toEqual(bases);
    bases.forEach((base, attempt) => {
      expect(nextDelayMs(attempt, () => 0)).toBeCloseTo(base * 0.8);
      expect(nextDelayMs(attempt, () => 1)).toBeCloseTo(base * 1.2);
    });
    expect(nextDelayMs(40, () => 0.5)).toBe(30000);
  });

  it("parses well-formed frames and rejects everything else without throwing", () => {
    expect(parseUpdateFrame('{"type":"Some.Message","value":{"id":"1"}}')).toEqual({
      type: "Some.Message",
      value: { id: "1" },
    });
    expect(parseUpdateFrame(42)).toBeNull();
    expect(parseUpdateFrame(new ArrayBuffer(2))).toBeNull();
    expect(parseUpdateFrame("not json")).toBeNull();
    expect(parseUpdateFrame('[{"type":"Some.Message"}]')).toBeNull();
    expect(parseUpdateFrame('{"value":{"id":"1"}}')).toBeNull();
    expect(parseUpdateFrame('{"type":5}')).toBeNull();
    expect(parseUpdateFrame("null")).toBeNull();
  });

  describe("createCoalescer", () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it("runs on the leading edge and collapses a burst into one trailing run", () => {
      const run = vi.fn();
      const coalescer = createCoalescer(run, 500);

      coalescer.trigger();
      expect(run).toHaveBeenCalledTimes(1);
      coalescer.trigger();
      coalescer.trigger();
      coalescer.trigger();
      expect(run).toHaveBeenCalledTimes(1);

      vi.advanceTimersByTime(499);
      expect(run).toHaveBeenCalledTimes(1);
      vi.advanceTimersByTime(1);
      expect(run).toHaveBeenCalledTimes(2);

      vi.advanceTimersByTime(500);
      expect(run).toHaveBeenCalledTimes(2);
      expect(vi.getTimerCount()).toBe(0);

      coalescer.trigger();
      expect(run).toHaveBeenCalledTimes(3);
      coalescer.cancel();
    });

    it("drops the pending trailing run and its timer on cancel", () => {
      const run = vi.fn();
      const coalescer = createCoalescer(run, 500);

      coalescer.trigger();
      coalescer.trigger();
      coalescer.cancel();

      expect(vi.getTimerCount()).toBe(0);
      vi.advanceTimersByTime(2000);
      expect(run).toHaveBeenCalledTimes(1);

      coalescer.trigger();
      expect(run).toHaveBeenCalledTimes(2);
      coalescer.cancel();
    });
  });
});

describe("useUpdateStream", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(Math, "random").mockReturnValue(0.5);
    visibility = "visible";
    Object.defineProperty(document, "visibilityState", {
      configurable: true,
      get: () => visibility,
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    Reflect.deleteProperty(document, "visibilityState");
  });

  it("moves from connecting to open to reconnecting and back with a reset retry delay", () => {
    const { result } = renderStream(streamOptions());
    expect(result.current.status).toBe("connecting");
    expect(FakeWebSocket.instances).toHaveLength(1);

    act(() => latestSocket().open());
    expect(result.current.status).toBe("open");
    expect(result.current.disconnectEpoch).toBe(0);

    act(() => latestSocket().serverClose());
    expect(result.current.status).toBe("reconnecting");
    expect(result.current.disconnectEpoch).toBe(1);
    expect(vi.getTimerCount()).toBe(1);

    advance(999);
    expect(FakeWebSocket.instances).toHaveLength(1);
    advance(1);
    expect(FakeWebSocket.instances).toHaveLength(2);
    expect(result.current.status).toBe("reconnecting");

    act(() => latestSocket().fail());
    expect(result.current.disconnectEpoch).toBe(1);
    advance(1999);
    expect(FakeWebSocket.instances).toHaveLength(2);
    advance(1);
    expect(FakeWebSocket.instances).toHaveLength(3);

    act(() => latestSocket().open());
    expect(result.current.status).toBe("open");
    expect(vi.getTimerCount()).toBe(0);

    act(() => latestSocket().serverClose());
    expect(result.current.status).toBe("reconnecting");
    expect(result.current.disconnectEpoch).toBe(2);
    advance(1000);
    expect(FakeWebSocket.instances).toHaveLength(4);
  });

  it("reports unavailable after five failed attempts without an open and keeps retrying at the cap", () => {
    const { result } = renderStream(streamOptions());
    const delays = [1000, 2000, 4000, 8000];

    act(() => latestSocket().fail());
    expect(result.current.status).toBe("reconnecting");
    expect(result.current.disconnectEpoch).toBe(1);

    delays.forEach((delay, index) => {
      advance(delay);
      expect(FakeWebSocket.instances).toHaveLength(index + 2);
      expect(result.current.status).toBe("reconnecting");
      act(() => latestSocket().fail());
    });

    expect(FakeWebSocket.instances).toHaveLength(5);
    expect(result.current.status).toBe("unavailable");
    expect(result.current.disconnectEpoch).toBe(1);

    advance(16000);
    expect(FakeWebSocket.instances).toHaveLength(6);
    act(() => latestSocket().fail());
    expect(result.current.status).toBe("unavailable");

    advance(29999);
    expect(FakeWebSocket.instances).toHaveLength(6);
    advance(1);
    expect(FakeWebSocket.instances).toHaveLength(7);
    act(() => latestSocket().fail());
    advance(30000);
    expect(FakeWebSocket.instances).toHaveLength(8);
    expect(vi.getTimerCount()).toBe(0);

    act(() => latestSocket().open());
    expect(result.current.status).toBe("open");
    expect(result.current.disconnectEpoch).toBe(1);
  });

  it("never reports unavailable once the stream has opened in this session", () => {
    const { result } = renderStream(streamOptions());
    act(() => latestSocket().open());
    act(() => latestSocket().serverClose());

    [1000, 2000, 4000, 8000, 16000, 30000].forEach((delay) => {
      advance(delay);
      act(() => latestSocket().fail());
    });

    expect(result.current.status).toBe("reconnecting");
    expect(result.current.disconnectEpoch).toBe(1);
  });

  it("attempts immediately on online, on becoming visible and on reconnectNow while disconnected", () => {
    const { result } = renderStream(streamOptions());
    act(() => latestSocket().open());
    act(() => latestSocket().serverClose());
    expect(vi.getTimerCount()).toBe(1);

    goOnline();
    expect(FakeWebSocket.instances).toHaveLength(2);
    expect(vi.getTimerCount()).toBe(0);

    act(() => latestSocket().fail());
    expect(vi.getTimerCount()).toBe(1);
    changeVisibility("hidden");
    expect(FakeWebSocket.instances).toHaveLength(2);
    expect(vi.getTimerCount()).toBe(1);

    changeVisibility("visible");
    expect(FakeWebSocket.instances).toHaveLength(3);
    expect(vi.getTimerCount()).toBe(0);

    act(() => latestSocket().fail());
    act(() => result.current.reconnectNow());
    expect(FakeWebSocket.instances).toHaveLength(4);
    expect(vi.getTimerCount()).toBe(0);

    goOnline();
    act(() => result.current.reconnectNow());
    expect(FakeWebSocket.instances).toHaveLength(4);
  });

  it("ignores the retry triggers while connecting or open", () => {
    const { result } = renderStream(streamOptions());

    goOnline();
    changeVisibility("visible");
    act(() => result.current.reconnectNow());
    expect(FakeWebSocket.instances).toHaveLength(1);

    act(() => latestSocket().open());
    goOnline();
    changeVisibility("visible");
    act(() => result.current.reconnectNow());
    expect(FakeWebSocket.instances).toHaveLength(1);
    expect(result.current.status).toBe("open");
  });

  it("calls onOpen on every open and onMessage only for valid frames, using the latest callbacks", () => {
    const onOpen = vi.fn();
    const onMessage = vi.fn<(frame: UpdateStreamFrame) => void>();
    const initial = streamOptions({ onOpen, onMessage });
    const { rerender } = renderStream(initial);

    act(() => latestSocket().open());
    expect(onOpen).toHaveBeenCalledTimes(1);

    act(() => latestSocket().message('{"type":"Some.Message","value":{"id":"1"}}'));
    act(() => latestSocket().message("not json"));
    act(() => latestSocket().message('{"value":1}'));
    expect(onMessage).toHaveBeenCalledTimes(1);
    expect(onMessage).toHaveBeenCalledWith({ type: "Some.Message", value: { id: "1" } });

    act(() => latestSocket().serverClose());
    advance(1000);
    act(() => latestSocket().open());
    expect(onOpen).toHaveBeenCalledTimes(2);

    const nextOnOpen = vi.fn();
    const nextOnMessage = vi.fn<(frame: UpdateStreamFrame) => void>();
    rerender({ ...initial, onOpen: nextOnOpen, onMessage: nextOnMessage });
    expect(FakeWebSocket.instances).toHaveLength(2);

    act(() => latestSocket().message('{"type":"Other.Message"}'));
    expect(nextOnMessage).toHaveBeenCalledWith({ type: "Other.Message", value: undefined });
    expect(onMessage).toHaveBeenCalledTimes(1);

    act(() => latestSocket().serverClose());
    advance(1000);
    act(() => latestSocket().open());
    expect(nextOnOpen).toHaveBeenCalledTimes(1);
    expect(onOpen).toHaveBeenCalledTimes(2);
  });

  it("closes the socket, clears the retry timer and removes its listeners on unmount", () => {
    const removeWindowListener = vi.spyOn(window, "removeEventListener");
    const removeDocumentListener = vi.spyOn(document, "removeEventListener");
    const { unmount } = renderStream(streamOptions());
    const socket = latestSocket();
    act(() => socket.open());

    unmount();

    expect(socket.closedByClient).toBe(true);
    expect(vi.getTimerCount()).toBe(0);
    expect(removeWindowListener).toHaveBeenCalledWith("online", expect.any(Function));
    expect(removeDocumentListener).toHaveBeenCalledWith("visibilitychange", expect.any(Function));
  });

  it("never reconnects after an unmount during a disconnection", () => {
    const { unmount } = renderStream(streamOptions());
    act(() => latestSocket().fail());
    expect(vi.getTimerCount()).toBe(1);

    unmount();

    expect(vi.getTimerCount()).toBe(0);
    advance(60_000);
    goOnline();
    changeVisibility("visible");
    expect(FakeWebSocket.instances).toHaveLength(1);
  });

  it("leaves one live socket and one retry timer at most under StrictMode", () => {
    const { unmount } = renderStream(streamOptions(), { strict: true });

    expect(liveSockets()).toHaveLength(1);
    expect(vi.getTimerCount()).toBe(0);

    const [live] = liveSockets();
    if (!live) throw new Error("Expected one live socket.");
    act(() => live.serverClose());
    expect(vi.getTimerCount()).toBe(1);
    advance(1000);
    expect(liveSockets()).toHaveLength(1);

    unmount();
    expect(liveSockets()).toHaveLength(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("closes the socket and stops retrying when disabled", () => {
    const initial = streamOptions();
    const { rerender } = renderStream(initial);
    act(() => latestSocket().fail());
    expect(vi.getTimerCount()).toBe(1);

    rerender({ ...initial, enabled: false });

    expect(vi.getTimerCount()).toBe(0);
    advance(60_000);
    goOnline();
    expect(FakeWebSocket.instances).toHaveLength(1);

    const disabled = renderStream(streamOptions({ enabled: false }));
    act(() => disabled.result.current.reconnectNow());
    expect(FakeWebSocket.instances).toHaveLength(1);
  });

  it("reconnects once when the path or the base url changes", () => {
    const initial = streamOptions();
    const { rerender } = renderStream(initial);
    const first = latestSocket();

    rerender({ ...initial, baseUrl: new URL(apiBase.href) });
    expect(FakeWebSocket.instances).toHaveLength(1);

    rerender({ ...initial, path: "events/7" });
    expect(first.closedByClient).toBe(true);
    expect(FakeWebSocket.instances).toHaveLength(2);
    expect(latestSocket().url).toBe(
      "ws://localhost:3000/api/update-stream/subscribe?path=events%2F7",
    );

    rerender({ ...initial, path: "events/7", baseUrl: new URL("https://utf.example.test/api/") });
    expect(FakeWebSocket.instances).toHaveLength(3);
    expect(liveSockets()).toHaveLength(1);
    expect(latestSocket().url).toBe(
      "wss://utf.example.test/api/update-stream/subscribe?path=events%2F7",
    );
  });
});
