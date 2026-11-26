import {
  actionCostCategory,
  confirmVariantFor,
  confirmationCopyKeysFor,
  dismissKeyFor,
  gridVariantFor,
  isCostBearingAction,
  lookupActionVocabulary,
  pendingKeyFor,
  toastKeyFor,
} from "@/features/hosting/actionVocabulary";

const knownIds = ["start", "stop", "terminate", "restart", "install", "update", "collect-logs"];
const prototypeNames = ["constructor", "__proto__", "toString", "hasOwnProperty"];

describe("lookupActionVocabulary", () => {
  it.each(knownIds)("finds %s", (id) => {
    expect(lookupActionVocabulary(id)).toBeDefined();
  });

  it("matches ids case-insensitively", () => {
    expect(lookupActionVocabulary("START")).toBe(lookupActionVocabulary("start"));
    expect(lookupActionVocabulary("Collect-Logs")).toBe(lookupActionVocabulary("collect-logs"));
  });

  it.each(["unknown", "", null, undefined, ...prototypeNames])("returns nothing for %s", (id) => {
    expect(lookupActionVocabulary(id)).toBeUndefined();
  });

  it("gives every known id distinct label, dismiss and pending keys that are never generic", () => {
    for (const id of knownIds) {
      const entry = lookupActionVocabulary(id);
      expect(entry).toBeDefined();
      const keys = [entry?.label, entry?.dismiss, entry?.pending];
      expect(new Set(keys).size).toBe(3);
      for (const key of keys) expect(String(key)).toMatch(/^Server_Action_/);
      expect(String(entry?.dismiss)).not.toMatch(/Cancel/);
    }
  });
});

describe("actionCostCategory", () => {
  it.each([
    ["NoCostImpact", "none"],
    ["StartsCost", "starts"],
    ["StopsCost", "stops"],
    ["KeepsCostRunning", "keeps"],
    ["Unknown", "unknown"],
  ])("maps %s to %s", (impact, expected) => {
    expect(actionCostCategory({ costImpact: impact })).toBe(expected);
  });

  it.each([undefined, null, "None", "arbitrary text", "", 3, "constructor", "__proto__"])(
    "classifies %s as unknown, never as none",
    (impact) => {
      expect(actionCostCategory({ costImpact: impact })).toBe("unknown");
    },
  );

  it("classifies a missing action as unknown", () => {
    expect(actionCostCategory(undefined)).toBe("unknown");
    expect(actionCostCategory(null)).toBe("unknown");
    expect(actionCostCategory({})).toBe("unknown");
  });
});

describe("isCostBearingAction", () => {
  it.each([
    ["StartsCost", true],
    ["StopsCost", true],
    ["KeepsCostRunning", false],
    ["NoCostImpact", false],
    ["Unknown", false],
    ["something else", false],
  ])("returns %s -> %s", (impact, expected) => {
    expect(isCostBearingAction({ costImpact: impact })).toBe(expected);
  });
});

describe("confirmVariantFor", () => {
  it("uses gold for start and danger for stop and terminate", () => {
    expect(confirmVariantFor("start")).toBe("gold");
    expect(confirmVariantFor("stop")).toBe("danger");
    expect(confirmVariantFor("Terminate")).toBe("danger");
  });

  it.each(["restart", "install", "update", "collect-logs", "unknown", ...prototypeNames])(
    "uses gold for %s",
    (id) => {
      expect(confirmVariantFor(id)).toBe("gold");
    },
  );

  it("uses gold without an id", () => {
    expect(confirmVariantFor(null)).toBe("gold");
    expect(confirmVariantFor(undefined)).toBe("gold");
  });
});

describe("gridVariantFor", () => {
  it("uses danger for the cost-stop actions", () => {
    expect(gridVariantFor("stop")).toBe("danger");
    expect(gridVariantFor("Terminate")).toBe("danger");
  });

  it.each(["start", "restart", "install", "update", "collect-logs", "unknown", ...prototypeNames])(
    "uses the neutral primary variant for %s, never gold",
    (id) => {
      expect(gridVariantFor(id)).toBe("primary");
    },
  );

  it("uses the neutral primary variant without an id", () => {
    expect(gridVariantFor(null)).toBe("primary");
    expect(gridVariantFor(undefined)).toBe("primary");
  });
});

describe("dismissKeyFor and pendingKeyFor", () => {
  it("returns the action keys for known ids", () => {
    expect(dismissKeyFor("start")).toBe("Server_Action_Start_Dismiss");
    expect(dismissKeyFor("collect-logs")).toBe("Server_Action_CollectLogs_Dismiss");
    expect(pendingKeyFor("stop")).toBe("Server_Action_Stop_Pending");
    expect(pendingKeyFor("Install")).toBe("Server_Action_Install_Pending");
  });

  it.each(["export-world", "", null, undefined, ...prototypeNames])(
    "returns the Other keys for %s",
    (id) => {
      expect(dismissKeyFor(id)).toBe("Server_Action_Other_Dismiss");
      expect(pendingKeyFor(id)).toBe("Server_Action_Other_Pending");
    },
  );
});

describe("confirmationCopyKeysFor", () => {
  it.each([
    ["start", "Server_Confirm_Title_Start", "Server_Confirm_Body_Start"],
    ["stop", "Server_Confirm_Title_Stop", "Server_Confirm_Body_Stop"],
    ["terminate", "Server_Confirm_Title_Terminate", "Server_Confirm_Body_Terminate"],
  ])("returns dedicated copy for %s", (id, titleKey, bodyKey) => {
    expect(confirmationCopyKeysFor(id)).toEqual({ titleKey, bodyKey, dedicated: true });
  });

  it.each(["restart", "collect-logs", "export-world", null, undefined, ...prototypeNames])(
    "returns the Other copy for %s",
    (id) => {
      expect(confirmationCopyKeysFor(id)).toEqual({
        titleKey: "Server_Confirm_Title_Other",
        bodyKey: "Server_Confirm_Body_Other",
        dedicated: false,
      });
    },
  );
});

describe("toastKeyFor", () => {
  it("returns the dedicated toasts for start, stop and terminate", () => {
    expect(toastKeyFor("start")).toBe("Server_Toast_StartRequested");
    expect(toastKeyFor("stop")).toBe("Server_Toast_ShutdownRequested");
    expect(toastKeyFor("terminate")).toBe("Server_Toast_ShutdownRequested");
  });

  it.each(["restart", "collect-logs", "unknown", null, undefined, ...prototypeNames])(
    "returns the generic toast for %s",
    (id) => {
      expect(toastKeyFor(id)).toBe("Server_Toast_ActionRequested");
    },
  );
});
