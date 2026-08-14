import { z } from "zod";
import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type { components } from "@/generated/api";

type Schema<Name extends keyof components["schemas"]> = components["schemas"][Name];
type EventCreationPayload = Schema<"EventCreationPayload">;
type EventUpdate = Schema<"EventUpdate">;
type EventSlotCreationPayload = Schema<"EventSlotCreationPayload">;
type EventSlotUpdate = Schema<"EventSlotUpdate">;
type FullEventDto = Schema<"FullEventDto">;
type PlainEventDto = Schema<"PlainEventDto">;
type PlainEventSlotDto = Schema<"PlainEventSlotDto">;
type FullUserDto = Schema<"FullUserDto">;
type UserDto = Schema<"UserDto">;

export type ArmaSide = Schema<"EArmaSide">;

export interface EventPerson {
  primaryKey: string;
  nickname: string;
  avatar: string;
  avatarMimeType: string;
}

export interface EventAuthoringRecord {
  primaryKey: string;
  title: string;
  description: string;
  terrainPrimaryKey: number | null;
  terrainTitle: string;
  terrainGameIdentifier: string;
  modPackRevisionPrimaryKey: number | null;
  modPackDefinitionTitle: string;
  imageAssetFk: string;
  imagePath: string;
  scheduledForOriginal: string;
  scheduledFor: string;
  timeStampCreated: string;
  isVisible: boolean;
  acceptedCount: number;
  rejectedCount: number;
  maybeCount: number;
  minimumAccepted: number;
  owner: EventPerson | null;
  hostedBy: EventPerson | null;
}

export interface EventFormValues {
  title: string;
  description: string;
  scheduledForLocal: string;
  hostedByFk: string;
  terrainFk: number | null;
  terrainGameIdentifier: string;
  modPackRevisionFk: number | null;
  imageAssetFk: string;
  imagePath: string;
  minimumAccepted: number;
  isVisible: boolean;
}

export interface EventSlotDraft {
  draftKey: string;
  slotNumber: number | null;
  assignedToFk: string;
  originalAssignedToFk: string;
  assignedUser: EventPerson | null;
  assignmentCandidate: EventPerson | null;
  group: string;
  title: string;
  side: ArmaSide;
  isSelfAssignable: boolean;
  isVisible: boolean;
  isDeleted: boolean;
}

export interface EventObjectiveDraft {
  draftKey: string;
  icon: string;
  text: string;
}

export interface EventAuthoringSnapshot {
  event: EventAuthoringRecord;
  slots: EventSlotDraft[];
}

export interface EventValidationMessages {
  titleRequired: string;
  titleTooLong: string;
  briefingRequired: string;
  scheduleRequired: string;
  hostRequired: string;
  terrainRequired: string;
  modPackRequired: string;
  imageRequired: string;
  minimumInvalid: string;
}

export interface SlotValidationMessages {
  groupRequired: string;
  titleRequired: string;
}

const sideNames: readonly ArmaSide[] = [
  "Empty",
  "East",
  "West",
  "Resistance",
  "Civilian",
  "AmbientLife",
  "Logic",
];

const objectiveIcons = [
  "objective",
  "intel",
  "map",
  "rally",
  "radio",
  "warning",
  "stealth",
  "support",
  "medical",
  "extract",
  "host",
  "time",
  "modpack",
  "slot",
  "document",
] as const;

export const eventObjectiveIcons: readonly string[] = objectiveIcons;

const objectiveHeading = /^\s{0,3}#{1,6}\s+mission objectives\s*$/i;
const objectiveLine = /^\s*(?:[-*+]\s+|\d+[.)]\s+)?(?::(?<icon>[a-z0-9-]+):\s*)?(?<text>.+?)\s*$/i;
const markdownLink = /\[(?<text>[^\]]+)]\([^)]*\)/g;
const markdownFormatting = /[*_~>#`]/g;

export const eventAuthoringQueryKeys = {
  event: (eventId: string) => ["event-authoring", eventId] as const,
  slots: (eventId: string) => ["event-authoring", eventId, "slots"] as const,
  users: (search: string) => ["event-authoring", "users", search] as const,
};

export function isGuid(value: string | undefined): value is string {
  return Boolean(
    value &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value),
  );
}

