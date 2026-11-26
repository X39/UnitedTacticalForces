import type { Page, WebSocketRoute } from "@playwright/test";

/** Wire type of the fleet-changed signal; must equal the type the API serializer writes. */
const fleetChangedType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerFleetChangedMessage";

/** Wire type of the accrued-cost value frame; must equal the type the API serializer writes (Phase 66). */
const accruedCostType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerAccruedCostMessage";

/** Wire type of the frame that pushes the current metric readings of one server (Phase 67). */
const metricsType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.GameServerMetricsMessage";

/** Wire type of the frame that pushes one newly raised cost alert (Phase 66). */
const costAlertRaisedType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.CostAlertRaisedMessage";

/** Wire type of the frame that tells other tabs an alert was dismissed (Phase 66). */
const costAlertDismissedType =
  "X39.UnitedTacticalForces.WebSockets.Contracts.Hosting.CostAlertDismissedMessage";

/** Matches the update stream subscription endpoint the React app opens through the API base URL. */
const subscribePattern = /\/api\/update-stream\/subscribe(?:\?|$)/;

/** Controls of the mocked update stream returned by {@link mockFleetStream}. */
export interface FleetStreamMock {
  /**
   * Sends one text frame to the page over the most recent connection.
   *
   * @param text The raw frame text, usually built with {@link fleetChangedFrame}.
   */
  send(text: string): void;
  /**
   * Closes the most recent connection from the server side and refuses every later connection attempt by closing it
   * before it opens, so the page stays disconnected and keeps retrying.
   *
   * @returns A promise that settles once the close was delivered to the page.
   */
  closeFromServer(): Promise<void>;
  /**
   * Returns the number of connections the page opened, including refused ones.
   *
   * @returns The connection count.
   */
  connectionCount(): number;
  /**
   * Returns the `path` query parameter of every connection in the order the page opened them.
   *
   * @returns The subscribed update stream paths.
   */
  paths(): string[];
}

/**
 * Builds the pinned fleet-changed frame exactly as the API serializes it (Plan 62-01).
 *
 * @param serverId Primary key of the server that changed.
 * @returns The frame text with reference-preserving `$id` markers.
 */
export function fleetChangedFrame(serverId: string): string {
  return JSON.stringify({
    $id: "1",
    type: fleetChangedType,
    value: { $id: "2", serverId },
  });
}

/** The value of one pushed accrued-cost frame; the fields the page validates and writes into its cache. */
export interface AccruedCostFrameValue {
  /** The accrued amount, or null when the control plane could not calculate it. */
  amount: number | null;
  /** The ISO instant the amount was true; an older instant never replaces a newer cached value. */
  asOf: string;
  /** Whether the session cost stopped and the amount can no longer change. Defaults to false. */
  isFinal?: boolean;
  /** The reason code when the amount is unknown. Defaults to null. */
  unavailableReason?: string | null;
  /** The deployment the session belongs to. Defaults to a fixed test id. */
  deploymentId?: string;
  /** The ISO currency code. Defaults to EUR. */
  currency?: string;
  /** The length of the provider billing unit in seconds, for example 3600 for started hours. Defaults to null. */
  billingUnitSeconds?: number | null;
  /** The ISO instant the last started billing unit ends at, which is when the next one starts. Defaults to null. */
  billedThrough?: string | null;
}

/**
 * Builds the accrued-cost value frame exactly as the API serializes it (Phase 66), with reference-preserving `$id`
 * markers.
 *
 * @param serverId Primary key of the server whose session cost is pushed.
 * @param value The amount, its instant and the optional final flag, reason, deployment, currency and billing
 * facts.
 * @returns The frame text.
 */
export function accruedCostFrame(serverId: string, value: AccruedCostFrameValue): string {
  return JSON.stringify({
    $id: "1",
    type: accruedCostType,
    value: {
      $id: "2",
      serverId,
      deploymentId: value.deploymentId ?? "50000000-0000-4000-8000-000000000b66",
      amount: value.amount,
      currency: value.currency ?? "EUR",
      asOf: value.asOf,
      isFinal: value.isFinal ?? false,
      unavailableReason: value.unavailableReason ?? null,
      billingUnitSeconds: value.billingUnitSeconds ?? null,
      billedThrough: value.billedThrough ?? null,
    },
  });
}

/** The value of one pushed metrics frame; the fields the page validates and writes into its cache. */
export interface MetricsFrameValue {
  /** The ISO instant of the newest reading, or null when there is none; an older instant never replaces a newer one. */
  asOf: string | null;
  /** The age in seconds after which the browser treats the reading as too old. Defaults to 45. */
  staleAfterSeconds?: number;
  /** The ISO instant the game process started, or null. Defaults to null. */
  runningSince?: string | null;
  /** The reason code when there is no start instant. Defaults to null. */
  runningSinceUnavailableReason?: string | null;
  /** The CPU load in percent, or null. Defaults to null. */
  cpuPercent?: number | null;
  /** The reason code when there is no CPU reading. Defaults to null. */
  cpuUnavailableReason?: string | null;
  /** The used memory in bytes, or null. Defaults to null. */
  memoryUsedBytes?: number | null;
  /** The machine memory in bytes, or null. Defaults to null. */
  memoryTotalBytes?: number | null;
  /** The reason code when there is no memory reading. Defaults to null. */
  memoryUnavailableReason?: string | null;
  /** The human player count, or null. Defaults to null. */
  playerCount?: number | null;
  /** The player capacity, or null. Defaults to null. */
  playerCapacity?: number | null;
  /** The reason code when there is no player count. Defaults to null. */
  playersUnavailableReason?: string | null;
}

