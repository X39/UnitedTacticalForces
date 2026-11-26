import { ApiError } from "@/api/client";
import {
  buildApplyPlan,
  configurationEntryIssue,
  countChanges,
  credentialValueIssue,
  describeReview,
  emptyServerChangeDraft,
  failureReason,
  normalizeDraft,
  removeApplied,
  removeReviewItem,
  runApplyPlan,
  stageModPack,
  stageProfile,
  stageReset,
  stageSecret,
  stageValue,
  summarizeResults,
  unstageSetting,
  validateStagedSecrets,
  validateStagedSettings,
  type ApplyExecutors,
  type ReviewContext,
  type SavedServerChanges,
  type ServerChangeDraft,
} from "@/features/hosting/serverChangeDraft";
import type { GameConfigurationEntry } from "@/features/hosting/types";
import { configurationEntry } from "../helpers/configurationFixtures";

const savedProfile = { profileId: "profile-a", revisionId: null };

function saved(
  entries: readonly GameConfigurationEntry[] | undefined,
  overrides: Partial<SavedServerChanges> = {},
): SavedServerChanges {
  return { modPackId: 7, profile: savedProfile, entries, ...overrides };
}

function apiError(code: ApiError["code"]): ApiError {
  return new ApiError("text that must never be read", 500, code);
}

describe("server change draft staging", () => {
  it("keeps values and resets disjoint", () => {
    let draft = stageValue(emptyServerChangeDraft, "a", "80");
    draft = stageReset(draft, "a");
    expect(draft.values.has("a")).toBe(false);
    expect([...draft.resets]).toEqual(["a"]);
    draft = stageValue(draft, "a", "90");
    expect(draft.values.get("a")).toBe("90");
    expect(draft.resets.size).toBe(0);
    expect(unstageSetting(draft, "a").values.size).toBe(0);
  });

  it("never mutates the input draft", () => {
    const before = stageValue(emptyServerChangeDraft, "a", "1");
    stageValue(before, "b", "2");
    stageReset(before, "a");
    stageSecret(before, "k", { kind: "rotate" });
    expect([...before.values.keys()]).toEqual(["a"]);
    expect(before.resets.size).toBe(0);
    expect(before.secrets.size).toBe(0);
    expect(emptyServerChangeDraft.values.size).toBe(0);
  });

  it("removes a credential set to an empty value and keeps a rotate", () => {
    let draft = stageSecret(emptyServerChangeDraft, "k", {
      kind: "set",
      value: "x",
      externalProvider: false,
    });
    expect(draft.secrets.size).toBe(1);
    draft = stageSecret(draft, "k", { kind: "set", value: "", externalProvider: false });
    expect(draft.secrets.size).toBe(0);
    draft = stageSecret(draft, "g", { kind: "rotate" });
    expect(draft.secrets.get("g")).toEqual({ kind: "rotate" });
  });
});

