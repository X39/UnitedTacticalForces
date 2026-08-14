import type { ComponentType } from "react";
import { AccountSettingsPage } from "./AccountSettingsPage";
import { EventDetailPage } from "./EventDetailPage";
import { VereinLedgerPage } from "./VereinLedgerPage";
import "./eventsMember.css";

export interface EventsMemberRouteContract {
  id: "event-detail" | "account-settings" | "verein-ledger";
  path: string;
  aliases: readonly string[];
  requiresAuthentication: boolean;
  Component: ComponentType;
}

export const eventsMemberRoutes = [
  {
    id: "event-detail",
    path: "/events/:eventId",
    aliases: [],
    requiresAuthentication: false,
    Component: EventDetailPage,
  },
  {
    id: "account-settings",
    path: "/settings",
    aliases: [],
    requiresAuthentication: true,
    Component: AccountSettingsPage,
  },
  {
    id: "verein-ledger",
    path: "/verein/kassenbuch",
    aliases: ["/unit/kassenbuch"],
    requiresAuthentication: true,
    Component: VereinLedgerPage,
  },
] as const satisfies readonly EventsMemberRouteContract[];

export { AccountSettingsPage, EventDetailPage, VereinLedgerPage };
export * from "./eventModel";
export * from "./memberAccess";
export * from "./services";
export type * from "./types";