export function createEventFormSchema(messages: EventValidationMessages, requireImage: boolean) {
  const schema = z.object({
    title: z.string().trim().min(1, messages.titleRequired).max(1024, messages.titleTooLong),
    description: z.string().trim().min(1, messages.briefingRequired),
    scheduledForLocal: z.string().min(1, messages.scheduleRequired),
    hostedByFk: z.string().uuid(messages.hostRequired),
    terrainFk: z.number().int().positive().nullable(),
    terrainGameIdentifier: z.string(),
    modPackRevisionFk: z.number().int().positive().nullable(),
    imageAssetFk: z.string(),
    imagePath: z.string(),
    minimumAccepted: z.number().int().min(0, messages.minimumInvalid),
    isVisible: z.boolean(),
  });
  return schema.superRefine((value, context) => {
    if (value.terrainFk == null)
      context.addIssue({ code: "custom", path: ["terrainFk"], message: messages.terrainRequired });
    if (value.modPackRevisionFk == null)
      context.addIssue({
        code: "custom",
        path: ["modPackRevisionFk"],
        message: messages.modPackRequired,
      });
    if (requireImage && !value.imageAssetFk)
      context.addIssue({
        code: "custom",
        path: ["imageAssetFk"],
        message: messages.imageRequired,
      });
  });
}

export function validateSlot(slot: EventSlotDraft, messages: SlotValidationMessages): string[] {
  const errors: string[] = [];
  if (!slot.group.trim()) errors.push(messages.groupRequired);
  if (!slot.title.trim()) errors.push(messages.titleRequired);
  return errors;
}

export function defaultScheduledForLocal(now = new Date()): string {
  const scheduled = new Date(now);
  scheduled.setHours(20, 0, 0, 0);
  if (scheduled <= now) scheduled.setDate(scheduled.getDate() + 1);
  return toLocalDateTimeInput(scheduled.toISOString());
}

export function createEventForm(now = new Date()): EventFormValues {
  return {
    title: "",
    description: "",
    scheduledForLocal: defaultScheduledForLocal(now),
    hostedByFk: "",
    terrainFk: null,
    terrainGameIdentifier: "",
    modPackRevisionFk: null,
    imageAssetFk: "",
    imagePath: "",
    minimumAccepted: 0,
    isVisible: true,
  };
}

export function eventToForm(event: EventAuthoringRecord): EventFormValues {
  return {
    title: event.title,
    description: event.description,
    scheduledForLocal: toLocalDateTimeInput(event.scheduledFor),
    hostedByFk: event.hostedBy?.primaryKey ?? "",
    terrainFk: event.terrainPrimaryKey,
    terrainGameIdentifier: event.terrainGameIdentifier,
    modPackRevisionFk: event.modPackRevisionPrimaryKey,
    imageAssetFk: event.imageAssetFk,
    imagePath: event.imagePath,
    minimumAccepted: event.minimumAccepted,
    isVisible: event.isVisible,
  };
}

export function createSlotDraft(): EventSlotDraft {
  return {
    draftKey: crypto.randomUUID(),
    slotNumber: null,
    assignedToFk: "",
    originalAssignedToFk: "",
    assignedUser: null,
    assignmentCandidate: null,
    group: "Alpha",
    title: "Rifleman",
    side: "West",
    isSelfAssignable: true,
    isVisible: true,
    isDeleted: false,
  };
}

export function normalizeArmaSide(value: ArmaSide | number | null | undefined): ArmaSide {
  if (typeof value === "number") return sideNames[value] ?? "Empty";
  if (!value) return "Empty";
  if (value === "AmbientLife") return value;
  return sideNames.includes(value) ? value : "Empty";
}

export function createObjectiveDraft(index = 0): EventObjectiveDraft {
  return {
    draftKey: crypto.randomUUID(),
    icon: objectiveIcons[index % objectiveIcons.length] ?? "objective",
    text: "",
  };
}

function normalizeObjectiveText(value: string): string {
  return value
    .replace(markdownLink, "$<text>")
    .replace(markdownFormatting, "")
    .replace(/\s+/g, " ")
    .trim();
}

