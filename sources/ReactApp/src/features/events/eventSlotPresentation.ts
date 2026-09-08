export type EventSideId =
  "ambient" | "civilian" | "east" | "empty" | "logic" | "resistance" | "west";

export type EventSideLabelKey = "Ambient" | "Civilian" | "East" | "Logic" | "Resistance" | "West";

export interface EventSidePresentation {
  id: EventSideId;
  labelKey: EventSideLabelKey | undefined;
  order: number;
}

const sidePresentations: Record<EventSideId, EventSidePresentation> = {
  west: { id: "west", labelKey: "West", order: 0 },
  east: { id: "east", labelKey: "East", order: 1 },
  resistance: { id: "resistance", labelKey: "Resistance", order: 2 },
  civilian: { id: "civilian", labelKey: "Civilian", order: 3 },
  ambient: { id: "ambient", labelKey: "Ambient", order: 4 },
  logic: { id: "logic", labelKey: "Logic", order: 5 },
  empty: { id: "empty", labelKey: undefined, order: 6 },
};

export function eventSidePresentation(
  side: string | number | null | undefined,
): EventSidePresentation {
  const value = typeof side === "string" ? side.trim().toLocaleLowerCase() : side;
  if (value === 2 || value === "west") return sidePresentations.west;
  if (value === 1 || value === "east") return sidePresentations.east;
  if (value === 3 || value === "resistance") return sidePresentations.resistance;
  if (value === 4 || value === "civilian") return sidePresentations.civilian;
  if (value === 5 || value === "ambientlife" || value === "ambient")
    return sidePresentations.ambient;
  if (value === 6 || value === "logic") return sidePresentations.logic;
  return sidePresentations.empty;
}