describe("normalizeDraft", () => {
  const entries = [
    configurationEntry({ key: "name", value: "Alpha" }),
    configurationEntry({ key: "password", sensitive: true }),
    configurationEntry({ key: "hidden", isRedacted: true }),
    configurationEntry({ key: "port", kind: "Integer", value: 2302, source: "Server" }),
    configurationEntry({ key: "inherited", value: "x", source: "Default" }),
  ];

  it("drops values equal to the saved input", () => {
    const draft = stageValue(stageValue(emptyServerChangeDraft, "name", "Alpha"), "port", "2310");
    const normalized = normalizeDraft(draft, saved(entries));
    expect([...normalized.values]).toEqual([["port", "2310"]]);
  });

  it("drops values for sensitive, redacted and unknown entries", () => {
    let draft = stageValue(emptyServerChangeDraft, "password", "secret");
    draft = stageValue(draft, "hidden", "secret");
    draft = stageValue(draft, "missing", "secret");
    draft = stageReset(draft, "missing2");
    expect(normalizeDraft(draft, saved(entries)).values.size).toBe(0);
    expect(normalizeDraft(draft, saved(entries)).resets.size).toBe(0);
  });

  it("keeps resets only for entries whose source is the server", () => {
    const draft = stageReset(stageReset(emptyServerChangeDraft, "port"), "inherited");
    expect([...normalizeDraft(draft, saved(entries)).resets]).toEqual(["port"]);
  });

  it("keeps values and resets while the entries are not loaded", () => {
    const draft = stageReset(stageValue(emptyServerChangeDraft, "a", "1"), "b");
    const normalized = normalizeDraft(draft, saved(undefined));
    expect(normalized.values.size).toBe(1);
    expect(normalized.resets.size).toBe(1);
  });

  it("drops a mod pack equal to the saved id and a profile equal to both saved ids", () => {
    let draft: ServerChangeDraft = stageModPack(emptyServerChangeDraft, 7);
    draft = stageProfile(draft, { profileId: "profile-a", revisionId: null });
    const same = normalizeDraft(draft, saved(entries));
    expect(same.modPack).toBeNull();
    expect(same.profile).toBeNull();
    draft = stageProfile(stageModPack(draft, null), { profileId: "profile-a", revisionId: "r1" });
    const different = normalizeDraft(draft, saved(entries));
    expect(different.modPack).toEqual({ to: null });
    expect(different.profile).toEqual({ profileId: "profile-a", revisionId: "r1" });
  });

  it("does not copy saved data into the draft", () => {
    const normalized = normalizeDraft(emptyServerChangeDraft, saved(entries));
    expect(countChanges(normalized)).toBe(0);
  });
});

describe("countChanges", () => {
  it("counts mod pack, profile, values, resets and credentials", () => {
    let draft = stageModPack(emptyServerChangeDraft, 3);
    draft = stageProfile(draft, { profileId: null, revisionId: null });
    draft = stageValue(stageValue(draft, "a", "1"), "b", "2");
    draft = stageReset(draft, "c");
    draft = stageSecret(draft, "k", { kind: "rotate" });
    expect(countChanges(draft)).toBe(6);
    expect(countChanges(emptyServerChangeDraft)).toBe(0);
  });
});

describe("buildApplyPlan", () => {
  const entries = [
    configurationEntry({ key: "port", kind: "Integer", value: 1 }),
    configurationEntry({ key: "flag", kind: "Boolean" }),
    configurationEntry({ key: "lines", kind: "StringArray" }),
  ];

  it("orders steps and parses staged values", () => {
    let draft = stageValue(emptyServerChangeDraft, "port", "80");
    draft = stageValue(draft, "lines", "a\n\n b \r\nc");
    draft = stageValue(draft, "flag", "true");
    draft = stageReset(stageReset(draft, "zeta"), "alpha");
    draft = stageSecret(draft, "b-key", { kind: "rotate" });
    draft = stageSecret(draft, "a-key", { kind: "set", value: "v", externalProvider: true });
    draft = stageProfile(stageModPack(draft, 4), { profileId: "p", revisionId: null });

    const plan = buildApplyPlan(draft, entries);

    expect(plan.map((step) => step.id)).toEqual([
      "modPack",
      "profile",
      "settings",
      "secret:a-key",
      "secret:b-key",
    ]);
    const settings = plan[2];
    expect(settings).toMatchObject({
      kind: "settings",
      values: [
        { key: "flag", value: true },
        { key: "lines", value: ["a", "b", "c"] },
        { key: "port", value: 80 },
      ],
      resetKeys: ["alpha", "zeta"],
      itemCount: 5,
      dependsOnProfile: true,
    });
  });

  it("marks the settings step independent without a profile step and skips unknown entries", () => {
    const draft = stageValue(stageValue(emptyServerChangeDraft, "port", "1"), "unknown", "x");
    const plan = buildApplyPlan(draft, entries);
    expect(plan).toHaveLength(1);
    expect(plan[0]).toMatchObject({ kind: "settings", itemCount: 1, dependsOnProfile: false });
  });

  it("emits no settings step without values or resets", () => {
    expect(buildApplyPlan(stageModPack(emptyServerChangeDraft, 1), entries)).toHaveLength(1);
  });
});

