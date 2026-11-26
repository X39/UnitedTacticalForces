import type {
  GameConfigurationEntry,
  HostingFile,
  LifecycleJob,
  ServerAction,
} from "@/features/hosting/types";
import { fleetAction } from "../helpers/fleetFixtures";
import {
  buildCommandProtocol,
  affectedDestinationDirectories,
  combineServerPath,
  configurationInputValue,
  costStopAction,
  directoryAncestors,
  findHostingFileByPath,
  findUploadCollisions,
  normalizeServerRelativePath,
  orderDirectoriesShallowFirst,
  parentServerPath,
  planUploads,
  parseConfigurationEntryValue,
  primaryServerAction,
  protocolEntries,
  sortHostingFiles,
  validateConfigurationEntry,
} from "@/features/hosting/workspaceModel";

const entry = (overrides: Partial<GameConfigurationEntry> = {}): GameConfigurationEntry => ({
  key: "maxPlayers",
  targetPath: "config/server.cfg",
  group: "General",
  displayName: "Maximum players",
  description: "Capacity",
  kind: "Integer",
  required: true,
  sensitive: false,
  isControlPlaneOnly: false,
  tier: "Advanced",
  choices: [],
  order: 1,
  minimum: 1,
  maximum: 128,
  allowedValues: [],
  value: 64,
  source: "Game",
  isCompatible: true,
  isRedacted: false,
  warning: null,
  ...overrides,
});

