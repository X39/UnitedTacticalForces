import de from "@/locales/de.json";
import en from "@/locales/en.json";

const english: Record<string, string> = en;
const german: Record<string, string> = de;

/** Locale keys whose values must stay distinct because the UI shows both for different attendee kinds. */
const teamKey = "Pages_Events_Detail_Status_ConfirmedTeam";
const memberKey = "Pages_Events_Detail_Status_ConfirmedMember";

describe("distinct event status wording", () => {
  it("names a confirmed team differently from a confirmed member in English", () => {
    expect(english[teamKey]).toBe("Confirmed team");
    expect(english[teamKey]).not.toBe(english[memberKey]);
  });

  it("names a confirmed team differently from a confirmed member in German", () => {
    expect(german[teamKey]).toBe("Bestätigtes Team");
    expect(german[teamKey]).not.toBe(german[memberKey]);
  });
});
