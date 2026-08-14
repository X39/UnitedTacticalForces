import {
  Apps20Regular,
  Book20Regular,
  Calendar20Regular,
  Document20Regular,
  Globe20Regular,
  Home20Regular,
  Image20Regular,
  Key20Regular,
  Money20Regular,
  People20Regular,
  Server20Regular,
  Settings20Regular,
  Shield20Regular,
  type FluentIcon,
} from "@fluentui/react-icons";
import type { FullUser } from "@/api/types";
import type { TranslationKey } from "@/app/localizationTypes";
import {
  canAccessConfigurationProfiles,
  canAccessGameServers,
  canAccessTerrains,
  canManageAuthority,
  canManageContent,
  canManageEvents,
  canManageGallery,
  canManageModPacks,
  canManageSettings,
  canManageSiteImages,
  canManageVereinDocuments,
  canManageVereinLedger,
  canManageVereinMemberships,
  canReadFieldReports,
  hasBaseClaim,
  hasClaim,
} from "@/auth/claims";

export type CommandNavigationGroupId =
  "command" | "operations" | "finance" | "assets" | "community" | "governance";

interface CommandContextRule {
  matches(pathname: string): boolean;
  titleKey: TranslationKey;
  priority?: number;
}

export interface CommandNavigationItem {
  id: string;
  group: CommandNavigationGroupId;
  to: string;
  labelKey: TranslationKey;
  icon: FluentIcon;
  isAuthorized(user: FullUser | undefined): boolean;
  isActive(pathname: string): boolean;
  contexts: CommandContextRule[];
  showOnOverview?: boolean;
}

export interface CommandNavigationGroup {
  id: CommandNavigationGroupId;
  labelKey: TranslationKey;
  items: CommandNavigationItem[];
}

export interface CommandNavigationContext {
  item: CommandNavigationItem;
  titleKey: TranslationKey;
}

const exact =
  (...paths: string[]) =>
  (pathname: string) =>
    paths.includes(pathname);
const startsWith =
  (...prefixes: string[]) =>
  (pathname: string) =>
    prefixes.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));

function normalizedPath(pathname: string): string {
  const normalized = pathname.toLocaleLowerCase().replace(/\/+$/, "");
  return normalized || "/";
}

const groupDefinitions: ReadonlyArray<{
  id: CommandNavigationGroupId;
  labelKey: TranslationKey;
}> = [
  { id: "command", labelKey: "Shared_CommandCenter_Group_CommandCenter" },
  { id: "operations", labelKey: "Shared_CommandCenter_Group_Operations" },
  { id: "finance", labelKey: "Shared_CommandCenter_Group_Finance" },
  { id: "assets", labelKey: "Shared_CommandCenter_Group_Assets" },
  { id: "community", labelKey: "Shared_CommandCenter_Group_Community" },
  { id: "governance", labelKey: "Shared_CommandCenter_Group_Governance" },
];