describe("failureReason", () => {
  it("maps the API error code and never reads the message", () => {
    expect(failureReason(apiError("forbidden"))).toBe("forbidden");
    expect(failureReason(apiError("conflict"))).toBe("conflict");
    expect(failureReason(apiError("server"))).toBe("other");
    expect(failureReason(new Error("forbidden"))).toBe("other");
    expect(failureReason("conflict")).toBe("other");
  });
});

describe("apply runner", () => {
  const entries = [
    configurationEntry({ key: "port", kind: "Integer", value: 1 }),
    configurationEntry({ key: "name", value: "x", source: "Server" }),
  ];

  function stagedDraft(): ServerChangeDraft {
    let draft = stageModPack(emptyServerChangeDraft, 4);
    draft = stageProfile(draft, { profileId: "p", revisionId: null });
    draft = stageValue(draft, "port", "80");
    draft = stageReset(draft, "name");
    return stageSecret(draft, "token", { kind: "set", value: "tok", externalProvider: false });
  }

  function executors(overrides: Partial<ApplyExecutors> = {}): {
    calls: string[];
    executors: ApplyExecutors;
  } {
    const calls: string[] = [];
    return {
      calls,
      executors: {
        changeModPack: vi.fn((to) => {
          calls.push(`modPack:${to}`);
          return Promise.resolve();
        }),
        selectProfile: vi.fn((profile) => {
          calls.push(`profile:${profile.profileId}`);
          return Promise.resolve();
        }),
        updateConfiguration: vi.fn((values, resetKeys) => {
          calls.push(`settings:${values.length}:${resetKeys.length}`);
          return Promise.resolve();
        }),
        setSecret: vi.fn((key) => {
          calls.push(`set:${key}`);
          return Promise.resolve();
        }),
        rotateSecret: vi.fn((key) => {
          calls.push(`rotate:${key}`);
          return Promise.resolve();
        }),
        ...overrides,
      },
    };
  }

  it("applies every step in order when nothing fails", async () => {
    const run = executors();
    const results = await runApplyPlan(buildApplyPlan(stagedDraft(), entries), run.executors);
    expect(run.calls).toEqual(["modPack:4", "profile:p", "settings:1:1", "set:token"]);
    expect(results.every((result) => result.state === "applied")).toBe(true);
  });

  it("keeps independent steps running when the profile step conflicts (D-04)", async () => {
    const run = executors({
      selectProfile: vi.fn(() => Promise.reject(apiError("conflict"))),
    });
    const draft = stageSecret(stagedDraft(), "other", { kind: "rotate" });
    const plan = buildApplyPlan(draft, entries);

    const results = await runApplyPlan(plan, run.executors);

    expect(results).toEqual([
      { id: "modPack", itemCount: 1, state: "applied" },
      { id: "profile", itemCount: 1, state: "failed", reason: "conflict" },
      { id: "settings", itemCount: 2, state: "notTried", reason: "dependency" },
      { id: "secret:other", itemCount: 1, state: "applied" },
      { id: "secret:token", itemCount: 1, state: "applied" },
    ]);
    expect(run.calls).toEqual(["modPack:4", "rotate:other", "set:token"]);

    const remaining = removeApplied(draft, results);
    expect(remaining.modPack).toBeNull();
    expect(remaining.secrets.size).toBe(0);
    expect(remaining.profile).toEqual({ profileId: "p", revisionId: null });
    expect([...remaining.values.keys()]).toEqual(["port"]);
    expect([...remaining.resets]).toEqual(["name"]);
  });

  it("reports a failed settings step without blocking the credentials", async () => {
    const run = executors({
      updateConfiguration: vi.fn(() => Promise.reject(apiError("forbidden"))),
    });
    const results = await runApplyPlan(buildApplyPlan(stagedDraft(), entries), run.executors);
    expect(results.map((result) => result.state)).toEqual([
      "applied",
      "applied",
      "failed",
      "applied",
    ]);
    expect(results[2]).toMatchObject({ reason: "forbidden" });
  });

  it("never throws, whatever the executors reject with", async () => {
    const run = executors({
      changeModPack: vi.fn(() => Promise.reject(new Error("boom"))),
    });
    const results = await runApplyPlan(buildApplyPlan(stagedDraft(), entries), run.executors);
    expect(results[0]).toMatchObject({ state: "failed", reason: "other" });
  });

  it("summarizes items rather than steps", async () => {
    const run = executors({
      selectProfile: vi.fn(() => Promise.reject(apiError("conflict"))),
    });
    const results = await runApplyPlan(buildApplyPlan(stagedDraft(), entries), run.executors);
    expect(summarizeResults(results)).toEqual({ applied: 2, total: 5, remaining: 3 });
  });
});

