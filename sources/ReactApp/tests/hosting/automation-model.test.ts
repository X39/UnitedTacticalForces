import {
  draftFromSaved,
  formatWallTime,
  idleStatusKey,
  isAutomationDraftDirty,
  lastRunKey,
  needsStartConsent,
  parseWholeNumber,
  ruleDiffersFromSaved,
  ruleSummaryKey,
  sortRulesForDisplay,
  timeZoneOptions,
  toAutomationUpdate,
  validateAutomationDraft,
  weekdayLabels,
  type AutomationDraft,
  type AutomationLimitsInput,
  type RuleDraft,
  type SavedAutomationInput,
} from "@/features/hosting/automationModel";

const limits: AutomationLimitsInput = {
  idleMinutesMin: 5,
  idleMinutesMax: 1440,
  maxRuntimeHoursMin: 1,
  maxRuntimeHoursMax: 168,
  maxSchedules: 3,
  warningLeadMinutes: 10,
};

function savedSettings(overrides: Partial<SavedAutomationInput> = {}): SavedAutomationInput {
  return {
    idleShutdownEnabled: true,
    idleShutdownMinutes: 30,
    maxRuntimeEnabled: false,
    maxRuntimeHours: 6,
    timeZone: "Europe/Berlin",
    schedules: [
      {
        id: "rule-start",
        action: "Start",
        days: [5, 6],
        time: "18:00",
        nextRunAt: "2026-11-27T17:00:00Z",
        lastRun: null,
      },
      {
        id: "rule-stop",
        action: "Stop",
        days: [6],
        time: "23:30",
        nextRunAt: null,
        lastRun: { at: "2026-11-21T22:30:00Z", outcome: "Stopped" },
      },
    ],
    version: 7,
    ...overrides,
  };
}

function keySource(): () => string {
  let counter = 0;
  return () => `key-${++counter}`;
}

function draftOf(saved: SavedAutomationInput, zone = "UTC"): AutomationDraft {
  return draftFromSaved(saved, zone, keySource());
}

function newRule(overrides: Partial<RuleDraft> = {}): RuleDraft {
  return { key: "new", id: null, action: "Start", days: [], time: "", ...overrides };
}

describe("draftFromSaved", () => {
  it("maps numbers to strings and gives each rule a fresh key with its API id", () => {
    const draft = draftOf(savedSettings());

    expect(draft.idleMinutes).toBe("30");
    expect(draft.maxHours).toBe("6");
    expect(draft.idleShutdownEnabled).toBe(true);
    expect(draft.maxRuntimeEnabled).toBe(false);
    expect(draft.rules.map((rule) => [rule.key, rule.id])).toEqual([
      ["key-1", "rule-start"],
      ["key-2", "rule-stop"],
    ]);
    expect(draft.rules[0]).toMatchObject({ action: "Start", days: [5, 6], time: "18:00" });
  });

  it("uses the browser zone when no zone was saved and keeps a saved zone", () => {
    expect(draftOf(savedSettings({ timeZone: null }), "America/New_York").timeZone).toBe(
      "America/New_York",
    );
    expect(draftOf(savedSettings(), "America/New_York").timeZone).toBe("Europe/Berlin");
  });

  it("shows an action it does not know as a start", () => {
    const saved = savedSettings({
      schedules: [
        { id: "x", action: "Pause", days: [1], time: "08:00", nextRunAt: null, lastRun: null },
      ],
    });

    expect(draftOf(saved).rules[0]?.action).toBe("Start");
  });
});

describe("parseWholeNumber", () => {
  it.each([
    ["", { error: "Required" }],
    ["   ", { error: "Required" }],
    ["4.5", { error: "Number" }],
    ["abc", { error: "Number" }],
    ["-5", { error: "Number" }],
    ["4", { error: "Min" }],
    ["1441", { error: "Max" }],
    [" 30 ", { value: 30 }],
    ["5", { value: 5 }],
    ["1440", { value: 1440 }],
  ])("parses %j", (text, expected) => {
    expect(parseWholeNumber(text, 5, 1440)).toEqual(expected);
  });
});