/**
 * Builds the metrics value frame exactly as the API serializes it (Phase 67), with reference-preserving `$id`
 * markers.
 *
 * @param serverId Primary key of the server whose readings are pushed.
 * @param value The readings and their reasons; an omitted field is null.
 * @returns The frame text.
 */
export function metricsFrame(serverId: string, value: MetricsFrameValue): string {
  return JSON.stringify({
    $id: "1",
    type: metricsType,
    value: {
      $id: "2",
      serverId,
      asOf: value.asOf,
      staleAfterSeconds: value.staleAfterSeconds ?? 45,
      runningSince: value.runningSince ?? null,
      runningSinceUnavailableReason: value.runningSinceUnavailableReason ?? null,
      cpuPercent: value.cpuPercent ?? null,
      cpuUnavailableReason: value.cpuUnavailableReason ?? null,
      memoryUsedBytes: value.memoryUsedBytes ?? null,
      memoryTotalBytes: value.memoryTotalBytes ?? null,
      memoryUnavailableReason: value.memoryUnavailableReason ?? null,
      playerCount: value.playerCount ?? null,
      playerCapacity: value.playerCapacity ?? null,
      playersUnavailableReason: value.playersUnavailableReason ?? null,
    },
  });
}

/** One cost alert in the shape of the REST list; the frame carries the same facts under the message field names. */
export interface CostAlertFrameSource {
  /** The alert id; sent as `alertId` in the frame. */
  id: string;
  /** The server the alert belongs to. */
  serverId: string;
  /** The server name at the time of the crossing. */
  serverName: string;
  /** The cost owner the alert is for. */
  costOwnerId: string;
  /** The configured amount that was reached. */
  thresholdAmount: number;
  /** The accrued amount at the crossing. */
  accruedAmount: number;
  /** The ISO currency code. */
  currency: string;
  /** The ISO instant the alert was raised. */
  createdAt: string;
  /** What the alert announces: `CostThreshold` or `AutoStop`. Omitted for a threshold alert, as an older API does. */
  kind?: string;
  /** The ISO instant the announced automatic stop follows; only sent for an `AutoStop` alert. */
  stopsAt?: string | null;
  /** Why the stop was announced: `Idle`, `MaxRuntime` or `Schedule`; only sent for an `AutoStop` alert. */
  stopReason?: string | null;
  /** Minutes for an idle stop and hours for a maximum runtime stop; only sent for an `AutoStop` alert. */
  stopLimit?: number | null;
}

/**
 * Builds the cost-alert-raised frame exactly as the API serializes it (Phase 66, extended in Phase 68 with the
 * optional automatic shutdown members, which are sent only when the alert has a kind).
 *
 * @param alert The alert to push, usually the same object the REST mock lists.
 * @returns The frame text.
 */
export function costAlertRaisedFrame(alert: CostAlertFrameSource): string {
  return JSON.stringify({
    $id: "1",
    type: costAlertRaisedType,
    value: {
      $id: "2",
      alertId: alert.id,
      serverId: alert.serverId,
      serverName: alert.serverName,
      costOwnerId: alert.costOwnerId,
      thresholdAmount: alert.thresholdAmount,
      accruedAmount: alert.accruedAmount,
      currency: alert.currency,
      createdAt: alert.createdAt,
      ...(alert.kind === undefined
        ? {}
        : {
            kind: alert.kind,
            stopsAt: alert.stopsAt ?? null,
            stopReason: alert.stopReason ?? null,
            stopLimit: alert.stopLimit ?? null,
          }),
    },
  });
}

/**
 * Builds the cost-alert-dismissed frame exactly as the API serializes it (Phase 66).
 *
 * @param alertId The id of the alert the viewer dismissed in another tab.
 * @returns The frame text.
 */
export function costAlertDismissedFrame(alertId: string): string {
  return JSON.stringify({
    $id: "1",
    type: costAlertDismissedType,
    value: { $id: "2", alertId },
  });
}

/**
 * Mocks the update stream subscription of the page with `page.routeWebSocket`.
 *
 * The handler never connects to a real server, so Playwright opens the page socket itself and no WebSocket leaves the
 * browser. Every connection is recorded; `send` and `closeFromServer` act on the most recent one. After
 * `closeFromServer`, later connection attempts are closed before they open, which keeps the client in one continuous
 * disconnection episode and lets the delayed notice appear.
 *
 * @param page The page whose WebSocket is mocked; call before the first navigation.
 * @returns The controls of the mocked stream.
 */
export async function mockFleetStream(page: Page): Promise<FleetStreamMock> {
  const connections: WebSocketRoute[] = [];
  const subscribedPaths: string[] = [];
  let refusing = false;

  await page.routeWebSocket(subscribePattern, async (ws) => {
    connections.push(ws);
    subscribedPaths.push(new URL(ws.url()).searchParams.get("path") ?? "");
    if (refusing) await ws.close();
  });

  const latest = (): WebSocketRoute => {
    const connection = connections.at(-1);
    if (!connection) throw new Error("The page has not opened an update stream connection yet.");
    return connection;
  };

  return {
    send(text) {
      latest().send(text);
    },
    async closeFromServer() {
      refusing = true;
      await latest().close();
    },
    connectionCount() {
      return connections.length;
    },
    paths() {
      return [...subscribedPaths];
    },
  };
}