describe("configurationEntryIssue and validateStagedSettings", () => {
  it("returns typed issues", () => {
    const required = configurationEntry({ key: "r", required: true });
    expect(configurationEntryIssue(required, "  ")).toEqual({ code: "required" });
    expect(configurationEntryIssue(configurationEntry({ key: "o" }), "")).toBeUndefined();
    const integer = configurationEntry({ key: "i", kind: "Integer", minimum: 2, maximum: 9 });
    expect(configurationEntryIssue(integer, "1.5")).toEqual({ code: "number" });
    expect(configurationEntryIssue(integer, "1")).toEqual({ code: "min", min: 2 });
    expect(configurationEntryIssue(integer, "10")).toEqual({ code: "max", max: 9 });
    expect(configurationEntryIssue(integer, "5")).toBeUndefined();
    const decimal = configurationEntry({ key: "d", kind: "Number" });
    expect(configurationEntryIssue(decimal, "abc")).toEqual({ code: "decimal" });
    const choice = configurationEntry({
      key: "c",
      kind: "Selection",
      choices: [{ value: "a", label: "A", description: null }],
    });
    expect(configurationEntryIssue(choice, "b")).toEqual({ code: "choice" });
    expect(configurationEntryIssue(choice, "a")).toBeUndefined();
    const allowed = configurationEntry({ key: "w", allowedValues: ["x", "y"] });
    expect(configurationEntryIssue(allowed, "z")).toEqual({ code: "choice" });
    expect(configurationEntryIssue(allowed, "x")).toBeUndefined();
  });

  it("lists invalid staged values in entries order and ignores resets", () => {
    const entries = [
      configurationEntry({ key: "b", kind: "Integer" }),
      configurationEntry({ key: "a", kind: "Integer", source: "Server" }),
      configurationEntry({ key: "c", kind: "Integer" }),
    ];
    let draft = stageValue(emptyServerChangeDraft, "a", "x");
    draft = stageValue(draft, "b", "y");
    draft = stageValue(draft, "c", "3");
    draft = stageReset(draft, "unknown");
    expect(validateStagedSettings(draft, entries)).toEqual(["b", "a"]);
    expect(validateStagedSettings(stageReset(emptyServerChangeDraft, "a"), entries)).toEqual([]);
  });
});