describe("validateAutomationDraft", () => {
  it("ignores the minutes while idle shutdown is off or the game cannot report players", () => {
    const draft = { ...draftOf(savedSettings()), idleMinutes: "abc" };

    expect(
      validateAutomationDraft({ ...draft, idleShutdownEnabled: false }, limits, true).idleMinutes,
    ).toBeNull();
    expect(validateAutomationDraft(draft, limits, false).idleMinutes).toBeNull();
    expect(validateAutomationDraft(draft, limits, true).idleMinutes).toBe("Number");
  });

  it("ignores the hours while the limit is off and checks them when it is on", () => {
    const draft = { ...draftOf(savedSettings()), maxHours: "999" };

    expect(validateAutomationDraft(draft, limits, true).maxHours).toBeNull();
    expect(
      validateAutomationDraft({ ...draft, maxRuntimeEnabled: true }, limits, true).maxHours,
    ).toBe("Max");
  });

  it("marks a rule without days and a rule without a time", () => {
    const draft = {
      ...draftOf(savedSettings({ schedules: [] })),
      rules: [newRule({ days: [], time: "10:00" }), newRule({ days: [1], time: "" })],
    };
    const verdict = validateAutomationDraft(draft, limits, true);

    expect(verdict.rules[0]).toEqual({ days: true, time: false, duplicate: false });
    expect(verdict.rules[1]).toEqual({ days: false, time: true, duplicate: false });
    expect(verdict.invalidCount).toBe(2);
    expect(verdict.valid).toBe(false);
  });

  it("treats a blank new rule as invalid on a save attempt", () => {
    const draft = { ...draftOf(savedSettings({ schedules: [] })), rules: [newRule()] };
    const verdict = validateAutomationDraft(draft, limits, true);

    expect(verdict.rules[0]).toEqual({ days: true, time: true, duplicate: false });
    expect(verdict.invalidCount).toBe(2);
  });

  it("marks both rules that share a weekday and a time as duplicate", () => {
    const draft = {
      ...draftOf(savedSettings({ schedules: [] })),
      rules: [
        newRule({ key: "a", days: [1, 2], time: "18:00" }),
        newRule({ key: "b", action: "Stop", days: [2, 3], time: "18:00" }),
        newRule({ key: "c", days: [2], time: "19:00" }),
      ],
    };
    const verdict = validateAutomationDraft(draft, limits, true);

    expect(verdict.rules.map((rule) => rule.duplicate)).toEqual([true, true, false]);
    expect(verdict.invalidCount).toBe(2);
    expect(verdict.valid).toBe(false);
  });

  it("reports overLimit when the rules exceed the schedule limit", () => {
    const rules = [1, 2, 3, 4].map((day) =>
      newRule({ key: `r${day}`, days: [day], time: "08:00" }),
    );
    const verdict = validateAutomationDraft(
      { ...draftOf(savedSettings({ schedules: [] })), rules },
      limits,
      true,
    );

    expect(verdict.overLimit).toBe(true);
    expect(verdict.invalidCount).toBe(0);
    expect(verdict.valid).toBe(false);
  });

  it("counts invalid fields and accepts a clean draft", () => {
    const clean = validateAutomationDraft(draftOf(savedSettings()), limits, true);
    const broken = validateAutomationDraft(
      { ...draftOf(savedSettings()), idleMinutes: "", maxRuntimeEnabled: true, maxHours: "0" },
      limits,
      true,
    );

    expect(clean).toMatchObject({ invalidCount: 0, valid: true, overLimit: false });
    expect(broken.idleMinutes).toBe("Required");
    expect(broken.maxHours).toBe("Min");
    expect(broken.invalidCount).toBe(2);
  });
});

