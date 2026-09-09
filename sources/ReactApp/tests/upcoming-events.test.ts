import type { UpcomingEvent } from "@/api/types";
import {
  eventAttendance,
  eventReadiness,
  eventSlotProgress,
  formatEventSlotCount,
  matchesUpcomingEventFilter,
  normalizeEventAcceptance,
  utcDateKey,
} from "@/app/upcomingEvents";

function event(metaAcceptance: UpcomingEvent["metaAcceptance"]): UpcomingEvent {
  return {
    primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f",
    title: "Operation",
    description: "Briefing",
    terrainTitle: "Altis",
    modPackRevisionTimeStampDownloaded: null,
    modPackDefinitionTitle: "UTF",
    imageAssetFk: "00000000-0000-0000-0000-000000000000",
    imagePath: "",
    scheduledForOriginal: "2026-08-13T18:00:00Z",
    scheduledFor: "2026-08-13T18:00:00Z",
    timeStampCreated: "2026-08-01T00:00:00Z",
    acceptedCount: 18,
    rejectedCount: 2,
    maybeCount: 3,
    minimumAccepted: 12,
    hostedBy: { primaryKey: "00000000-0000-0000-0000-000000000001", nickname: "Host" },
    metaAcceptance,
  };
}

describe("upcoming event parity", () => {
  it("normalizes string and legacy numeric acceptance values", () => {
    expect(normalizeEventAcceptance(-1)).toBe("Rejected");
    expect(normalizeEventAcceptance(0)).toBe("Maybe");
    expect(normalizeEventAcceptance(1)).toBe("Accepted");
    expect(normalizeEventAcceptance("Accepted")).toBe("Accepted");
  });

  it("matches the classic confirmed and open filters", () => {
    expect(matchesUpcomingEventFilter(event(1), "confirmed")).toBe(true);
    expect(matchesUpcomingEventFilter(event(0), "confirmed")).toBe(false);
    expect(matchesUpcomingEventFilter(event(-1), "open")).toBe(false);
    expect(matchesUpcomingEventFilter(event(null), "open")).toBe(true);
  });

  it("keeps the configured minimum visible when attendance exceeds it", () => {
    expect(formatEventSlotCount(event("Accepted"))).toBe("18 / 12");
  });

  it("keeps operational readiness separate from the current user's attendance", () => {
    const lowAttendance = event("Accepted");
    lowAttendance.acceptedCount = 4;
    lowAttendance.minimumAccepted = 12;

    expect(eventReadiness(lowAttendance)).toBe("needs-signups");
    expect(eventAttendance(lowAttendance)).toBe("Accepted");
    expect(eventSlotProgress(lowAttendance)).toBeCloseTo(33.333, 2);

    const unassigned = event(null);
    expect(eventReadiness(unassigned)).toBe("ready");
    expect(eventAttendance(unassigned)).toBe("Unassigned");
  });

  it("derives calendar dates in UTC", () => {
    expect(utcDateKey("2026-08-13T23:30:00-03:00")).toBe("2026-08-14");
  });
});