describe("describeReview and removeReviewItem", () => {
  const entries = [
    configurationEntry({ key: "port", group: "Network", order: 2, kind: "Integer", value: 10 }),
    configurationEntry({ key: "flag", group: "General", order: 5, kind: "Boolean", value: false }),
    configurationEntry({
      key: "mode",
      displayName: "Mode",
      group: "General",
      order: 1,
      kind: "Selection",
      value: "x",
      choices: [
        { value: "x", label: "Ex", description: null },
        { value: "y", label: "Why", description: null },
      ],
    }),
    configurationEntry({ key: "note", group: "General", order: 9 }),
    configurationEntry({
      key: "lines",
      group: "Network",
      order: 1,
      kind: "StringArray",
      value: ["l1"],
      source: "Server",
    }),
    configurationEntry({ key: "token", sensitive: true }),
  ];
  const context: ReviewContext = {
    savedModPack: { id: 7, title: "Seven" },
    modPackTitles: new Map([
      [7, "Seven"],
      [8, "Eight"],
    ]),
    profiles: [
      { primaryKey: "p1", title: "P One", latestRevisionNumber: 3 },
      { primaryKey: "p2", title: "P Two", latestRevisionNumber: 5 },
    ],
    savedProfile: { profileId: "p1", revisionId: null },
    savedProfileTitle: "P One",
    savedProfileRevisionNumber: 3,
    entries,
    secrets: [{ key: "adminPassword", displayName: "Admin password" }],
  };

  function fullDraft(): ServerChangeDraft {
    let draft = stageModPack(emptyServerChangeDraft, null);
    draft = stageProfile(draft, { profileId: "p2", revisionId: "r9" });
    draft = stageValue(draft, "port", "20");
    draft = stageValue(draft, "flag", "true");
    draft = stageValue(draft, "mode", "y");
    draft = stageValue(draft, "note", "");
    draft = stageReset(draft, "lines");
    draft = stageSecret(draft, "adminPassword", {
      kind: "set",
      value: "super-secret-value",
      externalProvider: false,
    });
    return stageSecret(draft, "generated", { kind: "rotate" });
  }

  it("orders groups, sorts settings and describes values", () => {
    const groups = describeReview(fullDraft(), context);
    expect(groups.map((group) => group.kind)).toEqual([
      "modPack",
      "profile",
      "settings",
      "credentials",
    ]);
    expect(groups[0]?.items[0]).toMatchObject({
      before: { kind: "text", text: "Seven", apiText: true },
      after: { kind: "noModPack" },
    });
    expect(groups[1]?.items[0]).toMatchObject({
      before: { kind: "profileLatest", title: "P One" },
      after: { kind: "profilePinned", title: "P Two", revision: 5 },
    });
    const settings = groups[2]?.items ?? [];
    expect(settings.map((item) => item.id)).toEqual([
      "value:mode",
      "value:flag",
      "value:note",
      "reset:lines",
      "value:port",
    ]);
    expect(settings[0]).toMatchObject({
      name: "Mode",
      nameIsApiText: true,
      before: { kind: "text", text: "Ex", apiText: true },
      after: { kind: "text", text: "Why", apiText: true },
    });
    expect(settings[1]).toMatchObject({
      before: { kind: "boolean", value: false },
      after: { kind: "boolean", value: true },
    });
    expect(settings[2]).toMatchObject({ after: { kind: "notSet" } });
    expect(settings[3]).toMatchObject({
      before: { kind: "text", text: "l1", apiText: false },
      after: { kind: "inherited" },
    });
    expect(settings[4]).toMatchObject({
      before: { kind: "text", text: "10", apiText: false },
      after: { kind: "text", text: "20", apiText: false },
    });
    const credentials = groups[3]?.items ?? [];
    expect(credentials.map((item) => item.id)).toEqual([
      "secret:adminPassword",
      "secret:generated",
    ]);
    expect(credentials[0]).toMatchObject({
      name: "Admin password",
      stepId: "secret:adminPassword",
      after: { kind: "secretChanged" },
    });
    expect(credentials[1]).toMatchObject({ name: "generated", after: { kind: "secretRotated" } });
  });

  it("keeps the saved revision number unless a different revision is staged", () => {
    const draft = stageProfile(emptyServerChangeDraft, { profileId: "p1", revisionId: null });
    const pinnedSaved: ReviewContext = {
      ...context,
      savedProfile: { profileId: "p1", revisionId: "r1" },
      savedProfileRevisionNumber: 2,
    };
    const same = describeReview(
      stageProfile(draft, { profileId: "p1", revisionId: "r1" }),
      pinnedSaved,
    );
    expect(same[0]?.items[0]?.after).toEqual({
      kind: "profilePinned",
      title: "P One",
      revision: 2,
    });
    const toDefaults = describeReview(
      stageProfile(draft, { profileId: null, revisionId: null }),
      context,
    );
    expect(toDefaults[0]?.items[0]?.after).toEqual({ kind: "gameDefaults" });
  });

  it("omits empty groups and never exposes a staged secret value or a sensitive entry", () => {
    expect(describeReview(emptyServerChangeDraft, context)).toEqual([]);
    const draft = stageValue(
      stageSecret(emptyServerChangeDraft, "adminPassword", {
        kind: "set",
        value: "super-secret-value",
        externalProvider: true,
      }),
      "token",
      "typed-token",
    );
    const serialized = JSON.stringify(describeReview(draft, context));
    expect(serialized).not.toContain("super-secret-value");
    expect(serialized).not.toContain("typed-token");
    expect(describeReview(draft, context).map((group) => group.kind)).toEqual(["credentials"]);
  });

  it("removes exactly one item per review id", () => {
    const draft = fullDraft();
    expect([...removeReviewItem(draft, "value:port").values.keys()]).not.toContain("port");
    expect(removeReviewItem(draft, "value:port").values.size).toBe(draft.values.size - 1);
    expect(removeReviewItem(draft, "reset:lines").resets.size).toBe(0);
    expect(removeReviewItem(draft, "modPack").modPack).toBeNull();
    expect(removeReviewItem(draft, "profile").profile).toBeNull();
    const withoutSecret = removeReviewItem(draft, "secret:adminPassword");
    expect([...withoutSecret.secrets.keys()]).toEqual(["generated"]);
    expect(withoutSecret.values.size).toBe(draft.values.size);
  });
});

