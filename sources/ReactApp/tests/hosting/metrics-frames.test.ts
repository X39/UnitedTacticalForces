import {
  historyBucketMs,
  metricsMessageType,
  parseMetricsFrame,
  replacesCachedReading,
} from "@/features/hosting/metricsFrames";
import type { GameServerMetrics } from "@/features/hosting/types";

const serverId = "3f2c9a52-6d1e-4b7a-9f10-5a8c2d4e6b01";

/** A valid metrics payload as the .NET serializer sends it (camelCase, instants as ISO strings). */
function metricsValue(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    $id: "2",
    serverId,
    asOf: "2026-11-22T10:00:00+00:00",
    staleAfterSeconds: 45,
    runningSince: "2026-11-22T09:00:00+00:00",
    runningSinceUnavailableReason: null,
    cpuPercent: 12.5,
    cpuUnavailableReason: null,
    memoryUsedBytes: 2_147_483_648,
    memoryTotalBytes: 8_589_934_592,
    memoryUnavailableReason: null,
    playerCount: 0,
    playerCapacity: 32,
    playersUnavailableReason: null,
    ...overrides,
  };
}

function parse(overrides: Record<string, unknown> = {}) {
  return parseMetricsFrame({ type: metricsMessageType, value: metricsValue(overrides) });
}

function reading(overrides: Partial<GameServerMetrics> = {}): GameServerMetrics {
  return {
    gameServerId: serverId,
    asOf: "2026-11-22T10:00:00+00:00",
    staleAfterSeconds: 45,
    runningSince: "2026-11-22T09:00:00+00:00",
    runningSinceUnavailableReason: null,
    cpuPercent: 12.5,
    cpuUnavailableReason: null,
    memoryUsedBytes: 1,
    memoryTotalBytes: 2,
    memoryUnavailableReason: null,
    playerCount: 0,
    playerCapacity: 32,
    playersUnavailableReason: null,
    ...overrides,
  };
}

describe("parseMetricsFrame", () => {
  it("maps a valid frame to the REST shape, keeps a zero player count and ignores extra properties", () => {
    expect(parse()).toEqual({
      serverId,
      metrics: {
        gameServerId: serverId,
        asOf: "2026-11-22T10:00:00+00:00",
        staleAfterSeconds: 45,
        runningSince: "2026-11-22T09:00:00+00:00",
        runningSinceUnavailableReason: null,
        cpuPercent: 12.5,
        cpuUnavailableReason: null,
        memoryUsedBytes: 2_147_483_648,
        memoryTotalBytes: 8_589_934_592,
        memoryUnavailableReason: null,
        playerCount: 0,
        playerCapacity: 32,
        playersUnavailableReason: null,
      },
    });
  });

  it("accepts reasons without values, a null as-of time and absent optional properties", () => {
    const parsed = parse({
      asOf: null,
      runningSince: undefined,
      runningSinceUnavailableReason: "worker-offline",
      cpuPercent: undefined,
      cpuUnavailableReason: "no-sample",
      memoryUsedBytes: null,
      memoryTotalBytes: null,
      memoryUnavailableReason: "no-sample",
      playerCount: null,
      playerCapacity: null,
      playersUnavailableReason: "not-reported",
    });
    expect(parsed?.metrics).toMatchObject({
      asOf: null,
      runningSince: null,
      runningSinceUnavailableReason: "worker-offline",
      cpuPercent: null,
      cpuUnavailableReason: "no-sample",
      memoryUsedBytes: null,
      playerCount: null,
      playersUnavailableReason: "not-reported",
    });
  });

  it("returns null for another type, a non-object value and a missing or empty server id", () => {
    expect(parseMetricsFrame({ type: "Other", value: metricsValue() })).toBeNull();
    expect(parseMetricsFrame({ type: metricsMessageType, value: null })).toBeNull();
    expect(parseMetricsFrame({ type: metricsMessageType, value: "text" })).toBeNull();
    expect(parse({ serverId: undefined })).toBeNull();
    expect(parse({ serverId: "" })).toBeNull();
    expect(parse({ serverId: 7 })).toBeNull();
  });

  it("returns null for a numeric field that is a string or not finite", () => {
    expect(parse({ cpuPercent: "12.5" })).toBeNull();
    expect(parse({ cpuPercent: Number.POSITIVE_INFINITY })).toBeNull();
    expect(parse({ cpuPercent: Number.NaN })).toBeNull();
    expect(parse({ memoryUsedBytes: "1" })).toBeNull();
    expect(parse({ memoryTotalBytes: Number.POSITIVE_INFINITY })).toBeNull();
    expect(parse({ playerCount: "0" })).toBeNull();
    expect(parse({ playerCapacity: Number.NaN })).toBeNull();
  });

  it("returns null for an instant that does not parse or is not a string", () => {
    expect(parse({ asOf: "yesterday" })).toBeNull();
    expect(parse({ asOf: 1_700_000_000 })).toBeNull();
    expect(parse({ runningSince: "not a date" })).toBeNull();
  });

  it("returns null for a stale limit that is below 1, fractional, missing or not a number", () => {
    expect(parse({ staleAfterSeconds: 0 })).toBeNull();
    expect(parse({ staleAfterSeconds: -5 })).toBeNull();
    expect(parse({ staleAfterSeconds: 4.5 })).toBeNull();
    expect(parse({ staleAfterSeconds: "45" })).toBeNull();
    expect(parse({ staleAfterSeconds: undefined })).toBeNull();
  });

  it("returns null for a reason that is not a string", () => {
    expect(parse({ cpuPercent: null, cpuUnavailableReason: 3 })).toBeNull();
  });

  it("returns null when a reading carries a value together with a reason", () => {
    expect(parse({ runningSinceUnavailableReason: "no-sample" })).toBeNull();
    expect(parse({ cpuUnavailableReason: "no-sample" })).toBeNull();
    expect(parse({ memoryUnavailableReason: "no-sample" })).toBeNull();
    expect(parse({ playersUnavailableReason: "not-reported" })).toBeNull();
  });
});

