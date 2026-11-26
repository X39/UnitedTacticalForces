import {
  channelStatus,
  formatAlertAmount,
  isAlertDraftDirty,
  normalizeAlertAmounts,
  parseAlertAmount,
  validateAlertDraft,
} from "@/features/hosting/alertSettingsModel";

const max = 10000;
const limits = { maxThresholds: 10, maxThresholdAmount: max };
const saved = { thresholds: [5, 10], discordEnabled: true, emailEnabled: false };

describe("parseAlertAmount", () => {
  it.each([
    ["5", 5],
    ["12.50", 12.5],
    ["12,5", 12.5],
    [" 7 ", 7],
    ["10000", 10000],
    ["0.01", 0.01],
    ["0,05", 0.05],
  ])("accepts %j as %d", (text, value) => {
    expect(parseAlertAmount(text, max)).toEqual({ value });
  });

  it.each([
    ["", "Required"],
    ["   ", "Required"],
    ["abc", "Number"],
    ["5.", "Number"],
    ["1,000.50", "Number"],
    ["1 000", "Number"],
    ["1.234", "Precision"],
    ["0,001", "Precision"],
    ["0", "Min"],
    ["0.00", "Min"],
    ["-1", "Min"],
    ["10000.01", "Max"],
    ["99999", "Max"],
  ])("rejects %j with %s", (text, error) => {
    expect(parseAlertAmount(text, max)).toEqual({ error });
  });

  it("applies the maximum it is given", () => {
    expect(parseAlertAmount("50", 25)).toEqual({ error: "Max" });
    expect(parseAlertAmount("25", 25)).toEqual({ value: 25 });
  });
});

describe("validateAlertDraft", () => {
  it("accepts a clean list and an empty list", () => {
    expect(validateAlertDraft(["5", "10.5"], limits)).toEqual({
      errors: [null, null],
      overLimit: false,
      valid: true,
    });
    expect(validateAlertDraft([], limits).valid).toBe(true);
  });

  it("marks the later row of a duplicate by parsed value", () => {
    const result = validateAlertDraft(["5", "7", "5,00"], limits);

    expect(result.errors).toEqual([null, null, "Duplicate"]);
    expect(result.valid).toBe(false);
  });

  it("reports the reason of every invalid row and treats a blank row as invalid", () => {
    const result = validateAlertDraft(["5", "", "abc", "0"], limits);

    expect(result.errors).toEqual([null, "Required", "Number", "Min"]);
    expect(result.valid).toBe(false);
  });

  it("flags more rows than the limit", () => {
    const rows = Array.from({ length: 11 }, (_, index) => String(index + 1));

    const result = validateAlertDraft(rows, limits);

    expect(result.overLimit).toBe(true);
    expect(result.valid).toBe(false);
    expect(validateAlertDraft(rows.slice(0, 10), limits).overLimit).toBe(false);
  });
});

describe("normalizeAlertAmounts", () => {
  it("returns the parsed amounts ascending and leaves out unparseable rows", () => {
    expect(normalizeAlertAmounts(["10", "2,5", "", "abc", "5"])).toEqual([2.5, 5, 10]);
  });
});

describe("isAlertDraftDirty", () => {
  const pristine = { rows: ["5", "10"], discordEnabled: true, emailEnabled: false };

  it("is not dirty for the saved values, in any order or notation", () => {
    expect(isAlertDraftDirty(pristine, saved)).toBe(false);
    expect(isAlertDraftDirty({ ...pristine, rows: ["10", "5"] }, saved)).toBe(false);
    expect(isAlertDraftDirty({ ...pristine, rows: ["5,00", "10.0"] }, saved)).toBe(false);
  });

  it("is dirty when an amount, the amount count or a channel flag differs", () => {
    expect(isAlertDraftDirty({ ...pristine, rows: ["5", "11"] }, saved)).toBe(true);
    expect(isAlertDraftDirty({ ...pristine, rows: ["5"] }, saved)).toBe(true);
    expect(isAlertDraftDirty({ ...pristine, rows: ["5", "10", "20"] }, saved)).toBe(true);
    expect(isAlertDraftDirty({ ...pristine, discordEnabled: false }, saved)).toBe(true);
    expect(isAlertDraftDirty({ ...pristine, emailEnabled: true }, saved)).toBe(true);
  });

  it("ignores a blank new row but counts a row that holds a value", () => {
    expect(isAlertDraftDirty({ ...pristine, rows: ["5", "10", ""] }, saved)).toBe(false);
    expect(isAlertDraftDirty({ ...pristine, rows: ["5", "10", "  "] }, saved)).toBe(false);
    expect(isAlertDraftDirty({ ...pristine, rows: ["5", "10", "abc"] }, saved)).toBe(true);
    expect(isAlertDraftDirty({ ...pristine, rows: ["5", "10", "10"] }, saved)).toBe(true);
  });

  it("compares an empty saved list with an empty draft as clean", () => {
    const empty = { thresholds: [], discordEnabled: false, emailEnabled: false };

    expect(isAlertDraftDirty({ rows: [], discordEnabled: false, emailEnabled: false }, empty)).toBe(
      false,
    );
    expect(
      isAlertDraftDirty({ rows: ["1"], discordEnabled: false, emailEnabled: false }, empty),
    ).toBe(true);
  });
});

describe("formatAlertAmount", () => {
  it("uses the decimal separator of the language", () => {
    expect(formatAlertAmount(12.5, ".")).toBe("12.5");
    expect(formatAlertAmount(12.5, ",")).toBe("12,5");
    expect(formatAlertAmount(5, ",")).toBe("5");
  });
});

describe("channelStatus", () => {
  it("marks the in-app channel as always on, whatever the availability", () => {
    expect(channelStatus("inApp")).toEqual({
      tone: "success",
      key: "Server_Alerts_Channel_Status_AlwaysOn",
    });
    expect(channelStatus("inApp", "NotLinked").key).toBe("Server_Alerts_Channel_Status_AlwaysOn");
  });

  it.each([
    ["discord", "Deliverable", "success", "Server_Alerts_Channel_Status_Ready"],
    ["discord", "NotLinked", "warning", "Server_Alerts_Channel_Status_NotLinked"],
    ["discord", "ChannelUnavailable", "neutral", "Server_Alerts_Channel_Status_Unavailable"],
    ["email", "Deliverable", "success", "Server_Alerts_Channel_Status_Ready"],
    ["email", "NoAddress", "warning", "Server_Alerts_Channel_Status_NoAddress"],
    ["email", "ChannelUnavailable", "neutral", "Server_Alerts_Channel_Status_Unavailable"],
  ] as const)("maps %s %s to a %s badge", (channel, availability, tone, key) => {
    expect(channelStatus(channel, availability)).toEqual({ tone, key });
  });

  it("falls back to the neutral not available badge for an unknown or missing availability", () => {
    const fallback = { tone: "neutral", key: "Server_Alerts_Channel_Status_Unavailable" };

    expect(channelStatus("email", "SomethingNew" as never)).toEqual(fallback);
    expect(channelStatus("discord")).toEqual(fallback);
  });
});
