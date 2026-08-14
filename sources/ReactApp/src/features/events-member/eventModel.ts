import type { FullEvent, FullUser } from "@/api/types";
import type { EventSlot, EventSlotGroup, EventSlotRow, SlotSide } from "./types";

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const briefingIconPattern = /^:(?<icon>[a-z0-9-]+):\s*/i;
const listPrefixPattern = /^\s*(?:[-*+]\s+|\d+[.)]\s+)/;
const supportedBriefingIcons = new Set([
  "document",
  "extract",
  "host",
  "intel",
  "map",
  "medical",
  "modpack",
  "objective",
  "radio",
  "rally",
  "slot",
  "stealth",
  "support",
  "time",
  "warning",
]);
const fallbackBriefingIcons = ["objective", "warning", "radio"] as const;

export interface BriefingObjective {
  icon: string;
  text: string;
}

export type SlotRowAction =
  | { kind: "assign" | "claim-role"; slot: EventSlot }
  | {
      kind: "assigned" | "assigning" | "closed" | "current" | "hidden" | "locked" | "sign-in";
    };

function normalizeBriefingText(value: string): string {
  return value
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, "$1")
    .replace(/\[([^\]]+)\]\([^)]*\)/g, "$1")
    .replace(/`([^`]+)`/g, "$1")
    .replace(/:[a-z0-9-]+:/gi, "")
    .replace(/[*_~>#]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

function truncateBriefingText(value: string): string {
  return value.length <= 150 ? value : `${value.slice(0, 147).trimEnd()}...`;
}

function normalizeBriefingProse(value: string): string {
  let insideFence = false;
  const lines = value.replace(/\r\n?/g, "\n").split("\n");
  return normalizeBriefingText(
    lines
      .filter((line) => {
        const trimmed = line.trim();
        if (trimmed.startsWith("```")) {
          insideFence = !insideFence;
          return false;
        }
        return !insideFence && !trimmed.startsWith("#") && !trimmed.startsWith("|");
      })
      .join(" "),
  );
}

function fallbackObjectives(description: string): BriefingObjective[] {
  return normalizeBriefingProse(description)
    .split(/(?<=[.!?])\s+/)
    .map((value) => value.trim())
    .filter(Boolean)
    .slice(0, 3)
    .map((text, index) => ({
      icon: fallbackBriefingIcons[index % fallbackBriefingIcons.length]!,
      text: truncateBriefingText(text),
    }));
}

/**
 * Builds the compact objective rail used above the full briefing. The extraction mirrors the
 * classic client: explicit supported icon shortcodes win, list entries become objectives, and a
 * short sentence fallback keeps prose-only briefings useful.
 */
export function extractBriefingObjectives(
  description: string | null | undefined,
): BriefingObjective[] {
  if (!description?.trim()) return [];

  const objectives: BriefingObjective[] = [];
  let insideFence = false;

  for (const sourceLine of description.replace(/\r\n?/g, "\n").split("\n")) {
    const trimmed = sourceLine.trim();
    if (trimmed.startsWith("```")) {
      insideFence = !insideFence;
      continue;
    }
    if (!trimmed || insideFence || trimmed.startsWith("#") || trimmed.startsWith("|")) continue;

    const listMatch = listPrefixPattern.exec(sourceLine);
    const candidate = listMatch ? sourceLine.slice(listMatch[0].length) : trimmed;
    const iconMatch = briefingIconPattern.exec(candidate);
    const icon = iconMatch?.groups?.icon?.toLocaleLowerCase();
    const supportedIcon = icon && supportedBriefingIcons.has(icon) ? icon : undefined;
    if (!listMatch && !supportedIcon) continue;
    const normalized = normalizeBriefingText(candidate);

    const wordCount = normalized.split(/\s+/).filter(Boolean).length;
    const looksLikeShortHeading =
      !supportedIcon &&
      !listMatch &&
      normalized.length <= 42 &&
      wordCount <= 4 &&
      !/[.,:]/.test(normalized);
    if (!normalized || looksLikeShortHeading) continue;

    objectives.push({
      icon:
        supportedIcon ?? fallbackBriefingIcons[objectives.length % fallbackBriefingIcons.length]!,
      text: truncateBriefingText(normalized),
    });
    if (objectives.length === 3) return objectives;
  }

  if (objectives.length > 0) return objectives;

  return fallbackObjectives(description);
}