describe("credentialValueIssue", () => {
  it("reports a blank value after trimming", () => {
    expect(credentialValueIssue("   ", "Token")).toBe("blank");
    expect(credentialValueIssue("", "SingleLine")).toBe("blank");
  });

  it("reports a value longer than 4096 characters and accepts exactly 4096", () => {
    expect(credentialValueIssue("a".repeat(4097), "Token")).toBe("tooLong");
    expect(credentialValueIssue("a".repeat(4096), "Token")).toBeUndefined();
    expect(credentialValueIssue(` ${"a".repeat(4096)} `, "Token")).toBeUndefined();
  });

  it("reports control characters for every format", () => {
    for (const format of ["SingleLine", "Token"] as const) {
      expect(credentialValueIssue("a\nb", format)).toBe("control");
      expect(credentialValueIssue("a\u007fb", format)).toBe("control");
      expect(credentialValueIssue("a\u0085b", format)).toBe("control");
    }
  });

  it("reports whitespace, quotes, backslashes and non-ASCII characters for the Token format", () => {
    expect(credentialValueIssue("two words", "Token")).toBe("token");
    expect(credentialValueIssue('with"quote', "Token")).toBe("token");
    expect(credentialValueIssue("with\\backslash", "Token")).toBe("token");
    expect(credentialValueIssue("ümlaut1", "Token")).toBe("token");
  });

  it("accepts a trimmed Token value and spaces in a SingleLine value", () => {
    expect(credentialValueIssue(" ok ", "Token")).toBeUndefined();
    expect(credentialValueIssue("Chosen-Value_42", "Token")).toBeUndefined();
    expect(credentialValueIssue("two words", "SingleLine")).toBeUndefined();
    expect(credentialValueIssue("ümlaut1", "SingleLine")).toBeUndefined();
  });

  it("never carries the value in its result", () => {
    expect(credentialValueIssue("secret value", "Token")).toBe("token");
  });
});

describe("validateStagedSecrets", () => {
  const set = (value: string) => ({ kind: "set", value, externalProvider: false }) as const;
  const statuses = [
    { key: "first", valueFormat: "Token" },
    { key: "second", valueFormat: "SingleLine" },
    { key: "third", valueFormat: "Token" },
  ] as const;

  it("returns only staged sets with an issue, in the order of the secret list", () => {
    let draft = stageSecret(emptyServerChangeDraft, "third", set("bad value"));
    draft = stageSecret(draft, "first", set("bad value"));
    draft = stageSecret(draft, "second", set("fine value"));

    expect(validateStagedSecrets(draft, statuses)).toEqual(["first", "third"]);
  });

  it("ignores rotations and keys without a status", () => {
    let draft = stageSecret(emptyServerChangeDraft, "first", { kind: "rotate" });
    draft = stageSecret(draft, "unknown", set("bad value"));

    expect(validateStagedSecrets(draft, statuses)).toEqual([]);
  });
});
