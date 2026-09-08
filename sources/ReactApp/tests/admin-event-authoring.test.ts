import { ApiClient, initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import {
  applyEventObjectives,
  createEventForm,
  createEventFormSchema,
  createSlotDraft,
  defaultScheduledForLocal,
  eventAuthoringService,
  isGuid,
  normalizeEventVisibility,
  normalizeArmaSide,
  parseEventObjectives,
  removeObjectiveBlock,
  validateSlot,
} from "@/features/admin/eventAuthoring";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

describe("event authoring model", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("chooses the next local 20:00 operation window", () => {
    expect(defaultScheduledForLocal(new Date(2026, 7, 14, 10, 30))).toBe("2026-08-14T20:00");
    expect(defaultScheduledForLocal(new Date(2026, 7, 14, 21, 30))).toBe("2026-08-15T20:00");
    expect(createEventForm(new Date(2026, 7, 14, 10, 30))).toMatchObject({
      scheduledForLocal: "2026-08-14T20:00",
      isVisible: true,
      minimumAccepted: 0,
    });
  });

  it("preserves public visibility for typed defaults and native select values", () => {
    expect(normalizeEventVisibility(true)).toBe(true);
    expect(normalizeEventVisibility("true")).toBe(true);
    expect(normalizeEventVisibility(false)).toBe(false);
    expect(normalizeEventVisibility("false")).toBe(false);
  });

  it("accepts canonical .NET Guid event identifiers, including UUIDv7", () => {
    expect(isGuid("01a0102f-3f75-7c4a-bd99-ed6273e83565")).toBe(true);
    expect(isGuid("83D25DDD-9FF5-4D09-99AB-4F49D91A7C3F")).toBe(true);
    expect(isGuid("01a0102f-3f75-7c4a-bd99-ed6273e8356")).toBe(false);
    expect(isGuid("01a0102f-3f75-xc4a-bd99-ed6273e83565")).toBe(false);
  });

  it("validates core event fields while allowing optional assets and slots", () => {
    const schema = createEventFormSchema({
      titleRequired: "title",
      titleTooLong: "long",
      briefingRequired: "briefing",
      scheduleRequired: "schedule",
      hostRequired: "host",
      terrainRequired: "terrain",
      modPackRequired: "modpack",
      imageRequired: "image",
      minimumInvalid: "minimum",
    });
    const result = schema.safeParse({
      ...createEventForm(new Date(2026, 7, 14, 10, 30)),
      scheduledForLocal: "",
      minimumAccepted: -1,
    });
    expect(result.success).toBe(false);
    expect(result.error?.issues.map((issue) => issue.message)).toEqual(
      expect.arrayContaining(["title", "briefing", "schedule", "host", "minimum"]),
    );
    expect(result.error?.issues.map((issue) => issue.message)).not.toEqual(
      expect.arrayContaining(["terrain", "modpack", "image"]),
    );

    expect(
      schema.safeParse({
        ...createEventForm(new Date(2026, 7, 14, 10, 30)),
        title: "Asset-free operation",
        description: "Briefing",
        hostedByFk: "2fdd4802-e081-4baa-9120-f1873653a057",
      }).success,
    ).toBe(true);

    const slot = { ...createSlotDraft(), group: " ", title: "" };
    expect(validateSlot(slot, { groupRequired: "group", titleRequired: "role" })).toEqual([
      "group",
      "role",
    ]);
  });

  it("parses and rewrites the leading mission-objective block without losing briefing content", () => {
    const markdown =
      "## Mission objectives\n- :intel: Locate the convoy\n- :medical: Stabilize casualties\n\n## Execution\nMove at dusk.";
    const objectives = parseEventObjectives(markdown);
    expect(objectives.map(({ icon, text }) => ({ icon, text }))).toEqual([
      { icon: "intel", text: "Locate the convoy" },
      { icon: "medical", text: "Stabilize casualties" },
    ]);
    expect(removeObjectiveBlock(markdown)).toBe("## Execution\nMove at dusk.");
    expect(
      applyEventObjectives(markdown, [
        { ...objectives[0]!, icon: "map", text: "Secure the bridge" },
      ]),
    ).toBe("## Mission objectives\n- :map: Secure the bridge\n\n## Execution\nMove at dusk.");

    expect(removeObjectiveBlock("## Mission objectives\nBriefing prose starts immediately.")).toBe(
      "Briefing prose starts immediately.",
    );
    expect(
      removeObjectiveBlock(
        "## Mission objectives\n- :intel: Locate the convoy\n## Execution\nMove at dusk.",
      ),
    ).toBe("## Execution\nMove at dusk.");
  });

  it("normalizes legacy numeric and string Arma sides", () => {
    expect(normalizeArmaSide(2)).toBe("West");
    expect(normalizeArmaSide(5)).toBe("AmbientLife");
    expect(normalizeArmaSide("Resistance")).toBe("Resistance");
    expect(normalizeArmaSide(99)).toBe("Empty");
  });

  it("sends typed event and slot mutations through the shared adapter", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const values = {
      ...createEventForm(new Date(2026, 7, 14, 10, 30)),
      title: "Operation Northstar",
      description: "Briefing",
      hostedByFk: "2fdd4802-e081-4baa-9120-f1873653a057",
      terrainFk: 3,
      terrainGameIdentifier: "arma3",
      modPackRevisionFk: 8,
      imageAssetFk: "4a7e2d42-50b0-4a7d-a381-a4f20a1f5dc2",
    };
    await expect(eventAuthoringService.create(values)).resolves.toBe(
      "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f",
    );
    await eventAuthoringService.update("83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f", values);

    const [createUrl, createInit] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(createUrl.pathname).toBe("/api/events/create");
    expect(createInit.method).toBe("POST");
    const createPayload = JSON.parse(String(createInit.body)) as { scheduledFor: string };
    expect(createPayload).toMatchObject({
      title: "Operation Northstar",
      terrainFk: 3,
      modPackRevisionFk: 8,
      imageAssetFk: "4a7e2d42-50b0-4a7d-a381-a4f20a1f5dc2",
      isVisible: true,
    });
    expect(createPayload.scheduledFor).toBe(new Date(values.scheduledForLocal).toISOString());
    expect(createPayload.scheduledFor.endsWith("Z")).toBe(true);
    const [updateUrl, updateInit] = fetchMock.mock.calls[1] as [URL, RequestInit];
    expect(updateUrl.pathname).toBe("/api/events/83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f/update");
    expect((JSON.parse(String(updateInit.body)) as { scheduledFor: string }).scheduledFor).toBe(
      new Date(values.scheduledForLocal).toISOString(),
    );
    expect(new ApiClient(configuration)).toBeInstanceOf(ApiClient);
  });

  it("sends hidden only when an author explicitly selects it", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await eventAuthoringService.create({
      ...createEventForm(new Date(2026, 7, 14, 10, 30)),
      title: "Hidden operation",
      description: "Briefing",
      hostedByFk: "2fdd4802-e081-4baa-9120-f1873653a057",
      isVisible: normalizeEventVisibility("false"),
    });

    expect(JSON.parse(String((fetchMock.mock.calls[0]?.[1] as RequestInit).body))).toMatchObject({
      isVisible: false,
    });
  });

  it("serializes absent assets as null and explicit update clears", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ primaryKey: "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);
    const values = {
      ...createEventForm(new Date(2026, 7, 14, 10, 30)),
      title: "Asset-free operation",
      description: "Briefing",
      hostedByFk: "2fdd4802-e081-4baa-9120-f1873653a057",
    };

    await eventAuthoringService.create(values);
    await eventAuthoringService.update("83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f", values);

    expect(JSON.parse(String((fetchMock.mock.calls[0]?.[1] as RequestInit).body))).toMatchObject({
      terrainFk: null,
      modPackRevisionFk: null,
      imageAssetFk: null,
    });
    expect(JSON.parse(String((fetchMock.mock.calls[1]?.[1] as RequestInit).body))).toMatchObject({
      terrainFk: null,
      clearTerrain: true,
      modPackRevisionFk: null,
      clearModPackRevision: true,
      imageAssetFk: null,
      clearImageAsset: true,
    });
  });
});
