/**
 * Deterministic WebSocket double installed globally for every Vitest file by `tests/setup.ts`.
 *
 * Nothing happens on its own: tests drive the socket with {@link FakeWebSocket.open},
 * {@link FakeWebSocket.message}, {@link FakeWebSocket.serverClose} and {@link FakeWebSocket.fail}. Every
 * constructed socket is recorded in {@link FakeWebSocket.instances}, which is cleared after each test. The
 * double supports `addEventListener` only, which is the only subscription style the production client uses.
 */
export class FakeWebSocket extends EventTarget {
  /** Ready state of a socket that has not opened yet. */
  static readonly CONNECTING = 0;
  /** Ready state of an open socket. */
  static readonly OPEN = 1;
  /** Ready state of a socket that is closing. */
  static readonly CLOSING = 2;
  /** Ready state of a closed socket. */
  static readonly CLOSED = 3;

  /** Every socket constructed since the last {@link FakeWebSocket.reset}, in construction order. */
  static instances: FakeWebSocket[] = [];

  /** Forgets every recorded socket; called after each test. */
  static reset(): void {
    FakeWebSocket.instances = [];
  }

  /** Ready state of a socket that has not opened yet. */
  readonly CONNECTING = FakeWebSocket.CONNECTING;
  /** Ready state of an open socket. */
  readonly OPEN = FakeWebSocket.OPEN;
  /** Ready state of a socket that is closing. */
  readonly CLOSING = FakeWebSocket.CLOSING;
  /** Ready state of a closed socket. */
  readonly CLOSED = FakeWebSocket.CLOSED;

  /** The URL the socket was created with. */
  readonly url: string;
  /** The current ready state; starts at {@link FakeWebSocket.CONNECTING}. */
  readyState: number = FakeWebSocket.CONNECTING;
  /** Whether the code under test closed the socket. */
  closedByClient = false;

  /**
   * Records a new socket without connecting anywhere.
   *
   * @param url The URL the code under test connects to.
   */
  constructor(url: string | URL) {
    super();
    this.url = String(url);
    FakeWebSocket.instances.push(this);
  }

  /** Accepts and discards outgoing data. */
  send(): void {}

  /** Marks the socket closed by the client without dispatching a close event. */
  close(): void {
    this.readyState = FakeWebSocket.CLOSED;
    this.closedByClient = true;
  }

  /** Simulates the server accepting the connection. */
  open(): void {
    this.readyState = FakeWebSocket.OPEN;
    this.dispatchEvent(new Event("open"));
  }

  /**
   * Simulates the server sending a text frame.
   *
   * @param data The frame payload.
   */
  message(data: string): void {
    this.dispatchEvent(new MessageEvent("message", { data }));
  }

  /** Simulates the server or the network closing the connection. */
  serverClose(): void {
    this.readyState = FakeWebSocket.CLOSED;
    this.dispatchEvent(new Event("close"));
  }

  /** Simulates a failed connection: an error event followed by a close event. */
  fail(): void {
    this.dispatchEvent(new Event("error"));
    this.readyState = FakeWebSocket.CLOSED;
    this.dispatchEvent(new Event("close"));
  }
}