describe("replacesCachedReading", () => {
  /** A reading as the control plane sends it for a stopped server: no as-of time, only reasons. */
  function stopped(): GameServerMetrics {
    return reading({
      asOf: null,
      runningSince: null,
      runningSinceUnavailableReason: "server-not-running",
      cpuPercent: null,
      cpuUnavailableReason: "server-not-running",
      memoryUsedBytes: null,
      memoryTotalBytes: null,
      memoryUnavailableReason: "server-not-running",
      playerCount: null,
      playerCapacity: null,
      playersUnavailableReason: "server-not-running",
    });
  }

  it("takes the first reading", () => {
    expect(replacesCachedReading(reading(), undefined)).toBe(true);
  });

  it("takes a later as-of time", () => {
    const current = reading({ asOf: "2026-11-22T10:00:00+00:00" });
    expect(replacesCachedReading(reading({ asOf: "2026-11-22T10:00:15+00:00" }), current)).toBe(
      true,
    );
  });

  it("refuses an earlier as-of time that holds any value, also running since alone", () => {
    const current = reading({ asOf: "2026-11-22T10:00:00+00:00" });
    const earlier = "2026-11-22T09:59:45+00:00";
    expect(replacesCachedReading(reading({ asOf: earlier }), current)).toBe(false);
    expect(
      replacesCachedReading(
        reading({
          asOf: earlier,
          cpuPercent: null,
          cpuUnavailableReason: "measuring",
          memoryUsedBytes: null,
          memoryTotalBytes: null,
          memoryUnavailableReason: "measuring",
          playerCount: null,
          playerCapacity: null,
          playersUnavailableReason: "measuring",
        }),
        current,
      ),
    ).toBe(false);
  });

  it("takes an earlier as-of time that carries reason codes only", () => {
    const current = reading({ asOf: "2026-11-22T10:00:00+00:00" });
    expect(
      replacesCachedReading({ ...stopped(), asOf: "2026-11-22T09:59:45+00:00" }, current),
    ).toBe(true);
  });

  it("takes a reading without an as-of time over one with an as-of time", () => {
    expect(replacesCachedReading(stopped(), reading())).toBe(true);
    expect(
      replacesCachedReading(
        reading({
          asOf: null,
          runningSince: "2026-11-22T11:00:00+00:00",
          cpuPercent: null,
          cpuUnavailableReason: "measuring",
          memoryUsedBytes: null,
          memoryTotalBytes: null,
          memoryUnavailableReason: "measuring",
          playerCount: null,
          playerCapacity: null,
          playersUnavailableReason: "measuring",
        }),
        reading(),
      ),
    ).toBe(true);
  });

  it("takes a reading with an as-of time over one without", () => {
    expect(replacesCachedReading(reading(), stopped())).toBe(true);
  });

  it("keeps the cached reading when the facts are equal", () => {
    expect(replacesCachedReading(reading(), reading())).toBe(false);
    expect(replacesCachedReading(stopped(), stopped())).toBe(false);
    const stamped = { ...reading(), receivedAtMs: 1000 };
    expect(replacesCachedReading(reading(), stamped)).toBe(false);
  });

  it("takes an equal as-of time when the facts differ", () => {
    expect(replacesCachedReading(reading({ cpuPercent: 99 }), reading())).toBe(true);
  });

  it("counts an as-of value that does not parse as missing", () => {
    const current = reading({ asOf: "2026-11-22T10:00:00+00:00" });
    expect(replacesCachedReading(reading({ asOf: "not-a-date" }), current)).toBe(true);
    expect(replacesCachedReading(stopped(), reading({ asOf: "not-a-date" }))).toBe(true);
  });
});

describe("historyBucketMs", () => {
  it("returns the bucket length of each range", () => {
    expect(historyBucketMs("1h")).toBe(15_000);
    expect(historyBucketMs("24h")).toBe(60_000);
    expect(historyBucketMs("7d")).toBe(300_000);
  });
});