describe("isAutomationDraftDirty", () => {
  it("is false for an unchanged draft", () => {
    const saved = savedSettings();

    expect(isAutomationDraftDirty(draftOf(saved), saved, "UTC")).toBe(false);
  });

  it("is false for a draft whose zone is the browser zone of a server without a saved zone", () => {
    const saved = savedSettings({ timeZone: null });

    expect(isAutomationDraftDirty(draftOf(saved, "Asia/Tokyo"), saved, "Asia/Tokyo")).toBe(false);
  });

  it("is false for a disabled number that was edited", () => {
    const saved = savedSettings();
    const draft = { ...draftOf(saved), maxHours: "42" };

    expect(isAutomationDraftDirty(draft, saved, "UTC")).toBe(false);
  });

  it("is false for an added blank rule", () => {
    const saved = savedSettings();
    const draft = draftOf(saved);
    draft.rules.push(newRule());

    expect(isAutomationDraftDirty(draft, saved, "UTC")).toBe(false);
  });

  it("is false for a day set that was picked in another order", () => {
    const saved = savedSettings();
    const draft = draftOf(saved);
    draft.rules[0] = { ...draft.rules[0]!, days: [6, 5] };

    expect(isAutomationDraftDirty(draft, saved, "UTC")).toBe(false);
  });

  it("is true for a changed zone and for a toggled setting", () => {
    const saved = savedSettings();

    expect(isAutomationDraftDirty({ ...draftOf(saved), timeZone: "UTC" }, saved, "UTC")).toBe(true);
    expect(
      isAutomationDraftDirty({ ...draftOf(saved), idleShutdownEnabled: false }, saved, "UTC"),
    ).toBe(true);
    expect(
      isAutomationDraftDirty({ ...draftOf(saved), maxRuntimeEnabled: true }, saved, "UTC"),
    ).toBe(true);
  });

  it("is true for an edited enabled number, including a blank one", () => {
    const saved = savedSettings();

    expect(isAutomationDraftDirty({ ...draftOf(saved), idleMinutes: "45" }, saved, "UTC")).toBe(
      true,
    );
    expect(isAutomationDraftDirty({ ...draftOf(saved), idleMinutes: "" }, saved, "UTC")).toBe(true);
    expect(isAutomationDraftDirty({ ...draftOf(saved), idleMinutes: " 30 " }, saved, "UTC")).toBe(
      false,
    );
  });

  it("is true for a new rule with days or a time and for an edited or removed rule", () => {
    const saved = savedSettings();
    const withDay = draftOf(saved);
    withDay.rules.push(newRule({ days: [1] }));
    const withTime = draftOf(saved);
    withTime.rules.push(newRule({ time: "07:00" }));
    const edited = draftOf(saved);
    edited.rules[1] = { ...edited.rules[1]!, time: "23:45" };
    const removed = draftOf(saved);
    removed.rules.pop();

    for (const draft of [withDay, withTime, edited, removed]) {
      expect(isAutomationDraftDirty(draft, saved, "UTC")).toBe(true);
    }
  });
});

describe("toAutomationUpdate", () => {
  it("sends the saved number for a disabled field and the parsed number for an enabled one", () => {
    const saved = savedSettings();
    const draft = { ...draftOf(saved), idleMinutes: " 45 ", maxHours: "99" };
    const body = toAutomationUpdate(draft, saved, false);

    expect(body).toMatchObject({
      idleShutdownEnabled: true,
      idleShutdownMinutes: 45,
      maxRuntimeEnabled: false,
      maxRuntimeHours: 6,
    });
  });

  it("keeps the saved minutes when idle shutdown is off and never sends it enabled when unavailable", () => {
    const saved = savedSettings();
    const off = toAutomationUpdate(
      { ...draftOf(saved), idleShutdownEnabled: false, idleMinutes: "77" },
      saved,
      false,
    );
    const unavailable = toAutomationUpdate(
      { ...draftOf(saved), idleMinutes: "77" },
      saved,
      false,
      false,
    );

    expect(off).toMatchObject({ idleShutdownEnabled: false, idleShutdownMinutes: 30 });
    expect(unavailable).toMatchObject({ idleShutdownEnabled: false, idleShutdownMinutes: 30 });
  });

  it("sends ISO days ascending, null ids for new rules and the saved version", () => {
    const saved = savedSettings();
    const draft = draftOf(saved);
    draft.rules[0] = { ...draft.rules[0]!, days: [6, 5] };
    draft.rules.push(newRule({ action: "Stop", days: [7, 1], time: "9:05" }));
    const body = toAutomationUpdate(draft, saved, true);

    expect(body.version).toBe(7);
    expect(body.startConsent).toBe(true);
    expect(body.timeZone).toBe("Europe/Berlin");
    expect(body.schedules).toEqual([
      { id: "rule-start", action: "Start", days: [5, 6], time: "18:00" },
      { id: "rule-stop", action: "Stop", days: [6], time: "23:30" },
      { id: null, action: "Stop", days: [1, 7], time: "09:05" },
    ]);
  });
});

