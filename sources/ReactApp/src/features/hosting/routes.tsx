import type { ComponentType } from "react";
import { ConfigurationProfileCreatePage } from "@/features/hosting/ConfigurationProfileCreatePage";
import { ConfigurationProfileWorkspacePage } from "@/features/hosting/ConfigurationProfileWorkspacePage";
import { ConfigurationProfilesPage } from "@/features/hosting/ConfigurationProfilesPage";
import { CreateServerPage } from "@/features/hosting/CreateServerPage";
import { HostingOverviewPage, ServerFleetPage } from "@/features/hosting/FleetPages";
import { HostingFinancePage } from "@/features/hosting/HostingFinancePage";
import { HostingFinanceAccountPage } from "@/features/hosting/HostingFinanceAccountPage";
import { HostingFinanceSettingsPage } from "@/features/hosting/HostingFinanceSettingsPage";
import { ServerWorkspacePage } from "@/features/hosting/ServerWorkspacePage";

export interface HostingRouteDefinition {
  manifestPattern: string;
  path: string;
  Component: ComponentType;
  parity: "read-action" | "workflow";
}

/**
 * Integration contract for the shared router. These routes intentionally carry an explicit
 * parity level so the route manifest is not certified merely because a component exists.
 */
export const hostingRouteDefinitions: HostingRouteDefinition[] = [
  {
    manifestPattern: "/command",
    path: "command",
    Component: HostingOverviewPage,
    parity: "read-action",
  },
  {
    manifestPattern: "/command/servers",
    path: "command/servers",
    Component: ServerFleetPage,
    parity: "read-action",
  },
  {
    manifestPattern: "/command/servers/create",
    path: "command/servers/create",
    Component: CreateServerPage,
    parity: "workflow",
  },
  {
    manifestPattern: "/command/servers/{ServerId:guid}",
    path: "command/servers/:serverId",
    Component: ServerWorkspacePage,
    parity: "workflow",
  },
  {
    manifestPattern: "/command/configuration-profiles",
    path: "command/configuration-profiles",
    Component: ConfigurationProfilesPage,
    parity: "workflow",
  },
  {
    manifestPattern: "/command/configuration-profiles/create",
    path: "command/configuration-profiles/create",
    Component: ConfigurationProfileCreatePage,
    parity: "workflow",
  },
  {
    manifestPattern: "/command/configuration-profiles/{ProfileId:guid}",
    path: "command/configuration-profiles/:profileId",
    Component: ConfigurationProfileWorkspacePage,
    parity: "workflow",
  },
  {
    manifestPattern: "/command/hosting-finance",
    path: "command/hosting-finance",
    Component: HostingFinancePage,
    parity: "workflow",
  },
  {
    manifestPattern: "/command/hosting-finance/accounts/{CostOwnerId:guid}",
    path: "command/hosting-finance/accounts/:costOwnerId",
    Component: HostingFinanceAccountPage,
    parity: "workflow",
  },
  {
    manifestPattern: "/command/hosting-finance/settings",
    path: "command/hosting-finance/settings",
    Component: HostingFinanceSettingsPage,
    parity: "workflow",
  },
];
