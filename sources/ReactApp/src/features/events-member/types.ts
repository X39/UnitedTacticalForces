import type { components } from "@/generated/api";

type Schema<Name extends keyof components["schemas"]> = components["schemas"][Name];
type RequiredSchema<Name extends keyof components["schemas"]> = Required<Schema<Name>>;

export type EventSlot = RequiredSchema<"PlainEventSlotDto">;
export type EventAssignee = Required<Pick<Schema<"UserDto">, "primaryKey" | "nickname">> &
  Schema<"UserDto">;
export type ProfileSettingsUpdate = Schema<"ProfileSettingsUpdate">;
export type KassenbuchEntry = RequiredSchema<"KassenbuchEntryDto">;
export type KassenbuchPage = Omit<RequiredSchema<"KassenbuchPageDto">, "entries"> & {
  entries: KassenbuchEntry[];
};

export type SlotSide = EventSlot["side"] | number;

export interface EventSlotRow {
  key: string;
  group: string;
  title: string;
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