export function isEventId(value: string): boolean {
  return uuidPattern.test(value);
}

function textOrFallback(value: string | null | undefined, fallback: string): string {
  const trimmed = value?.trim();
  return trimmed ? trimmed : fallback;
}

function sideSort(side: SlotSide): number {
  const value = typeof side === "number" ? side : side.toLocaleLowerCase();
  if (value === 2 || value === "west") return 0;
  if (value === 1 || value === "east") return 1;
  if (value === 3 || value === "resistance") return 2;
  if (value === 4 || value === "civilian") return 3;
  if (value === 5 || value === "ambientlife" || value === "ambient") return 4;
  if (value === 6 || value === "logic") return 5;
  return 6;
}

export function slotSideKey(
  side: SlotSide,
): "East" | "West" | "Resistance" | "Civilian" | "Ambient" | "Logic" | undefined {
  const value = typeof side === "string" ? side.trim().toLocaleLowerCase() : side;
  if (value === 1 || value === "east") return "East";
  if (value === 2 || value === "west") return "West";
  if (value === 3 || value === "resistance") return "Resistance";
  if (value === 4 || value === "civilian") return "Civilian";
  if (value === 5 || value === "ambientlife" || value === "ambient") return "Ambient";
  if (value === 6 || value === "logic") return "Logic";
  return undefined;
}

export function groupEventSlots(
  slots: EventSlot[],
  ungroupedLabel: string,
  unassignedRoleLabel: string,
  locale?: string,
): EventSlotGroup[] {
  const grouped = new Map<string, Map<string, EventSlot[]>>();
  const collator = new Intl.Collator(locale, { sensitivity: "base" });

  for (const slot of slots) {
    const group = textOrFallback(slot.group, ungroupedLabel);
    const title = textOrFallback(slot.title, unassignedRoleLabel);
    const rowKey = `${slotSideKey(slot.side) ?? "Empty"}|${title}`;
    const rows = grouped.get(group) ?? new Map<string, EventSlot[]>();
    rows.set(rowKey, [...(rows.get(rowKey) ?? []), slot]);
    grouped.set(group, rows);
  }

  return [...grouped.entries()]
    .sort(([left], [right]) => collator.compare(left, right))
    .map(([name, rows]) => {
      const mappedRows: EventSlotRow[] = [...rows.entries()]
        .map(([key, rowSlots]) => {
          const orderedSlots = [...rowSlots].sort(
            (left, right) => left.slotNumber - right.slotNumber,
          );
          const assignedCount = orderedSlots.filter((slot) => slot.assignedToFk != null).length;
          const openCount = orderedSlots.filter(
            (slot) => slot.assignedToFk == null && slot.isVisible !== false,
          ).length;
          const first = orderedSlots[0]!;
          return {
            key: `${name}|${key}`,
            group: name,
            title: textOrFallback(first.title, unassignedRoleLabel),
            side: first.side,
            slots: orderedSlots,
            assignedCount,
            openCount,
          };
        })
        .sort(
          (left, right) =>
            sideSort(left.side) - sideSort(right.side) ||
            left.slots[0]!.slotNumber - right.slots[0]!.slotNumber ||
            collator.compare(left.title, right.title),
        );
      return {
        name,
        rows: mappedRows,
        assignedCount: mappedRows.reduce((total, row) => total + row.assignedCount, 0),
        totalCount: mappedRows.reduce((total, row) => total + row.slots.length, 0),
      };
    });
}

export function eventHasStarted(event: FullEvent, now = new Date()): boolean {
  const scheduledFor = new Date(event.scheduledFor);
  return !Number.isNaN(scheduledFor.getTime()) && scheduledFor.getTime() < now.getTime();
}

