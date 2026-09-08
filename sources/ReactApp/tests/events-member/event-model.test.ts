import type { FullEvent, FullUser } from "@/api/types";
import {
  attendanceChangeNeedsSlotLossConfirmation,
  canSelfAssignSlot,
  currentUserSlot,
  eventAttendanceProgress,
  eventHasStarted,
  extractBriefingObjectives,
  formatEventTimeRemaining,
  groupEventSlots,
  isEventId,
  modPackDownloadPath,
  safeDownloadFileName,
  slotRowAction,
  slotSideKey,
} from "@/features/events-member/eventModel";
import type { EventSlot, EventSlotRow } from "@/features/events-member/types";

const eventId = "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f";
const userId = "34b29531-739a-41eb-983c-49c105f3475e";

function slot(overrides: Partial<EventSlot> = {}): EventSlot {
  return {
    slotNumber: 1,
    assignedToFk: null,
    eventFk: eventId,
    isSelfAssignable: true,
    isVisible: true,
    group: "Alpha",
    title: "Rifleman",
    side: "West",
    ...overrides,
  };
}

function operation(
  overrides: { [Key in keyof FullEvent]?: FullEvent[Key] | undefined } = {},
): FullEvent {
  return {
    primaryKey: eventId,
    title: "Operation Test",
    description: "Briefing",
    terrainPrimaryKey: 1,
    terrainTitle: "Altis",
    terrainImageAssetFk: "9ed9745b-0b38-4dce-a59f-c7674ed38d8d",
    terrainImagePath: "",
    terrainIsActive: true,
    modPackRevisionPrimaryKey: 24,
    modPackRevisionTimeStampCreated: "2026-08-01T00:00:00Z",
    modPackRevisionHtml: "<html></html>",
    modPackRevisionUpdatedBy: null,
    modPackRevisionIsActive: true,
    modPackRevisionTimeStampDownloaded: null,
    modPackDefinitionPrimaryKey: 7,
    modPackDefinitionTimeStampCreated: "2026-08-01T00:00:00Z",
    modPackDefinitionTitle: "UTF Main",
    modPackDefinitionOwner: null,
    modPackDefinitionIsActive: true,
    modPackDefinitionIsComposition: false,
    imageAssetFk: "9ed9745b-0b38-4dce-a59f-c7674ed38d8d",
    imagePath: "",
    scheduledForOriginal: "2030-08-13T18:00:00Z",
    scheduledFor: "2030-08-13T18:00:00Z",
    timeStampCreated: "2026-08-01T00:00:00Z",
    isVisible: true,
    acceptedCount: 3,
    rejectedCount: 1,
    maybeCount: 2,
    minimumAccepted: 10,
    owner: { primaryKey: userId, nickname: "Owner" },
    hostedBy: { primaryKey: userId, nickname: "Host" },
    ...overrides,
  } as FullEvent;
}

function user(overrides: Partial<FullUser> = {}): FullUser {
  return {
    primaryKey: userId,
    nickname: "Player",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims: [],
    roles: [],
    ...overrides,
  };
}