describe("server workspace model", () => {
  it("normalizes safe server-relative paths and breadcrumbs", () => {
    expect(combineServerPath("profiles\\main", "/users/test.vars/")).toBe(
      "profiles/main/users/test.vars",
    );
    expect(parentServerPath("profiles/main/users/test.vars")).toBe("profiles/main/users");
    expect(parentServerPath("server.cfg")).toBe("");
    expect(normalizeServerRelativePath("/ profiles\\main//./server.cfg ")).toBe(
      "profiles/main/server.cfg",
    );
    expect(() => normalizeServerRelativePath("profiles/../server.cfg")).toThrow("parent-directory");
  });

  it("derives destination directories in shallow creation order", () => {
    expect(directoryAncestors("profiles/main/users")).toEqual([
      "profiles",
      "profiles/main",
      "profiles/main/users",
    ]);
    expect(
      affectedDestinationDirectories([
        "profiles/main/users/a.cfg",
        "addons/mod/file.pbo",
        "root.txt",
      ]),
    ).toEqual(["addons", "profiles", "addons/mod", "profiles/main", "profiles/main/users"]);
    expect(orderDirectoriesShallowFirst(["a/b", "a", "B/c", "a/b"])).toEqual(["a", "a/b", "B/c"]);
  });

  it("plans uploads deterministically and identifies case-insensitive collisions", () => {
    const uploads = planUploads("profiles/main", [
      { item: "z", relativePath: "mods/z.pbo" },
      { item: "a", relativePath: "server.cfg" },
    ]);
    expect(uploads.map((upload) => upload.targetPath)).toEqual([
      "profiles/main/mods/z.pbo",
      "profiles/main/server.cfg",
    ]);
    const files = [
      {
        name: "Server.cfg",
        path: "profiles/main/Server.cfg",
        isDirectory: false,
        isGeneratedTarget: true,
        overridesGenerated: false,
      },
      { name: "mods", path: "profiles/main/mods", isDirectory: true },
    ] as HostingFile[];
    expect(findHostingFileByPath(files, "PROFILES/main/server.cfg")?.name).toBe("Server.cfg");
    expect(findUploadCollisions(uploads, files)).toMatchObject([
      {
        upload: { item: "a", targetPath: "profiles/main/server.cfg" },
        existing: { name: "Server.cfg" },
        requiresGeneratedOverride: true,
      },
    ]);
  });

  it("parses typed sparse configuration values and rejects constraints", () => {
    expect(configurationInputValue(entry())).toBe("64");
    expect(parseConfigurationEntryValue(entry(), "42")).toBe(42);
    expect(parseConfigurationEntryValue(entry({ kind: "Boolean" }), "true")).toBe(true);
    expect(parseConfigurationEntryValue(entry({ kind: "StringArray" }), "one\n two \n")).toEqual([
      "one",
      "two",
    ]);
    expect(validateConfigurationEntry(entry(), "129")).toContain("at most 128");
    expect(validateConfigurationEntry(entry(), "4.2")).toContain("whole number");
  });

  it("never reveals a redacted configuration value", () => {
    expect(
      configurationInputValue(
        entry({ sensitive: true, isRedacted: true, value: "server-secret", kind: "String" }),
      ),
    ).toBe("");
  });

  it("sorts directories before files and respects the chosen file column", () => {
    const files = [
      { name: "z.log", path: "z.log", isDirectory: false, size: 5 },
      { name: "profiles", path: "profiles", isDirectory: true, size: null },
      { name: "a.log", path: "a.log", isDirectory: false, size: 10 },
    ] as HostingFile[];
    expect(sortHostingFiles(files, "size", true).map((file) => file.name)).toEqual([
      "profiles",
      "a.log",
      "z.log",
    ]);
  });

  describe("primary action", () => {
    const action = (overrides: Partial<ServerAction>): ServerAction =>
      ({
        enabled: true,
        pending: false,
        dangerous: false,
        costImpact: "NoCostImpact",
        order: 5,
        ...overrides,
      }) as ServerAction;
    const start = action({ id: "start", order: 1, costImpact: "StartsCost" });
    const stop = action({ id: "stop", order: 2, costImpact: "StopsCost" });
    const terminate = action({
      id: "terminate",
      order: 9,
      dangerous: true,
      costImpact: "StopsCost",
    });
    const collectLogs = action({ id: "collect-logs", order: 0 });

    it("prefers the graceful cost-stop over the destructive one when provider cost remains", () => {
      expect(primaryServerAction([start, terminate, stop], true)?.id).toBe("stop");
      expect(primaryServerAction([terminate, stop], true)?.id).toBe("stop");
    });

    it("falls back to the destructive cost-stop when no graceful stop is a candidate", () => {
      const disabledStop = { ...stop, enabled: false } as ServerAction;
      expect(primaryServerAction([start, disabledStop, terminate], true)?.id).toBe("terminate");
    });

    it("prefers start, then stop, then the lowest order and ignores collect-logs", () => {
      expect(primaryServerAction([terminate, stop, start], false)?.id).toBe("start");
      expect(primaryServerAction([terminate, stop], false)?.id).toBe("stop");
      expect(primaryServerAction([collectLogs, terminate], false)?.id).toBe("terminate");
      expect(primaryServerAction([collectLogs], false)).toBeUndefined();
    });

    it("treats unrecognised cost impact strings as not stopping cost, with no substring matching", () => {
      const lowercase = { ...stop, costImpact: "stopscost" } as unknown as ServerAction;
      const unknown = {
        ...stop,
        id: "other",
        costImpact: "SomethingStopsCostish",
      } as unknown as ServerAction;
      expect(primaryServerAction([terminate, lowercase], true)?.id).toBe("terminate");
      expect(primaryServerAction([terminate, unknown], true)?.id).toBe("terminate");
    });
  });

  describe("cost-stop action", () => {
    const stop = fleetAction("stop");
    const terminate = fleetAction("terminate", { dangerous: true });
    const start = fleetAction("start");
    const collectLogs = fleetAction("collect-logs", { costImpact: "StopsCost" });
    const disabled = (source: ServerAction): ServerAction => ({ ...source, enabled: false });

    it("prefers an enabled graceful stop over an enabled terminate", () => {
      expect(costStopAction([terminate, stop])?.id).toBe("stop");
      expect(costStopAction([start, terminate, stop])?.id).toBe("stop");
    });

    it("returns the enabled terminate when the graceful stop is disabled", () => {
      expect(costStopAction([disabled(stop), terminate])?.id).toBe("terminate");
    });

    it("treats a pending action as usable", () => {
      const pendingStop = { ...disabled(stop), pending: true } as ServerAction;
      expect(costStopAction([terminate, pendingStop])?.id).toBe("stop");
    });

    it("returns a disabled graceful stop when no cost-stop is usable", () => {
      expect(costStopAction([disabled(terminate), disabled(stop)])?.id).toBe("stop");
    });

    it("returns a disabled terminate when it is the only cost-stop", () => {
      expect(costStopAction([start, disabled(terminate)])?.id).toBe("terminate");
    });

    it("never returns collect-logs or a start action and returns undefined without a cost-stop", () => {
      expect(costStopAction([collectLogs, start])).toBeUndefined();
      expect(costStopAction([])).toBeUndefined();
    });

    it("keeps primaryServerAction on the same choice while cost may remain", () => {
      expect(primaryServerAction([start, terminate, stop], true)?.id).toBe("stop");
      expect(primaryServerAction([start, disabled(stop), terminate], true)?.id).toBe("terminate");
      expect(primaryServerAction([start, disabled(stop), disabled(terminate)], true)?.id).toBe(
        "start",
      );
    });
  });

  it("orders command protocols by their latest timestamp and includes diagnostic metadata", () => {
    const job = {
      primaryKey: "job-1",
      commands: [
        {
          primaryKey: "command-1",
          sequence: 1,
          actionId: "prepare",
          stage: "install",
          status: "Failed",
          failureCode: "steam-timeout",
          failureReason: "Timed out",
          diagnosticOutput: "retry later",
          timeStampCreated: "2026-08-13T10:00:00Z",
          timeStampStarted: "2026-08-13T10:01:00Z",
          timeStampCompleted: "2026-08-13T10:02:00Z",
        },
      ],
    } as LifecycleJob;
    const protocol = buildCommandProtocol(protocolEntries([job])[0]!);
    expect(protocol).toContain("steam-timeout");
    expect(protocol).toContain("retry later");
  });
});
