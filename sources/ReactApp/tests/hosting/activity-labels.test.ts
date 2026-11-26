import {
  actorFor,
  actorLabelKey,
  hiddenActorName,
  jobKindLabelKey,
  triggerFor,
} from "@/features/hosting/activityLabels";
import de from "@/locales/de.json";
import en from "@/locales/en.json";

const english: Record<string, string> = en;
const german: Record<string, string> = de;

describe("activity actor display rule", () => {
  it("treats a null or undefined actor as the platform", () => {
    expect(actorFor(null)).toEqual({ kind: "platform" });
    expect(actorFor(undefined)).toEqual({ kind: "platform" });
  });

  it("treats the API hidden-member placeholder as a hidden member", () => {
    expect(hiddenActorName).toBe("Hidden member");
    expect(actorFor("Hidden member")).toEqual({ kind: "hidden" });
  });

  it("treats a blank name as a hidden member rather than showing an empty actor", () => {
    expect(actorFor("  ")).toEqual({ kind: "hidden" });
    expect(actorFor("")).toEqual({ kind: "hidden" });
  });

  it("returns any other value as the display name", () => {
    expect(actorFor("Falcon One")).toEqual({ kind: "name", name: "Falcon One" });
  });

  it("does not trim a real name and keeps lookalike placeholders as names", () => {
    expect(actorFor(" Falcon One ")).toEqual({ kind: "name", name: " Falcon One " });
    expect(actorFor("hidden member")).toEqual({ kind: "name", name: "hidden member" });
  });

  it("maps the three non-name kinds to their actor label keys", () => {
    expect(actorLabelKey("platform")).toBe("Server_Activity_Actor_Platform");
    expect(actorLabelKey("hidden")).toBe("Server_Activity_Actor_Hidden");
    expect(actorLabelKey("server")).toBe("Server_Activity_Actor_Server");
  });
});

describe("activity job kind labels", () => {
  it.each(["Start", "Stop", "Terminate", "Restart", "Install", "Update", "CollectLogs"])(
    "maps %s to its action label key",
    (kind) => {
      expect(jobKindLabelKey(kind)).toBe(`Server_Action_${kind}_Label`);
    },
  );

  it("maps Custom to the custom kind label", () => {
    expect(jobKindLabelKey("Custom")).toBe("Server_Activity_Kind_Custom");
  });

  it.each(["__proto__", "constructor", "toString", "", "start", "Archive", null, undefined])(
    "maps the unknown kind %j to the unknown kind label",
    (kind) => {
      expect(jobKindLabelKey(kind)).toBe("Server_Activity_Kind_Unknown");
    },
  );

  it("returns only keys that exist in both languages", () => {
    const kinds = [
      "Start",
      "Stop",
      "Terminate",
      "Restart",
      "Install",
      "Update",
      "CollectLogs",
      "Custom",
      "other",
    ];

    for (const kind of kinds) {
      const key = jobKindLabelKey(kind);
      expect(english[key]?.trim(), `en ${key}`).toBeTruthy();
      expect(german[key]?.trim(), `de ${key}`).toBeTruthy();
    }
  });
});

describe("triggerFor", () => {
  it.each([undefined, null, "Manual", "", "__proto__", "constructor", "idle", "Other"])(
    "keeps the actor line for the trigger %j",
    (trigger) => {
      expect(triggerFor(trigger, 30)).toBeNull();
    },
  );

  it("maps an idle trigger to the idle key with the minutes as count", () => {
    expect(triggerFor("Idle", 30)).toEqual({ key: "Server_Activity_Trigger_Idle", count: 30 });
  });

  it("maps a maximum runtime trigger to the max key with the hours as count", () => {
    expect(triggerFor("MaxRuntime", 6)).toEqual({ key: "Server_Activity_Trigger_Max", count: 6 });
    expect(triggerFor("MaxRuntime", 1)).toEqual({ key: "Server_Activity_Trigger_Max", count: 1 });
  });

  it("maps a schedule trigger to the schedule key without a count", () => {
    expect(triggerFor("Schedule", null)).toEqual({
      key: "Server_Activity_Trigger_Schedule",
      count: null,
    });
    expect(triggerFor("Schedule", 5)).toEqual({
      key: "Server_Activity_Trigger_Schedule",
      count: null,
    });
  });

  it.each([null, undefined, Number.NaN])(
    "keeps the actor line for an idle or maximum runtime trigger without the usable limit %j",
    (limit) => {
      expect(triggerFor("Idle", limit)).toBeNull();
      expect(triggerFor("MaxRuntime", limit)).toBeNull();
    },
  );

  it("returns keys that exist in both languages, with both plural forms for the plural keys", () => {
    for (const key of ["Server_Activity_Trigger_Idle", "Server_Activity_Trigger_Max"]) {
      for (const form of ["one", "other"]) {
        expect(english[`${key}_${form}`]?.trim(), `en ${key}_${form}`).toBeTruthy();
        expect(german[`${key}_${form}`]?.trim(), `de ${key}_${form}`).toBeTruthy();
      }
    }
    expect(english["Server_Activity_Trigger_Schedule"]?.trim()).toBeTruthy();
    expect(german["Server_Activity_Trigger_Schedule"]?.trim()).toBeTruthy();
  });
});
