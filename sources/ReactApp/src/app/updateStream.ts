import { useCallback, useEffect, useRef, useState } from "react";

/**
 * Connection state of an update stream subscription.
 *
 * - `connecting`: the first attempt of the subscription is in flight.
 * - `open`: the socket is open and frames are delivered.
 * - `reconnecting`: the socket closed unexpectedly and the client is trying again.
 * - `unavailable`: repeated attempts failed without the stream ever opening.
 */
export type UpdateStreamStatus = "connecting" | "open" | "reconnecting" | "unavailable";

/** One parsed update stream frame: the full type name of the wire message and its untrusted payload. */
export interface UpdateStreamFrame {
  /** Full type name of the wire message, for example the .NET contract class name. */
  type: string;
  /** The message payload; untrusted input that consumers must check before use. */
  value: unknown;
}

/** Options of {@link useUpdateStream}. */
export interface UpdateStreamOptions {
  /** Base URL of the API; the subscription endpoint is resolved against it. */
  baseUrl: URL;
  /** Update stream path to subscribe to, for example `game-servers/fleet`. */
  path: string;
  /** Whether a socket may be opened at all; false keeps the hook idle. */
  enabled: boolean;
  /** Called for every well-formed frame; unknown or malformed frames never reach it. */
  onMessage(frame: UpdateStreamFrame): void;
  /** Called every time the socket opens, including every reconnect. */
  onOpen?(): void;
}

/** The value returned by {@link useUpdateStream}. */
export interface UpdateStreamResult {
  /** The current connection state. */
  status: UpdateStreamStatus;
  /** Incremented once whenever a disconnection episode begins. */
  disconnectEpoch: number;
  /** Starts a fresh connection attempt now when the stream is disconnected; a no-op while connecting or open. */
  reconnectNow(): void;
}

/** A leading-edge coalescer created by {@link createCoalescer}. */
export interface Coalescer {
  /** Runs immediately when the window is idle, otherwise schedules at most one trailing run at the window end. */
  trigger(): void;
  /** Drops a pending trailing run and clears the window timer. The coalescer stays usable afterwards. */
  cancel(): void;
}

/** Base delay of the first retry in milliseconds. */
const initialRetryDelayMs = 1000;

/** Upper bound of the retry delay in milliseconds before jitter. */
const maximumRetryDelayMs = 30_000;

/** Consecutive failed attempts without any open after which the stream reports `unavailable`. */
const unavailableAfterFailures = 5;

/**
 * Builds the WebSocket URL of an update stream subscription.
 *
 * @param apiBase Base URL of the API; a missing trailing slash is added so its last path segment is kept.
 * @param path Update stream path to subscribe to.
 * @returns The `ws:` or `wss:` URL of the subscription endpoint with the path as query parameter.
 */
export function toSocketUrl(apiBase: URL, path: string): string {
  const base = new URL(apiBase.href);
  if (!base.pathname.endsWith("/")) base.pathname += "/";
  const url = new URL("update-stream/subscribe", base);
  url.protocol = url.protocol === "https:" ? "wss:" : "ws:";
  url.searchParams.set("path", path);
  return url.toString();
}

/**
 * Parses an untrusted update stream frame.
 *
 * @param data The raw `MessageEvent.data` value.
 * @returns The frame when `data` is a JSON object string with a string `type`, otherwise null. Never throws.
 */
export function parseUpdateFrame(data: unknown): UpdateStreamFrame | null {
  if (typeof data !== "string") return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(data);
  } catch {
    return null;
  }
  if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) return null;
  const candidate = parsed as { type?: unknown; value?: unknown };
  if (typeof candidate.type !== "string") return null;
  return { type: candidate.type, value: candidate.value };
}

/**
 * Returns the delay before a reconnect attempt: 1, 2, 4, 8 and 16 seconds, then 30 seconds, each with plus or minus
 * 20 percent jitter.
 *
 * @param attempt Zero-based index of the retry; 0 is the retry after the first failure.
 * @param random Source of uniform values in [0, 1]; injectable for deterministic tests.
 * @returns The delay in milliseconds.
 */
