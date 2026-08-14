import { describe, expect, it } from "vitest";
import {
  eventAdminState,
  eventMatchesFilter,
  eventMatchesSearch,
  normalizeEventAdminSnapshot,
  slotReadinessPercentage,
  sortEventAdminRows,
} from "@/features/admin/eventAdmin";

const now = Date.parse("2026-08-13T12:00:00Z");

describe("event administration model", () => {
  it("normalizes incomplete snapshots and drops rows without an event", () => {
    expect(
      normalizeEventAdminSnapshot({
        eventCount: 2,
        events: [{}, { plainEvent: { primaryKey: "event-1", title: "Operation One" } }],
      }),
    ).toMatchObject({
      eventCount: 2,
      events: [{ plainEvent: { primaryKey: "event-1" } }],
      upcomingEvents: [],
    });
  });

  it("preserves classic state precedence and readiness calculation", () => {
    expect(
      eventAdminState(
        {
          isVisible: false,
          scheduledFor: "2026-08-10T12:00:00Z",
          acceptedCount: 1,
          minimumAccepted: 10,
        },
        now,
      ),
    ).toBe("hidden");
    expect(
      eventAdminState(
        { scheduledFor: "2026-08-10T12:00:00Z", acceptedCount: 1, minimumAccepted: 10 },
        now,
      ),
    ).toBe("archived");
    expect(
      eventAdminState(
        { scheduledFor: "2026-08-14T12:00:00Z", acceptedCount: 4, minimumAccepted: 10 },
        now,
      ),
    ).toBe("low-slots");
    expect(slotReadinessPercentage({ acceptedCount: 12, minimumAccepted: 10 })).toBe(100);
    expect(slotReadinessPercentage({ acceptedCount: 3, minimumAccepted: 12 })).toBe(25);
  });

  it("filters, searches, and sorts the returned administration window", () => {
    const rows = normalizeEventAdminSnapshot({
      events: [
        {
          plainEvent: {
            primaryKey: "past",
            title: "Past patrol",
            scheduledFor: "2026-08-12T12:00:00Z",
          },
        },
        {
          plainEvent: {
            primaryKey: "future-b",
            title: "Operation Bravo",
            scheduledFor: "2026-08-15T12:00:00Z",
          },
          hostedBy: { nickname: "Raven" },
          terrain: { title: "Altis" },
        },
        {
          plainEvent: {
            primaryKey: "future-a",
            title: "Operation Alpha",
            scheduledFor: "2026-08-14T12:00:00Z",
          },
        },
      ],
    }).events;

    expect(eventMatchesFilter(rows[1]!.plainEvent, "upcoming", now)).toBe(true);
    expect(eventMatchesFilter(rows[0]!.plainEvent, "archived", now)).toBe(true);
    expect(eventMatchesSearch(rows[1]!, "raven", "Ready")).toBe(true);
    expect(eventMatchesSearch(rows[1]!, "altis", "Ready")).toBe(true);
    expect(sortEventAdminRows(rows, now).map((row) => row.plainEvent.primaryKey)).toEqual([
      "future-a",
      "future-b",
      "past",
    ]);
  });
});