describe("needsStartConsent", () => {
  it("is true for a new start rule and for a start rule whose action, days or time changed", () => {
    const saved = savedSettings();
    const added = draftOf(saved);
    added.rules.push(newRule({ days: [1], time: "08:00" }));
    const days = draftOf(saved);
    days.rules[0] = { ...days.rules[0]!, days: [5] };
    const time = draftOf(saved);
    time.rules[0] = { ...time.rules[0]!, time: "18:30" };
    const action = draftOf(saved);
    action.rules[1] = { ...action.rules[1]!, action: "Start" };

    for (const draft of [added, days, time, action]) {
      expect(needsStartConsent(draft, saved)).toBe(true);
    }
  });

  it("is false for stop-only changes, unchanged start rules and a blank new rule", () => {
    const saved = savedSettings();
    const stopChanged = draftOf(saved);
    stopChanged.rules[1] = { ...stopChanged.rules[1]!, time: "22:00" };
    const stopAdded = draftOf(saved);
    stopAdded.rules.push(newRule({ action: "Stop", days: [2], time: "10:00" }));
    const blank = draftOf(saved);
    blank.rules.push(newRule());
    const reordered = draftOf(saved);
    reordered.rules[0] = { ...reordered.rules[0]!, days: [6, 5] };

    expect(needsStartConsent(draftOf(saved), saved)).toBe(false);
    for (const draft of [stopChanged, stopAdded, blank, reordered]) {
      expect(needsStartConsent(draft, saved)).toBe(false);
    }
  });
});

describe("ruleDiffersFromSaved", () => {
  it("is true without a saved rule and when the action, days or time differ", () => {
    const saved = savedSettings();
    const draft = draftOf(saved);
    const rule = draft.rules[0]!;
    const match = saved.schedules[0]!;

    expect(ruleDiffersFromSaved(rule, null)).toBe(true);
    expect(ruleDiffersFromSaved(rule, match)).toBe(false);
    expect(ruleDiffersFromSaved({ ...rule, days: [...rule.days].reverse() }, match)).toBe(false);
    expect(ruleDiffersFromSaved({ ...rule, action: "Stop" }, match)).toBe(true);
    expect(ruleDiffersFromSaved({ ...rule, days: [...rule.days, 7] }, match)).toBe(true);
    expect(ruleDiffersFromSaved({ ...rule, time: "03:45" }, match)).toBe(true);
  });
});

describe("sortRulesForDisplay", () => {
  it("orders by the first selected day, then by time, without changing the input", () => {
    const rules = [
      { name: "c", days: [3], time: "08:00" },
      { name: "b", days: [5, 1], time: "20:00" },
      { name: "a", days: [1], time: "09:00" },
      { name: "d", days: [], time: "00:00" },
      { name: "e", days: [3], time: "07:00" },
    ];
    const sorted = sortRulesForDisplay(rules);

    expect(sorted.map((rule) => rule.name)).toEqual(["a", "b", "e", "c", "d"]);
    expect(rules.map((rule) => rule.name)).toEqual(["c", "b", "a", "d", "e"]);
  });
});

describe("idleStatusKey", () => {
  it.each([
    [{ available: false, enabled: true, state: "Idle" }, "NotAvailable"],
    [{ available: true, enabled: false, state: "Idle" }, "Off"],
    [{ available: true, enabled: true, state: "Unknown" }, "Unknown"],
    [{ available: true, enabled: true, state: "SomethingNew" }, "Unknown"],
    [{ available: true, enabled: true, state: "" }, "Unknown"],
    [{ available: true, enabled: true, state: "PlayersOnline" }, "Players"],
    [{ available: true, enabled: true, state: "Idle" }, "Running"],
    [{ available: true, enabled: true, state: "NotRunning" }, "Armed"],
  ])("maps %j to %s", (input, expected) => {
    expect(idleStatusKey(input)).toBe(expected);
  });
});

