/** How completely a hosting route reproduces the behavior of its legacy screen. */
export type HostingRouteParity = "read-action" | "workflow";

/** One hosting route of the command center, consumed by the shared router in `App.tsx`. */
export interface HostingRouteDefinition {
  /** The route manifest pattern the route certifies against. */
  manifestPattern: string;
  /** The route path, relative to the `command` layout route. */
  path: string;
  /** The export of `@/features/hosting` that is lazy-loaded as the route component. */
  exportName: string;
  /** The parity level, so a route is not certified merely because a component exists. */
  parity: HostingRouteParity;
}

/**
 * The hosting routes under `/command`. `App.tsx` builds its lazy-loaded routes from this table, and
 * the screen-registry and accessibility discovery checks read it, so there is one source of truth.
 * The `/command` overview route is not hosting-specific and lives in `App.tsx`.
 */
export const hostingRouteDefinitions: HostingRouteDefinition[] = [
  {
    manifestPattern: "/command/servers",
    path: "servers",
    exportName: "ServerFleetPage",
    parity: "read-action",
  },
  {
    manifestPattern: "/command/servers/create",
    path: "servers/create",
    exportName: "CreateServerPage",
    parity: "workflow",
  },
  {
    manifestPattern: "/command/servers/cost-alerts",
    path: "servers/cost-alerts",
    exportName: "CostAlertsPage",
    parity: "workflow",
  },
  {
    manifestPattern: "/command/servers/{ServerId:guid}",
    path: "servers/:serverId",
    exportName: "ServerWorkspacePage",
    parity: "workflow",
  },
  {
    manifestPattern: "/command/configuration-profiles",
    path: "configuration-profiles",
    exportName: "ConfigurationProfilesPage",
    parity: "workflow",
  },
  {
    manifestPattern: "/command/configuration-profiles/create",
    path: "configuration-profiles/create",
    exportName: "ConfigurationProfileCreatePage",
    parity: "workflow",
  },
  {
    manifestPattern: "/command/configuration-profiles/{ProfileId:guid}",
    path: "configuration-profiles/:profileId",
    exportName: "ConfigurationProfileWorkspacePage",
    parity: "workflow",
  },
  {
    manifestPattern: "/command/hosting-finance",
    path: "hosting-finance",
    exportName: "HostingFinancePage",
    parity: "workflow",
  },
  {
    manifestPattern: "/command/hosting-finance/accounts/{CostOwnerId:guid}",
    path: "hosting-finance/accounts/:costOwnerId",
    exportName: "HostingFinanceAccountPage",
    parity: "workflow",
  },
  {
    manifestPattern: "/command/hosting-finance/settings",
    path: "hosting-finance/settings",
    exportName: "HostingFinanceSettingsPage",
    parity: "workflow",
  },
];