export const commandNavigationItems: readonly CommandNavigationItem[] = [
  {
    id: "overview",
    group: "command",
    to: "/command",
    labelKey: "Shared_CommandCenter_Nav_Overview",
    icon: Home20Regular,
    isAuthorized: () => true,
    isActive: exact("/command"),
    contexts: [
      {
        matches: exact("/command"),
        titleKey: "Shared_CommandCenter_CurrentArea_Overview",
        priority: 100,
      },
    ],
  },
  {
    id: "servers",
    group: "operations",
    to: "/command/servers",
    labelKey: "Hosting_Nav_Servers",
    icon: Server20Regular,
    isAuthorized: canAccessGameServers,
    isActive: startsWith("/command/servers"),
    showOnOverview: true,
    contexts: [
      {
        matches: exact("/command/servers/create"),
        titleKey: "Shared_CommandCenter_CurrentArea_CreateServer",
        priority: 120,
      },
      {
        matches: exact("/command/servers"),
        titleKey: "Shared_CommandCenter_CurrentArea_ServerFleet",
        priority: 110,
      },
      {
        matches: startsWith("/command/servers"),
        titleKey: "Shared_CommandCenter_CurrentArea_ServerWorkspace",
        priority: 100,
      },
    ],
  },
  {
    id: "configuration-profiles",
    group: "operations",
    to: "/command/configuration-profiles",
    labelKey: "Shared_CommandCenter_Nav_CloudConfiguration",
    icon: Settings20Regular,
    isAuthorized: canAccessConfigurationProfiles,
    isActive: startsWith("/command/configuration-profiles"),
    showOnOverview: true,
    contexts: [
      {
        matches: startsWith("/command/configuration-profiles"),
        titleKey: "Shared_CommandCenter_CurrentArea_CloudConfiguration",
      },
    ],
  },
  {
    id: "events",
    group: "operations",
    to: "/command/events",
    labelKey: "Shared_CommandCenter_Nav_Events",
    icon: Calendar20Regular,
    isAuthorized: canManageEvents,
    isActive: startsWith("/command/events", "/events/all", "/events/create"),
    showOnOverview: true,
    contexts: [
      {
        matches: exact("/command/events/create", "/events/create"),
        titleKey: "Shared_CommandCenter_CurrentArea_CreateEvent",
        priority: 110,
      },
      {
        matches: exact("/command/events", "/events/all"),
        titleKey: "Shared_CommandCenter_CurrentArea_EventAdministration",
        priority: 100,
      },
      {
        matches: startsWith("/command/events"),
        titleKey: "Shared_CommandCenter_CurrentArea_EventEditor",
      },
    ],
  },
  {
    id: "hosting-finance",
    group: "finance",
    to: "/command/hosting-finance",
    labelKey: "Shared_CommandCenter_Nav_HostingFinance",
    icon: Money20Regular,
    isAuthorized: (user) => hasBaseClaim(user, "fin", "fin:view-all"),
    isActive: startsWith("/command/hosting-finance"),
    showOnOverview: true,
    contexts: [
      {
        matches: startsWith("/command/hosting-finance"),
        titleKey: "Shared_CommandCenter_Nav_HostingFinance",
      },
    ],
  },
  {
    id: "mod-packs",
    group: "assets",
    to: "/command/mod-packs",
    labelKey: "Shared_CommandCenter_Nav_ModPacks",
    icon: Apps20Regular,
    isAuthorized: (user) =>
      canManageModPacks(user) || hasClaim(user, "mp", "mp:modify", "mp:delete"),
    isActive: startsWith("/command/mod-packs", "/data/mod-packs"),
    showOnOverview: true,
    contexts: [
      {
        matches: exact(
          "/command/mod-packs",
          "/command/mod-packs/create",
          "/data/mod-packs/all",
          "/data/mod-packs/create",
        ),
        titleKey: "Shared_CommandCenter_CurrentArea_ModPacks",
        priority: 100,
      },
      {
        matches: startsWith("/command/mod-packs", "/data/mod-packs"),
        titleKey: "Shared_CommandCenter_CurrentArea_ModPackDetail",
      },
    ],
  },
  {
    id: "terrains",
    group: "assets",
    to: "/command/terrains",
    labelKey: "Shared_CommandCenter_Nav_Terrains",
    icon: Globe20Regular,
    isAuthorized: canAccessTerrains,
    isActive: startsWith("/command/terrains", "/data/terrains"),
    showOnOverview: true,
    contexts: [
      {
        matches: startsWith("/command/terrains", "/data/terrains"),
        titleKey: "Shared_CommandCenter_CurrentArea_Terrains",
      },
    ],
  },
  {
    id: "field-reports",
    group: "assets",
    to: "/command/field-reports",
    labelKey: "Shared_CommandCenter_Nav_FieldReports",
    icon: Document20Regular,
    isAuthorized: canReadFieldReports,
    isActive: startsWith("/command/field-reports", "/command/wiki-media"),
    showOnOverview: true,
    contexts: [
      {
        matches: exact("/command/field-reports/create"),
        titleKey: "Shared_CommandCenter_CurrentArea_CreateFieldReport",
        priority: 110,
      },
      {
        matches: exact("/command/field-reports", "/command/wiki-media"),
        titleKey: "Shared_CommandCenter_CurrentArea_FieldReports",
        priority: 100,
      },
      {
        matches: startsWith("/command/field-reports"),
        titleKey: "Shared_CommandCenter_CurrentArea_EditFieldReport",
      },
    ],
  },
  {
    id: "gallery",
    group: "assets",
    to: "/command/content/gallery",
    labelKey: "Shared_CommandCenter_Nav_Gallery",
    icon: Image20Regular,
    isAuthorized: canManageGallery,
    isActive: startsWith("/command/content/gallery"),
    showOnOverview: true,
    contexts: [
      {
        matches: startsWith("/command/content/gallery"),
        titleKey: "Shared_CommandCenter_CurrentArea_Gallery",
        priority: 120,
      },
    ],
  },
  {
    id: "public-images",
    group: "assets",
    to: "/command/content/images/home",
    labelKey: "Shared_CommandCenter_Nav_PublicImages",
    icon: Image20Regular,
    isAuthorized: canManageSiteImages,
    isActive: startsWith("/command/content/images"),
    contexts: [
      {
        matches: startsWith("/command/content/images"),
        titleKey: "Shared_CommandCenter_CurrentArea_AssetControl",
        priority: 120,
      },
    ],
  },
  {
    id: "members",
    group: "community",
    to: "/command/members",
    labelKey: "Shared_CommandCenter_Nav_Members",
    icon: People20Regular,
    isAuthorized: canManageVereinMemberships,
    isActive: startsWith("/command/members"),
    showOnOverview: true,
    contexts: [
      {
        matches: exact("/command/members"),
        titleKey: "Shared_CommandCenter_CurrentArea_Members",
        priority: 100,
      },
      {
        matches: startsWith("/command/members"),
        titleKey: "Shared_CommandCenter_CurrentArea_MemberDetail",
      },
    ],
  },
  {
    id: "roles",
    group: "community",
    to: "/command/roles",
    labelKey: "Shared_CommandCenter_Nav_Roles",
    icon: Shield20Regular,
    isAuthorized: canManageAuthority,
    isActive: startsWith("/command/roles"),
    showOnOverview: true,
    contexts: [
      {
        matches: startsWith("/command/roles"),
        titleKey: "Shared_CommandCenter_CurrentArea_Roles",
      },
    ],
  },
  {
    id: "claims",
    group: "community",
    to: "/command/claims",
    labelKey: "Shared_CommandCenter_Nav_Claims",
    icon: Key20Regular,
    isAuthorized: canManageAuthority,
    isActive: startsWith("/command/claims"),
    contexts: [
      {
        matches: startsWith("/command/claims"),
        titleKey: "Shared_CommandCenter_CurrentArea_Claims",
      },
    ],
  },
  {
    id: "content",
    group: "governance",
    to: "/command/content",
    labelKey: "Shared_CommandCenter_Nav_ContentAndDocuments",
    icon: Book20Regular,
    isAuthorized: canManageContent,
    isActive: (pathname) =>
      startsWith("/command/content")(pathname) &&
      !startsWith("/command/content/gallery", "/command/content/images")(pathname),
    showOnOverview: true,
    contexts: [
      {
        matches: exact("/command/content"),
        titleKey: "Shared_CommandCenter_Nav_ContentAndDocuments",
        priority: 110,
      },
      {
        matches: exact("/command/content/pages"),
        titleKey: "Shared_CommandCenter_CurrentArea_PublicPages",
        priority: 110,
      },
      {
        matches: (pathname) =>
          startsWith("/command/content/pages")(pathname) && pathname.endsWith("/edit"),
        titleKey: "Shared_CommandCenter_CurrentArea_EditPublicPage",
        priority: 110,
      },
      {
        matches: startsWith("/command/content/pages"),
        titleKey: "Shared_CommandCenter_CurrentArea_PublicPage",
        priority: 100,
      },
    ],
  },
  {
    id: "membership-requests",
    group: "governance",
    to: "/command/verein/membership-requests",
    labelKey: "Shared_CommandCenter_Nav_VereinMembershipRequests",
    icon: People20Regular,
    isAuthorized: canManageVereinMemberships,
    isActive: startsWith("/command/verein/membership-requests"),
    showOnOverview: true,
    contexts: [
      {
        matches: startsWith("/command/verein/membership-requests"),
        titleKey: "Shared_CommandCenter_CurrentArea_VereinMembershipRequests",
        priority: 120,
      },
    ],
  },
  {
    id: "verein-documents",
    group: "governance",
    to: "/command/verein/documents",
    labelKey: "Shared_CommandCenter_Nav_VereinDocuments",
    icon: Document20Regular,
    isAuthorized: canManageVereinDocuments,
    isActive: startsWith("/command/verein/documents"),
    showOnOverview: true,
    contexts: [
      {
        matches: exact("/command/verein/documents/create"),
        titleKey: "Shared_CommandCenter_CurrentArea_CreateVereinDocument",
        priority: 130,
      },
      {
        matches: (pathname) =>
          startsWith("/command/verein/documents")(pathname) && pathname.endsWith("/edit"),
        titleKey: "Shared_CommandCenter_CurrentArea_EditVereinDocument",
        priority: 130,
      },
      {
        matches: exact("/command/verein/documents"),
        titleKey: "Shared_CommandCenter_CurrentArea_VereinDocuments",
        priority: 120,
      },
      {
        matches: startsWith("/command/verein/documents"),
        titleKey: "Shared_CommandCenter_CurrentArea_VereinDocument",
        priority: 110,
      },
    ],
  },
  {
    id: "verein-ledger",
    group: "governance",
    to: "/command/verein/kassenbuch",
    labelKey: "Shared_CommandCenter_Nav_VereinKassenbuch",
    icon: Money20Regular,
    isAuthorized: canManageVereinLedger,
    isActive: startsWith("/command/verein/kassenbuch"),
    showOnOverview: true,
    contexts: [
      {
        matches: exact("/command/verein/kassenbuch/create"),
        titleKey: "Shared_CommandCenter_CurrentArea_CreateKassenbuchEntry",
        priority: 130,
      },
      {
        matches: (pathname) =>
          startsWith("/command/verein/kassenbuch")(pathname) && pathname.endsWith("/edit"),
        titleKey: "Shared_CommandCenter_CurrentArea_EditKassenbuchEntry",
        priority: 130,
      },
      {
        matches: startsWith("/command/verein/kassenbuch"),
        titleKey: "Shared_CommandCenter_CurrentArea_VereinKassenbuch",
        priority: 120,
      },
    ],
  },
  {
    id: "verein-settings",
    group: "governance",
    to: "/command/verein/settings",
    labelKey: "Shared_CommandCenter_Nav_VereinSettings",
    icon: Settings20Regular,
    isAuthorized: canManageVereinMemberships,
    isActive: exact(
      "/command/verein/settings",
      "/command/settings/verein",
      "/command/association/settings",
    ),
    contexts: [
      {
        matches: exact(
          "/command/verein/settings",
          "/command/settings/verein",
          "/command/association/settings",
        ),
        titleKey: "Shared_CommandCenter_CurrentArea_VereinSettings",
        priority: 120,
      },
    ],
  },
  {
    id: "settings",
    group: "governance",
    to: "/command/settings",
    labelKey: "Shared_CommandCenter_Nav_Settings",
    icon: Settings20Regular,
    isAuthorized: canManageSettings,
    isActive: exact("/command/settings"),
    contexts: [
      {
        matches: exact("/command/settings"),
        titleKey: "Shared_CommandCenter_CurrentArea_Settings",
        priority: 110,
      },
    ],
  },
];

export function getCommandNavigationGroups(user: FullUser | undefined): CommandNavigationGroup[] {
  return groupDefinitions
    .map((group) => ({
      ...group,
      items: commandNavigationItems.filter(
        (item) => item.group === group.id && item.isAuthorized(user),
      ),
    }))
    .filter((group) => group.items.length > 0);
}

export function getCommandOverviewItems(user: FullUser | undefined): CommandNavigationItem[] {
  return commandNavigationItems.filter((item) => item.showOnOverview && item.isAuthorized(user));
}

export function getCommandNavigationContext(
  pathname: string,
): CommandNavigationContext | undefined {
  const normalized = normalizedPath(pathname);
  return commandNavigationItems
    .flatMap((item) =>
      item.contexts
        .filter((context) => context.matches(normalized))
        .map((context) => ({ item, context })),
    )
    .sort((left, right) => (right.context.priority ?? 0) - (left.context.priority ?? 0))
    .map(({ item, context }) => ({ item, titleKey: context.titleKey }))[0];
}

export function isCommandNavigationItemActive(
  item: CommandNavigationItem,
  pathname: string,
): boolean {
  return item.isActive(normalizedPath(pathname));
}
