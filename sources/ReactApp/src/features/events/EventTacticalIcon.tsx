import {
  Airplane24Regular,
  ArrowExit24Regular,
  Clock24Regular,
  Cube24Regular,
  Document24Regular,
  Eye24Regular,
  Flag24Regular,
  HeartPulse24Regular,
  Map24Regular,
  Megaphone24Regular,
  PeopleTeam24Regular,
  PersonStar24Regular,
  PersonVoice24Regular,
  Rocket24Regular,
  ScanPerson24Regular,
  ShieldTask24Regular,
  TargetArrow24Regular,
  VehicleTruck24Regular,
  Warning24Regular,
  WrenchScrewdriver24Regular,
  type FluentIcon,
} from "@fluentui/react-icons";
import type { components } from "@/generated/api";
import "./EventTacticalIcon.css";

export const eventMissionIconNames = [
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
] as const;

export type EventMissionIconName = (typeof eventMissionIconNames)[number];

export const eventRoleIconNames = [
  "anti-armor",
  "automatic-rifleman",
  "crew",
  "engineer",
  "grenadier",
  "host",
  "medical",
  "pilot",
  "radio",
  "rifleman",
  "slot",
  "stealth",
  "support",
] as const;

export type EventRoleIconName = (typeof eventRoleIconNames)[number];

export type EventSlotRoleSymbol = components["schemas"]["EEventSlotRoleSymbol"];

export const eventSlotRoleSymbols = [
  "Automatic",
  "Generic",
  "Rifleman",
  "AutomaticRifleman",
  "Grenadier",
  "AntiArmor",
  "Medic",
  "RadioOperator",
  "Leader",
  "Marksman",
  "Engineer",
  "Crew",
  "Pilot",
  "Support",
] as const satisfies readonly EventSlotRoleSymbol[];

export type EventTacticalIconName = EventMissionIconName | EventRoleIconName;

const missionIconNames = new Set<string>(eventMissionIconNames);
const slotRoleSymbols = new Set<string>(eventSlotRoleSymbols);
const iconByName: Record<EventTacticalIconName, FluentIcon> = {
  "anti-armor": Rocket24Regular,
  "automatic-rifleman": ScanPerson24Regular,
  crew: VehicleTruck24Regular,
  document: Document24Regular,
  engineer: WrenchScrewdriver24Regular,
  extract: ArrowExit24Regular,
  grenadier: ShieldTask24Regular,
  host: PersonStar24Regular,
  intel: Eye24Regular,
  map: Map24Regular,
  medical: HeartPulse24Regular,
  modpack: Cube24Regular,
  objective: TargetArrow24Regular,
  pilot: Airplane24Regular,
  radio: PersonVoice24Regular,
  rally: Flag24Regular,
  rifleman: TargetArrow24Regular,
  slot: PeopleTeam24Regular,
  stealth: Eye24Regular,
  support: Megaphone24Regular,
  time: Clock24Regular,
  warning: Warning24Regular,
};

const explicitRoleIconBySymbol = {
  Generic: "slot",
  Rifleman: "rifleman",
  AutomaticRifleman: "automatic-rifleman",
  Grenadier: "grenadier",
  AntiArmor: "anti-armor",
  Medic: "medical",
  RadioOperator: "radio",
  Leader: "host",
  Marksman: "stealth",
  Engineer: "engineer",
  Crew: "crew",
  Pilot: "pilot",
  Support: "support",
} as const satisfies Record<Exclude<EventSlotRoleSymbol, "Automatic">, EventRoleIconName>;

function normalizedRoleTitle(title: string): string {
  return title
    .normalize("NFKD")
    .replace(/\p{Diacritic}/gu, "")
    .toLocaleLowerCase();
}

export function eventMissionIconName(value: string): EventMissionIconName {
  const normalized = value.trim().toLocaleLowerCase();
  return missionIconNames.has(normalized) ? (normalized as EventMissionIconName) : "objective";
}

export function eventRoleIconName(title: string): EventRoleIconName {
  const value = normalizedRoleTitle(title);

  if (/(medic|medical|corpsman|sanitat)/.test(value)) return "medical";
  if (/(\brto\b|\bjtac\b|\bfac\b|radio|funk)/.test(value)) return "radio";
  if (
    /(team.?lead|squad.?lead|platoon|command|commander|officer|leader|gruppenfuhr|truppfuhr|zugfuhr)/.test(
      value,
    )
  )
    return "host";
  if (/(pilot|aviator|flieger)/.test(value)) return "pilot";
  if (/(driver|crew|vehicle|fahrer|besatz)/.test(value)) return "crew";
  if (/(anti.?tank|anti.?air|missile|launcher|panzerabwehr|flugabwehr|\b(?:at|aa)\b)/.test(value))
    return "anti-armor";
  if (/(engineer|\beod\b|demolition|pioneer|pionier|spreng)/.test(value)) return "engineer";
  if (/(marksman|sniper|scout|recon|scharfschutze|aufklar)/.test(value)) return "stealth";
  if (/(automatic.?rifle|autorifle|machine.?gun|gunner|mg.?schutze)/.test(value))
    return "automatic-rifleman";
  if (/(grenadier|granat)/.test(value)) return "grenadier";
  if (/(rifleman|fusilier|schutze)/.test(value)) return "rifleman";
  if (/(support|assistant|ammo|versorgung)/.test(value)) return "support";
  return "slot";
}

export function normalizeEventSlotRoleSymbol(value: unknown): EventSlotRoleSymbol {
  return typeof value === "string" && slotRoleSymbols.has(value)
    ? (value as EventSlotRoleSymbol)
    : "Automatic";
}

export function eventSlotRoleSymbolIconName(
  value: unknown,
  title: string | null | undefined,
): EventRoleIconName {
  const symbol = normalizeEventSlotRoleSymbol(value);
  return symbol === "Automatic" ? eventRoleIconName(title ?? "") : explicitRoleIconBySymbol[symbol];
}

export function EventTacticalIcon({
  name,
  className,
}: {
  name: EventTacticalIconName;
  className?: string;
}) {
  const Icon = iconByName[name];
  return (
    <span
      className={["event-tactical-icon", className].filter(Boolean).join(" ")}
      data-tactical-icon={name}
      aria-hidden="true"
    >
      <Icon />
    </span>
  );
}
