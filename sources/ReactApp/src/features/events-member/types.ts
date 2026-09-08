import type { components } from "@/generated/api";
import type { EventSideId, EventSideLabelKey } from "@/features/events/eventSlotPresentation";

type Schema<Name extends keyof components["schemas"]> = components["schemas"][Name];
type RequiredSchema<Name extends keyof components["schemas"]> = Required<Schema<Name>>;

export type EventSlot = RequiredSchema<"PlainEventSlotDto">;
export type EventSlotRoleSymbol = Schema<"EEventSlotRoleSymbol">;
export type EventAssignee = Required<Pick<Schema<"UserDto">, "primaryKey" | "nickname">> &
  Schema<"UserDto">;
export type ProfileSettingsUpdate = Schema<"ProfileSettingsUpdate">;
export type KassenbuchEntry = RequiredSchema<"KassenbuchEntryDto">;
export type KassenbuchPage = Omit<RequiredSchema<"KassenbuchPageDto">, "entries"> & {
  entries: KassenbuchEntry[];
};

export type SlotSide = EventSlot["side"] | number;
export type SlotSideId = EventSideId;
export type SlotSideLabelKey = EventSideLabelKey;

export interface EventSlotRow {
  key: string;
  group: string;
  title: string;
  roleSymbol: EventSlotRoleSymbol;
  side: SlotSide;
  slots: EventSlot[];
  assignedCount: number;
  openCount: number;
}

export interface EventSlotGroup {
  name: string;
  rows: EventSlotRow[];
  assignedCount: number;
  totalCount: number;
}

export interface EventSlotSideGroup {
  id: SlotSideId;
  labelKey: SlotSideLabelKey | undefined;
  groups: EventSlotGroup[];
  assignedCount: number;
  totalCount: number;
}

export interface ProfileSettingsFormValues {
  nickname: string;
  eMail: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  phoneNumber: string;
  addressLine1: string;
  addressLine2: string;
  postalCode: string;
  city: string;
  country: string;
}