export function canSelfAssignSlot(
  slot: EventSlot,
  user: FullUser | undefined,
  event: FullEvent,
  mutationPending: boolean,
): boolean {
  return Boolean(
    user &&
    !user.isBanned &&
    !user.isDeleted &&
    !eventHasStarted(event) &&
    !mutationPending &&
    slot.assignedToFk == null &&
    slot.isSelfAssignable !== false &&
    slot.isVisible !== false,
  );
}

export function currentUserSlot(
  slots: EventSlot[],
  user: FullUser | undefined,
): EventSlot | undefined {
  return user ? slots.find((slot) => slot.assignedToFk === user.primaryKey) : undefined;
}

export function firstAssignableSlot(
  row: EventSlotRow,
  user: FullUser | undefined,
  event: FullEvent,
  mutationPending: boolean,
): EventSlot | undefined {
  return row.slots.find((slot) => canSelfAssignSlot(slot, user, event, mutationPending));
}

export function slotRowAction(
  row: EventSlotRow,
  user: FullUser | undefined,
  event: FullEvent,
  assigningSlotNumber?: number,
): SlotRowAction {
  if (row.slots.some((slot) => slot.assignedToFk === user?.primaryKey)) return { kind: "current" };
  if (
    assigningSlotNumber != null &&
    row.slots.some((slot) => slot.slotNumber === assigningSlotNumber)
  ) {
    return { kind: "assigning" };
  }
  if (row.slots.every((slot) => slot.isVisible === false)) return { kind: "hidden" };
  if (row.slots.every((slot) => slot.isSelfAssignable === false || slot.isVisible === false))
    return { kind: "locked" };
  if (eventHasStarted(event)) return { kind: "closed" };
  if (!user || user.isBanned || user.isDeleted) return { kind: "sign-in" };
  if (row.openCount === 0) return { kind: "assigned" };

  const slot = firstAssignableSlot(row, user, event, false);
  if (!slot) return { kind: "locked" };
  return { kind: row.slots.length > 1 ? "claim-role" : "assign", slot };
}

export function attendanceChangeNeedsSlotLossConfirmation(
  currentSlot: EventSlot | undefined,
  acceptance: "accept" | "maybe" | "reject",
): boolean {
  return currentSlot != null && acceptance !== "accept";
}

export function eventAttendanceProgress(event: FullEvent): {
  accepted: number;
  minimum: number;
  percentage: number;
  needsMoreSignups: boolean;
} {
  const accepted = event.acceptedCount ?? 0;
  const minimum = event.minimumAccepted ?? 0;
  const percentage =
    minimum <= 0
      ? accepted > 0
        ? 100
        : 0
      : Math.min(100, Math.max(0, (accepted * 100) / minimum));
  return { accepted, minimum, percentage, needsMoreSignups: minimum > 0 && accepted < minimum };
}

export function formatEventTimeRemaining(value: string | undefined, now = new Date()): string {
  if (!value) return "tbd";
  const scheduledFor = new Date(value);
  if (Number.isNaN(scheduledFor.getTime())) return "tbd";
  const remainingMilliseconds = scheduledFor.getTime() - now.getTime();
  if (remainingMilliseconds <= 0) return "started";

  const totalMinutes = Math.max(1, Math.floor(remainingMilliseconds / 60_000));
  const days = Math.floor(totalMinutes / 1_440);
  const hours = Math.floor((totalMinutes % 1_440) / 60);
  const minutes = totalMinutes % 60;
  if (days >= 1) return `${days}d ${hours}h`;
  if (hours >= 1) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}

export function modPackDownloadPath(event: FullEvent): string | undefined {
  if (event.modPackDefinitionPrimaryKey == null) return undefined;
  return event.modPackRevisionPrimaryKey == null
    ? `/mod-packs/${event.modPackDefinitionPrimaryKey}/download/latest`
    : `/mod-packs/${event.modPackDefinitionPrimaryKey}/download/${event.modPackRevisionPrimaryKey}`;
}

export function safeDownloadFileName(title: string | null | undefined): string {
  const safe = [...(title ?? "")].filter((value) => /[\p{L}\p{N}+-]/u.test(value)).join("");
  return `${safe || "modpack"}.html`;
}
