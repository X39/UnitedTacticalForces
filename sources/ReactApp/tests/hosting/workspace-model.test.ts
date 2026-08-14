import type {
  GameConfigurationEntry,
  HostingFile,
  LifecycleJob,
  ServerAction,
} from "@/features/hosting/types";
import {
  buildCommandProtocol,
  combineServerPath,
  configurationInputValue,
  parentServerPath,
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

  it("prioritizes a dangerous cost-stop action when provider cost remains", () => {
    const actions = [
      {
        id: "start",
        enabled: true,
        pending: false,
        order: 1,
        dangerous: false,
        costImpact: "StartsCost",
      },
      {
        id: "terminate",
        enabled: true,
        pending: false,
        order: 9,
        dangerous: true,
        costImpact: "StopsCost",
      },
    ] as ServerAction[];
    expect(primaryServerAction(actions, true)?.id).toBe("terminate");
    expect(primaryServerAction(actions, false)?.id).toBe("start");
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