function parseObjectiveLine(value: string): Pick<EventObjectiveDraft, "icon" | "text"> | undefined {
  const match = objectiveLine.exec(value.trim());
  const text = normalizeObjectiveText(match?.groups?.text ?? "");
  if (!match || !text) return undefined;
  const requestedIcon = (match.groups?.icon ?? "objective").toLocaleLowerCase();
  return {
    icon: objectiveIcons.includes(requestedIcon as (typeof objectiveIcons)[number])
      ? requestedIcon
      : "objective",
    text,
  };
}

function normalizedLines(value: string): string[] {
  return value.replace(/\r\n/g, "\n").replace(/\r/g, "\n").split("\n");
}

export function parseEventObjectives(description: string): EventObjectiveDraft[] {
  const lines = normalizedLines(description);
  const firstContent = lines.findIndex((line) => line.trim().length > 0);
  const candidates: string[] = [];
  if (firstContent >= 0 && objectiveHeading.test(lines[firstContent] ?? "")) {
    let started = false;
    for (const line of lines.slice(firstContent + 1)) {
      if (!line.trim()) {
        if (started) break;
        continue;
      }
      if (!parseObjectiveLine(line)) break;
      started = true;
      candidates.push(line);
    }
  } else {
    candidates.push(
      ...lines.filter((line) => {
        const trimmed = line.trim();
        return (
          Boolean(trimmed) &&
          !trimmed.startsWith("#") &&
          !trimmed.startsWith("```") &&
          !trimmed.startsWith("|") &&
          !trimmed.startsWith("<!--")
        );
      }),
    );
  }
  const drafts = candidates
    .map(parseObjectiveLine)
    .filter((item): item is Pick<EventObjectiveDraft, "icon" | "text"> => Boolean(item))
    .map((item) => ({ ...item, draftKey: crypto.randomUUID() }));
  return drafts.length ? drafts : [createObjectiveDraft()];
}

export function removeObjectiveBlock(description: string): string {
  const lines = normalizedLines(description);
  const firstContent = lines.findIndex((line) => line.trim().length > 0);
  if (firstContent < 0 || !objectiveHeading.test(lines[firstContent] ?? "")) return description;
  let index = firstContent + 1;
  while (index < lines.length && !lines[index]?.trim()) index += 1;
  while (index < lines.length && parseObjectiveLine(lines[index] ?? "")) index += 1;
  while (index < lines.length && !lines[index]?.trim()) index += 1;
  return lines.slice(index).join("\n").trimStart();
}

export function applyEventObjectives(
  description: string,
  objectives: readonly EventObjectiveDraft[],
): string {
  const lines = objectives
    .filter((objective) => objective.text.trim())
    .map((objective) => `- :${objective.icon}: ${objective.text.trim()}`);
  const body = removeObjectiveBlock(description).trimStart();
  if (!lines.length) return body;
  const block = `## Mission objectives\n${lines.join("\n")}`;
  return body ? `${block}\n\n${body}` : block;
}

function toLocalDateTimeInput(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) return "";
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.valueOf() - offset).toISOString().slice(0, 16);
}

function normalizePerson(
  person: Schema<"PlainUserDto"> | UserDto | FullUserDto | null | undefined,
): EventPerson | null {
  if (!person?.primaryKey) return null;
  return {
    primaryKey: person.primaryKey,
    nickname: person.nickname ?? person.primaryKey,
    avatar: person.avatar ?? "",
    avatarMimeType: person.avatarMimeType ?? "",
  };
}