export function nextDelayMs(attempt: number, random: () => number = Math.random): number {
  const base = Math.min(initialRetryDelayMs * 2 ** Math.max(0, attempt), maximumRetryDelayMs);
  return base * (0.8 + 0.4 * random());
}

/**
 * Creates a leading-edge coalescer: the first trigger runs `run` immediately and opens a window; further triggers
 * inside the window collapse into one trailing run at the window end, which opens the next window. A trigger after
 * an idle window runs immediately again, so `run` executes at most once per window after the leading run.
 *
 * @param run The work to coalesce.
 * @param windowMs Length of the coalescing window in milliseconds.
 * @returns The coalescer; call `cancel` when its owner unmounts.
 */
export function createCoalescer(run: () => void, windowMs: number): Coalescer {
  let timer: ReturnType<typeof setTimeout> | undefined;
  let pending = false;

  const openWindow = () => {
    timer = setTimeout(() => {
      timer = undefined;
      if (!pending) return;
      pending = false;
      run();
      openWindow();
    }, windowMs);
  };

  return {
    trigger() {
      if (timer !== undefined) {
        pending = true;
        return;
      }
      run();
      openWindow();
    },
    cancel() {
      pending = false;
      if (timer === undefined) return;
      clearTimeout(timer);
      timer = undefined;
    },
  };
}

/** State that outlives a single effect run and belongs to one hook instance, that is one page session. */
interface StreamSession {
  /** Whether any socket of this hook instance has ever opened. */
  everOpened: boolean;
}

/** What a {@link StreamConnection} reports when its socket failed. */
interface StreamDisconnect {
  /** True for the first failure after the stream was connecting or open. */
  beginsEpisode: boolean;
  /** True once the failure threshold is reached without the stream ever having opened. */
  unavailable: boolean;
}

/** Callbacks through which a {@link StreamConnection} reports to the hook that created it. */
interface StreamConnectionEvents {
  /** The socket opened. */
  onOpen(): void;
  /** A well-formed frame arrived. */
  onFrame(frame: UpdateStreamFrame): void;
  /**
   * The socket closed or errored without being disposed; one retry is already scheduled.
   *
   * @param disconnect The episode and availability flags of this failure.
   */
  onDisconnect(disconnect: StreamDisconnect): void;
}

/** The per-effect-run connection controller that owns the socket and the retry timer of one subscription. */
interface StreamConnection {
  /** Opens a socket unless the controller is disposed or a socket already exists. */
  connect(): void;
  /** Attempts a connection now and drops the pending retry, but only while disconnected and between attempts. */
  retryNow(): void;
  /** Ends the subscription: clears the retry timer, closes the socket and silences every later event. */
  dispose(): void;
}

/**
 * Creates the connection controller of one effect run. It owns at most one socket and at most one pending retry
 * timeout; it never uses an interval timer.
 *
 * @param url The WebSocket URL to connect to.
 * @param session Hook-instance state that records whether the stream has ever opened.
 * @param events Callbacks invoked while the controller is not disposed.
 * @returns The controller; call `connect` to start and `dispose` from the effect cleanup.
 */