describe("event detail parity model", () => {
  it("validates event ids and groups duplicate roles without losing slot order", () => {
    expect(isEventId(eventId)).toBe(true);
    expect(isEventId("00000000-0000-0000-0000-000000000000")).toBe(true);
    expect(isEventId("not-an-event")).toBe(false);

    const sides = groupEventSlots(
      [
        slot({ slotNumber: 3, title: "Medic" }),
        slot({ slotNumber: 2 }),
        slot({ slotNumber: 1, assignedToFk: userId }),
        slot({ slotNumber: 4, group: "" }),
      ],
      "Ungrouped",
      "Unassigned role",
    );

    expect(sides.map((side) => side.id)).toEqual(["west"]);
    expect(sides[0]?.groups.map((group) => group.name)).toEqual(["Alpha", "Ungrouped"]);
    expect(sides[0]?.groups[0]?.rows[0]?.slots.map((item) => item.slotNumber)).toEqual([1, 2]);
    expect(sides[0]).toMatchObject({ assignedCount: 1, totalCount: 4 });
    expect(sides[0]?.groups[0]).toMatchObject({ assignedCount: 1, totalCount: 3 });
  });

  it("keeps identically named groups separated by side", () => {
    const sides = groupEventSlots(
      [
        slot({ slotNumber: 4, group: "Alpha", title: "Rifleman", side: "East" }),
        slot({ slotNumber: 1, group: "Alpha", title: "Team Lead", side: "West" }),
        slot({ slotNumber: 2, group: "Alpha", title: "Rifleman", side: "West" }),
        slot({ slotNumber: 3, group: "Alpha", title: "Medic", side: "East" }),
      ],
      "Ungrouped",
      "Unassigned role",
    );

    expect(sides.map((side) => side.id)).toEqual(["west", "east"]);
    expect(sides.map((side) => side.groups.map((group) => group.name))).toEqual([
      ["Alpha"],
      ["Alpha"],
    ]);
    expect(sides[0]?.groups[0]?.rows.map((row) => row.title)).toEqual(["Team Lead", "Rifleman"]);
    expect(sides[1]?.groups[0]?.rows.map((row) => row.title)).toEqual(["Medic", "Rifleman"]);
  });

  it("permits only authenticated users to claim a visible, open, self-assignable future slot", () => {
    const futureEvent = operation();
    expect(canSelfAssignSlot(slot(), user(), futureEvent, false)).toBe(true);
    expect(canSelfAssignSlot(slot({ isVisible: false }), user(), futureEvent, false)).toBe(false);
    expect(canSelfAssignSlot(slot({ isSelfAssignable: false }), user(), futureEvent, false)).toBe(
      false,
    );
    expect(canSelfAssignSlot(slot({ assignedToFk: userId }), user(), futureEvent, false)).toBe(
      false,
    );
    expect(canSelfAssignSlot(slot(), undefined, futureEvent, false)).toBe(false);
    expect(canSelfAssignSlot(slot(), user(), futureEvent, true)).toBe(false);
    expect(
      canSelfAssignSlot(slot(), user(), operation({ scheduledFor: "2020-01-01T00:00:00Z" }), false),
    ).toBe(false);
    expect(eventHasStarted(futureEvent, new Date("2029-01-01T00:00:00Z"))).toBe(false);
  });

  it("preserves the classic default-visible and default-self-assignable slot semantics", () => {
    const legacySlot = slot();
    delete (legacySlot as Partial<EventSlot>).isVisible;
    delete (legacySlot as Partial<EventSlot>).isSelfAssignable;

    expect(canSelfAssignSlot(legacySlot, user(), operation(), false)).toBe(true);
    expect(
      groupEventSlots([legacySlot], "Ungrouped", "Unassigned")[0]?.groups[0]?.rows[0]?.openCount,
    ).toBe(1);
  });

  it("never turns the current-slot row into a self-unassign action", () => {
    const assignedSlot = slot({ assignedToFk: userId });
    const row: EventSlotRow = {
      key: "Alpha|West|Rifleman",
      group: "Alpha",
      title: "Rifleman",
      side: "West",
      slots: [assignedSlot],
      assignedCount: 1,
      openCount: 0,
    };

    expect(slotRowAction(row, user(), operation())).toEqual({ kind: "current" });
    expect(slotRowAction(row, user(), operation({ scheduledFor: "2020-01-01T00:00:00Z" }))).toEqual(
      { kind: "current" },
    );
    expect(attendanceChangeNeedsSlotLossConfirmation(assignedSlot, "maybe")).toBe(true);
    expect(attendanceChangeNeedsSlotLossConfirmation(assignedSlot, "reject")).toBe(true);
    expect(attendanceChangeNeedsSlotLossConfirmation(assignedSlot, "accept")).toBe(false);
  });

  it("distinguishes closed, hidden, full, locked, and assignable rows", () => {
    const row = (value: EventSlot): EventSlotRow => ({
      key: "row",
      group: "Alpha",
      title: "Rifleman",
      side: "West",
      slots: [value],
      assignedCount: value.assignedToFk ? 1 : 0,
      openCount: value.assignedToFk == null && value.isVisible !== false ? 1 : 0,
    });

    expect(slotRowAction(row(slot({ isVisible: false })), user(), operation()).kind).toBe("hidden");
    expect(
      slotRowAction(row(slot()), user(), operation({ scheduledFor: "2020-01-01T00:00:00Z" })).kind,
    ).toBe("closed");
    expect(slotRowAction(row(slot()), undefined, operation()).kind).toBe("sign-in");
    expect(
      slotRowAction(row(slot({ assignedToFk: "another-user" })), user(), operation()).kind,
    ).toBe("assigned");
    expect(slotRowAction(row(slot({ isSelfAssignable: false })), user(), operation()).kind).toBe(
      "locked",
    );
    expect(slotRowAction(row(slot()), user(), operation()).kind).toBe("assign");
  });

  it("reports viability and deterministic time remaining", () => {
    expect(eventAttendanceProgress(operation({ acceptedCount: 3, minimumAccepted: 12 }))).toEqual({
      accepted: 3,
      minimum: 12,
      percentage: 25,
      needsMoreSignups: true,
    });
    expect(eventAttendanceProgress(operation({ acceptedCount: 15, minimumAccepted: 12 }))).toEqual({
      accepted: 15,
      minimum: 12,
      percentage: 100,
      needsMoreSignups: false,
    });
    expect(formatEventTimeRemaining("2030-01-03T05:30:00Z", new Date("2030-01-01T01:00:00Z"))).toBe(
      "2d 4h",
    );
    expect(formatEventTimeRemaining("2030-01-01T02:31:00Z", new Date("2030-01-01T01:00:00Z"))).toBe(
      "1h 31m",
    );
    expect(formatEventTimeRemaining("2030-01-01T00:00:00Z", new Date("2030-01-01T01:00:00Z"))).toBe(
      "started",
    );
  });

  it("finds the current slot and chooses revision-specific or latest mod-pack downloads", () => {
    expect(
      currentUserSlot([slot(), slot({ slotNumber: 2, assignedToFk: userId })], user()),
    ).toMatchObject({ slotNumber: 2 });
    expect(modPackDownloadPath(operation())).toBe("/mod-packs/7/download/24");
    const eventWithoutRevision = operation();
    delete (eventWithoutRevision as Partial<FullEvent>).modPackRevisionPrimaryKey;
    expect(modPackDownloadPath(eventWithoutRevision)).toBe("/mod-packs/7/download/latest");
    const eventWithoutModPack = operation();
    delete (eventWithoutModPack as Partial<FullEvent>).modPackDefinitionPrimaryKey;
    expect(modPackDownloadPath(eventWithoutModPack)).toBeUndefined();
    expect(safeDownloadFileName("UTF Main: A/B + 3")).toBe("UTFMainAB+3.html");
  });

  it("maps numeric and string sides to localizable resource keys", () => {
    expect(slotSideKey("West")).toBe("West");
    expect(slotSideKey(1)).toBe("East");
    expect(slotSideKey("AmbientLife")).toBe("Ambient");
    expect(slotSideKey("Empty")).toBeUndefined();
  });

  it("extracts concise briefing objectives without leaking markdown decoration", () => {
    expect(
      extractBriefingObjectives(`
# Mission briefing

## Objectives
- :objective: **Secure** the [radio tower](https://example.test/map).
- :medical: Establish a casualty collection point.
- :radio: Report extraction readiness on \`long-range\`.

| Unit | Callsign |
| --- | --- |
| Alpha | One |
`),
    ).toEqual([
      { icon: "objective", text: "Secure the radio tower." },
      { icon: "medical", text: "Establish a casualty collection point." },
      { icon: "radio", text: "Report extraction readiness on long-range." },
    ]);
  });

  it("falls back to briefing sentences when prose has no objective list", () => {
    expect(
      extractBriefingObjectives(
        "Recon the western approach. Avoid contact until the support element is ready! Extract before dawn.",
      ),
    ).toEqual([
      { icon: "objective", text: "Recon the western approach." },
      { icon: "warning", text: "Avoid contact until the support element is ready!" },
      { icon: "radio", text: "Extract before dawn." },
    ]);
  });
});