function normalizeEvent(event: FullEventDto): EventAuthoringRecord | undefined {
  if (!event.primaryKey) return undefined;
  return {
    primaryKey: event.primaryKey,
    title: event.title ?? "",
    description: event.description ?? "",
    terrainPrimaryKey: event.terrainPrimaryKey ?? null,
    terrainTitle: event.terrainTitle ?? "",
    terrainGameIdentifier: "",
    modPackRevisionPrimaryKey: event.modPackRevisionPrimaryKey ?? null,
    modPackDefinitionTitle: event.modPackDefinitionTitle ?? "",
    imageAssetFk: event.imageAssetFk ?? "",
    imagePath: event.imagePath ?? "",
    scheduledForOriginal: event.scheduledForOriginal ?? "",
    scheduledFor: event.scheduledFor ?? "",
    timeStampCreated: event.timeStampCreated ?? "",
    isVisible: event.isVisible ?? true,
    acceptedCount: event.acceptedCount ?? 0,
    rejectedCount: event.rejectedCount ?? 0,
    maybeCount: event.maybeCount ?? 0,
    minimumAccepted: event.minimumAccepted ?? 0,
    owner: normalizePerson(event.owner),
    hostedBy: normalizePerson(event.hostedBy),
  };
}

function normalizeSlot(slot: PlainEventSlotDto): EventSlotDraft {
  return {
    draftKey: crypto.randomUUID(),
    slotNumber: slot.slotNumber ?? null,
    assignedToFk: slot.assignedToFk ?? "",
    originalAssignedToFk: slot.assignedToFk ?? "",
    assignedUser: null,
    assignmentCandidate: null,
    group: slot.group?.trim() || "Ungrouped",
    title: slot.title?.trim() || "Unassigned role",
    side: normalizeArmaSide(slot.side),
    isSelfAssignable: slot.isSelfAssignable ?? true,
    isVisible: slot.isVisible ?? true,
    isDeleted: false,
  };
}

function toCreationPayload(values: EventFormValues): EventCreationPayload {
  if (values.terrainFk == null || values.modPackRevisionFk == null)
    throw new Error("Terrain and mod-pack revision are required.");
  return {
    title: values.title.trim(),
    description: values.description.trim(),
    scheduledFor: new Date(values.scheduledForLocal).toISOString(),
    hostedByFk: values.hostedByFk,
    terrainFk: values.terrainFk,
    modPackRevisionFk: values.modPackRevisionFk,
    imageAssetFk: values.imageAssetFk,
    minimumAccepted: values.minimumAccepted,
    isVisible: values.isVisible,
  };
}

function toUpdatePayload(values: EventFormValues): EventUpdate {
  return toCreationPayload(values);
}

function toSlotCreationPayload(slot: EventSlotDraft): EventSlotCreationPayload {
  return {
    assignedToFk: slot.assignedToFk || null,
    group: slot.group.trim(),
    title: slot.title.trim(),
    side: slot.side,
    isSelfAssignable: slot.isSelfAssignable,
    isVisible: slot.isVisible,
  };
}

function toSlotUpdate(slot: EventSlotDraft): EventSlotUpdate {
  return {
    group: slot.group.trim(),
    title: slot.title.trim(),
    side: slot.side,
    isSelfAssignable: slot.isSelfAssignable,
    isVisible: slot.isVisible,
  };
}

async function loadPerson(userId: string, signal?: AbortSignal): Promise<EventPerson | null> {
  const person = await getApiClient().request<UserDto>(fillApiPath("/users/{userId}", { userId }), {
    signal,
    expectedStatuses: [204, 404],
  });
  return normalizePerson(person);
}

async function hydrateSlotAssignees(
  slots: EventSlotDraft[],
  signal?: AbortSignal,
): Promise<EventSlotDraft[]> {
  const assignees = new Map<string, EventPerson>();
  await Promise.all(
    [...new Set(slots.map((slot) => slot.assignedToFk).filter(Boolean))].map(async (userId) => {
      try {
        const person = await loadPerson(userId, signal);
        if (person) assignees.set(userId, person);
      } catch {
        // Slot editing remains available with an identifier fallback when a profile is unavailable.
      }
    }),
  );
  for (const slot of slots) {
    if (!slot.assignedToFk) continue;
    slot.assignedUser = assignees.get(slot.assignedToFk) ?? {
      primaryKey: slot.assignedToFk,
      nickname: slot.assignedToFk,
      avatar: "",
      avatarMimeType: "",
    };
    slot.assignmentCandidate = slot.assignedUser;
  }
  return slots;
}