function createStreamConnection(
  url: string,
  session: StreamSession,
  events: StreamConnectionEvents,
): StreamConnection {
  let socket: WebSocket | null = null;
  let retryTimer: number | undefined;
  let failureCount = 0;
  let disposed = false;
  let disconnected = false;

  const clearRetryTimer = () => {
    if (retryTimer === undefined) return;
    window.clearTimeout(retryTimer);
    retryTimer = undefined;
  };

  const handleOpen = () => {
    if (disposed) return;
    clearRetryTimer();
    failureCount = 0;
    disconnected = false;
    session.everOpened = true;
    events.onOpen();
  };
  const handleMessage = (event: MessageEvent) => {
    if (disposed) return;
    const frame = parseUpdateFrame(event.data);
    if (frame) events.onFrame(frame);
  };
  const handleClose = () => {
    if (disposed) return;
    detach();
    handleFailure();
  };
  const handleError = () => {
    if (disposed) return;
    detach()?.close();
    handleFailure();
  };

  function handleFailure() {
    const beginsEpisode = !disconnected;
    disconnected = true;
    failureCount += 1;
    events.onDisconnect({
      beginsEpisode,
      unavailable: !session.everOpened && failureCount >= unavailableAfterFailures,
    });
    clearRetryTimer();
    retryTimer = window.setTimeout(
      () => {
        retryTimer = undefined;
        open();
      },
      nextDelayMs(failureCount - 1),
    );
  }

  function detach(): WebSocket | null {
    const current = socket;
    if (!current) return null;
    current.removeEventListener("open", handleOpen);
    current.removeEventListener("message", handleMessage);
    current.removeEventListener("close", handleClose);
    current.removeEventListener("error", handleError);
    socket = null;
    return current;
  }

  function open() {
    if (disposed || socket) return;
    const next = new WebSocket(url);
    next.addEventListener("open", handleOpen);
    next.addEventListener("message", handleMessage);
    next.addEventListener("close", handleClose);
    next.addEventListener("error", handleError);
    socket = next;
  }

  return {
    connect: open,
    retryNow() {
      if (disposed || !disconnected || socket) return;
      clearRetryTimer();
      open();
    },
    dispose() {
      disposed = true;
      clearRetryTimer();
      detach()?.close();
    },
  };
}

/**
 * Subscribes to one update stream path over a WebSocket and keeps the subscription alive.
 *
 * The hook is resource-agnostic: it delivers parsed frames and reports the connection state, and the caller
 * decides what a frame means. After an unexpected close or error it retries with {@link nextDelayMs} (1 to 16
 * seconds, then a 30 second cap, with jitter) indefinitely while enabled, and immediately on the window `online`
 * event, when the document becomes visible, or through `reconnectNow`. There is no polling fallback: callers that
 * need fresh data refetch from `onOpen`, which runs on every open including reconnects.
 *
 * Every effect run owns its own controller with a disposed flag, so StrictMode's mount, unmount and mount leaves
 * exactly one live socket, and nothing reports state or reconnects after cleanup.
 *
 * @param options Base URL, path, enabled flag and frame callbacks.
 * @returns The connection status, the disconnect epoch and a function that reconnects immediately.
 */
export function useUpdateStream(options: UpdateStreamOptions): UpdateStreamResult {
  const { baseUrl, path, enabled, onMessage, onOpen } = options;
  const [status, setStatus] = useState<UpdateStreamStatus>("connecting");
  const [disconnectEpoch, setDisconnectEpoch] = useState(0);
  const onMessageRef = useRef(onMessage);
  const onOpenRef = useRef(onOpen);
  const sessionRef = useRef<StreamSession>({ everOpened: false });
  const connectionRef = useRef<StreamConnection | null>(null);
  const baseHref = baseUrl.href;

  useEffect(() => {
    onMessageRef.current = onMessage;
    onOpenRef.current = onOpen;
  }, [onMessage, onOpen]);

  useEffect(() => {
    if (!enabled) return;
    const connection = createStreamConnection(
      toSocketUrl(new URL(baseHref), path),
      sessionRef.current,
      {
        onOpen() {
          setStatus("open");
          onOpenRef.current?.();
        },
        onFrame(frame) {
          onMessageRef.current(frame);
        },
        onDisconnect({ beginsEpisode, unavailable }) {
          if (beginsEpisode) {
            setStatus("reconnecting");
            setDisconnectEpoch((current) => current + 1);
          }
          if (unavailable) setStatus("unavailable");
        },
      },
    );
    const retryWhenOnline = () => connection.retryNow();
    const retryWhenVisible = () => {
      if (document.visibilityState === "visible") connection.retryNow();
    };
    connectionRef.current = connection;
    window.addEventListener("online", retryWhenOnline);
    document.addEventListener("visibilitychange", retryWhenVisible);
    connection.connect();
    return () => {
      connection.dispose();
      window.removeEventListener("online", retryWhenOnline);
      document.removeEventListener("visibilitychange", retryWhenVisible);
      if (connectionRef.current === connection) connectionRef.current = null;
    };
  }, [enabled, baseHref, path]);

  const reconnectNow = useCallback(() => {
    connectionRef.current?.retryNow();
  }, []);

  return { status, disconnectEpoch, reconnectNow };
}