describe("lastRunKey", () => {
  it("maps the eleven outcome codes, null and unknown codes", () => {
    const codes = [
      "Started",
      "Stopped",
      "SkippedRunning",
      "SkippedOff",
      "SkippedCost",
      "SkippedConsent",
      "SkippedMissed",
      "SkippedBusy",
      "WaitingForStop",
      "SkippedStopUnfinished",
      "Failed",
    ];

    for (const code of codes) expect(lastRunKey({ outcome: code })).toBe(code);
    expect(lastRunKey(null)).toBe("None");
    expect(lastRunKey({ outcome: "SomethingNew" })).toBe("Other");
    expect(lastRunKey({ outcome: "" })).toBe("Other");
  });
});

describe("ruleSummaryKey", () => {
  it("chooses daily, weekdays, days and incomplete", () => {
    expect(ruleSummaryKey([1, 2, 3, 4, 5, 6, 7], "18:00")).toBe("Daily");
    expect(ruleSummaryKey([7, 3, 1, 2, 4, 5, 6], "18:00")).toBe("Daily");
    expect(ruleSummaryKey([1, 2, 3, 4, 5], "18:00")).toBe("Weekdays");
    expect(ruleSummaryKey([1, 3, 5], "18:00")).toBe("Days");
    expect(ruleSummaryKey([6, 7], "18:00")).toBe("Days");
    expect(ruleSummaryKey([], "18:00")).toBe("Incomplete");
    expect(ruleSummaryKey([1, 2], "")).toBe("Incomplete");
  });
});

describe("weekdayLabels", () => {
  it("starts with Monday in English and German", () => {
    const english = weekdayLabels("en-US", "long");
    const german = weekdayLabels("de-DE", "long");

    expect(english.map((entry) => entry.day)).toEqual([1, 2, 3, 4, 5, 6, 7]);
    expect(english[0]?.label).toBe("Monday");
    expect(english[6]?.label).toBe("Sunday");
    expect(german[0]?.label).toBe("Montag");
    expect(german[6]?.label).toBe("Sonntag");
    expect(weekdayLabels("en-US", "short")[0]?.label).toBe("Mon");
  });
});

describe("timeZoneOptions", () => {
  const supported = [
    "Europe/Berlin",
    "America/Argentina/Buenos_Aires",
    "Africa/Abidjan",
    "America/New_York",
    "UTC",
  ];

  it("puts UTC first, groups by region in alphabetical order and replaces underscores in labels", () => {
    const options = timeZoneOptions(supported, "Europe/Berlin");

    expect(options.utc).toBe("UTC");
    expect(options.groups.map((group) => group.region)).toEqual(["Africa", "America", "Europe"]);
    expect(options.groups[1]?.zones).toEqual([
      { id: "America/Argentina/Buenos_Aires", label: "America/Argentina/Buenos Aires" },
      { id: "America/New_York", label: "America/New York" },
    ]);
    expect(options.groups.flatMap((group) => group.zones.map((zone) => zone.id))).not.toContain(
      "UTC",
    );
  });

  it("includes a saved zone that the supported list lacks, once", () => {
    const options = timeZoneOptions(supported, "Pacific/Chatham");
    const ids = options.groups.flatMap((group) => group.zones.map((zone) => zone.id));

    expect(options.groups.map((group) => group.region)).toContain("Pacific");
    expect(ids.filter((id) => id === "Pacific/Chatham")).toHaveLength(1);
    expect(timeZoneOptions(supported, "Europe/Berlin").groups[2]?.zones).toHaveLength(1);
  });

  it("offers UTC only once when UTC is the saved zone", () => {
    const options = timeZoneOptions(["UTC"], "UTC");

    expect(options).toEqual({ utc: "UTC", groups: [] });
  });
});

describe("formatWallTime", () => {
  it("formats an API instant in the given zone without changing it", () => {
    const options: Intl.DateTimeFormatOptions = {
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
    };

    expect(formatWallTime("2026-06-01T16:00:00Z", "Europe/Berlin", "en-US", options)).toBe("18:00");
    expect(formatWallTime("2026-06-01T16:00:00Z", "UTC", "en-US", options)).toBe("16:00");
  });

  it("gives an empty text for an instant or a zone it cannot format", () => {
    const options: Intl.DateTimeFormatOptions = { hour: "2-digit", minute: "2-digit" };

    expect(formatWallTime("not a date", "UTC", "en-US", options)).toBe("");
    expect(formatWallTime("2026-06-01T16:00:00Z", "Not/AZone", "en-US", options)).toBe("");
  });
});