export const eventAuthoringService = {
  async searchUsers(search: string, signal?: AbortSignal): Promise<EventPerson[]> {
    const query = new URLSearchParams({ skip: "0", take: "20" });
    if (search.trim()) query.set("search", search.trim());
    const users = await getApiClient().request<FullUserDto[]>(
      apiPathWithQuery("/users/all", query.toString()),
      { method: apiMethod("/users/all", "post"), signal },
    );
    return (users ?? [])
      .map(normalizePerson)
      .filter((person): person is EventPerson => Boolean(person));
  },

  async load(eventId: string, signal?: AbortSignal): Promise<EventAuthoringSnapshot | undefined> {
    const [eventDto, slotDtos] = await Promise.all([
      getApiClient().request<FullEventDto>(fillApiPath("/events/{eventId}", { eventId }), {
        signal,
        expectedStatuses: [204, 404],
      }),
      getApiClient().request<PlainEventSlotDto[]>(
        fillApiPath("/events/{eventId}/slotting/all", { eventId }),
        { signal, expectedStatuses: [204, 404] },
      ),
    ]);
    const event = eventDto ? normalizeEvent(eventDto) : undefined;
    if (!event) return undefined;
    const slots = await hydrateSlotAssignees((slotDtos ?? []).map(normalizeSlot), signal);
    return { event, slots };
  },

  async create(values: EventFormValues, signal?: AbortSignal): Promise<string> {
    const result = await getApiClient().request<PlainEventDto>(apiPath("/events/create"), {
      method: apiMethod("/events/create", "post"),
      body: toCreationPayload(values),
      signal,
    });
    if (!result?.primaryKey) throw new Error("Event creation returned no event identifier.");
    return result.primaryKey;
  },

  async update(eventId: string, values: EventFormValues, signal?: AbortSignal): Promise<void> {
    const path = "/events/{eventId}/update" as const;
    await getApiClient().request<void>(fillApiPath(path, { eventId }), {
      method: apiMethod(path, "post"),
      body: toUpdatePayload(values),
      signal,
    });
  },

  async reloadSlots(eventId: string, signal?: AbortSignal): Promise<EventSlotDraft[]> {
    const slots = await getApiClient().request<PlainEventSlotDto[]>(
      fillApiPath("/events/{eventId}/slotting/all", { eventId }),
      { signal },
    );
    return hydrateSlotAssignees((slots ?? []).map(normalizeSlot), signal);
  },

  async createSlot(eventId: string, slot: EventSlotDraft, signal?: AbortSignal): Promise<void> {
    const path = "/events/{eventId}/slotting/create" as const;
    await getApiClient().request<PlainEventSlotDto>(fillApiPath(path, { eventId }), {
      method: apiMethod(path, "post"),
      body: toSlotCreationPayload(slot),
      signal,
    });
  },

  async updateSlot(eventId: string, slot: EventSlotDraft, signal?: AbortSignal): Promise<void> {
    if (slot.slotNumber == null) return;
    const path = "/events/{eventId}/slotting/{slotNumber}/update" as const;
    await getApiClient().request<void>(
      fillApiPath(path, { eventId, slotNumber: slot.slotNumber }),
      { method: apiMethod(path, "post"), body: toSlotUpdate(slot), signal },
    );
  },

  async deleteSlot(eventId: string, slotNumber: number, signal?: AbortSignal): Promise<void> {
    const path = "/events/{eventId}/slotting/{slotNumber}/delete" as const;
    await getApiClient().request<void>(fillApiPath(path, { eventId, slotNumber }), {
      method: apiMethod(path, "post"),
      signal,
    });
  },

  async assignSlot(
    eventId: string,
    slotNumber: number,
    userId: string,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/events/{eventId}/slotting/{slotNumber}/assign/user/{userId}" as const;
    await getApiClient().request<void>(fillApiPath(path, { eventId, slotNumber, userId }), {
      method: apiMethod(path, "post"),
      signal,
    });
  },

  async unassignSlot(
    eventId: string,
    slotNumber: number,
    userId: string,
    signal?: AbortSignal,
  ): Promise<void> {
    const path = "/events/{eventId}/slotting/{slotNumber}/unassign/user/{userId}" as const;
    await getApiClient().request<void>(fillApiPath(path, { eventId, slotNumber, userId }), {
      method: apiMethod(path, "post"),
      signal,
    });
  },
};
