import { FluentProvider, webDarkTheme, type Theme } from "@fluentui/react-components";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useMemo, type ComponentType } from "react";
import { createBrowserRouter, RouterProvider, type RouteObject } from "react-router";
import type { PublicConfiguration } from "@/app/config";
import { routeManifest } from "@/app/routes";
import { SiteImagesProvider } from "@/app/siteImages";
import { AuthProvider } from "@/auth/auth";
import { NotFoundPage } from "@/components/NotFoundPage";
import { CommandCenterLayout } from "@/components/CommandCenterLayout";
import { CommunityLayout } from "@/components/CommunityLayout";

const tacticalTheme: Theme = {
  ...webDarkTheme,
  colorBrandBackground: "#baa05a",
  colorBrandBackgroundHover: "#ceb66f",
  colorBrandBackgroundPressed: "#a68d4d",
  colorBrandForeground1: "#dec982",
  colorNeutralBackground1: "#0d1118",
  colorNeutralBackground2: "#121822",
  colorNeutralBackground3: "#1a2230",
  colorNeutralForeground1: "#f4f1e8",
  colorNeutralForeground2: "#b8bec9",
  colorNeutralStroke1: "#2a3442",
};

function lazyComponent(module: () => Promise<Record<string, unknown>>, exportName = "Component") {
  return async () => {
    const loaded = await module();
    return { Component: loaded[exportName] as ComponentType };
  };
}

const communityRouteDefinitions: { manifestPattern: string; route: RouteObject }[] = [
  {
    manifestPattern: "/",
    route: { index: true, lazy: lazyComponent(() => import("@/pages/HomePage")) },
  },
  {
    manifestPattern: "/events/upcoming",
    route: {
      path: "events/upcoming",
      lazy: lazyComponent(() => import("@/pages/UpcomingEventsPage")),
    },
  },
  {
    manifestPattern: "/events/{EventId:guid}",
    route: {
      path: "events/:eventId",
      lazy: lazyComponent(() => import("@/features/events-member"), "EventDetailPage"),
    },
  },
  {
    manifestPattern: "/wiki",
    route: { path: "wiki", lazy: lazyComponent(() => import("@/features/wiki/WikiPage")) },
  },
  {
    manifestPattern: "/wiki/{PageId:guid}",
    route: {
      path: "wiki/:pageId",
      lazy: lazyComponent(() => import("@/features/wiki/WikiPage")),
    },
  },
  {
    manifestPattern: "/authentication/{action}",
    route: {
      path: "authentication/:action",
      lazy: lazyComponent(() => import("@/pages/AuthenticationPage")),
    },
  },
  {
    manifestPattern: "/field-reports",
    route: { path: "field-reports", lazy: lazyComponent(() => import("@/pages/FieldReportsPage")) },
  },
  {
    manifestPattern: "/media",
    route: { path: "media", lazy: lazyComponent(() => import("@/pages/FieldReportsPage")) },
  },
  {
    manifestPattern: "/field-reports/{Slug}",
    route: {
      path: "field-reports/:slug",
      lazy: lazyComponent(() => import("@/pages/FieldReportDetailPage")),
    },
  },
  {
    manifestPattern: "/media/{Slug}",
    route: {
      path: "media/:slug",
      lazy: lazyComponent(() => import("@/pages/FieldReportDetailPage")),
    },
  },
  {
    manifestPattern: "/unit",
    route: { path: "unit", lazy: lazyComponent(() => import("@/pages/UnitPage")) },
  },
  {
    manifestPattern: "/gdpr",
    route: { path: "gdpr", lazy: lazyComponent(() => import("@/pages/GdprPage")) },
  },
  {
    manifestPattern: "/join",
    route: { path: "join", lazy: lazyComponent(() => import("@/pages/JoinPage")) },
  },
  {
    manifestPattern: "/settings",
    route: {
      path: "settings",
      lazy: lazyComponent(() => import("@/features/events-member"), "AccountSettingsPage"),
    },
  },
  {
    manifestPattern: "/members",
    route: { path: "members", lazy: lazyComponent(() => import("@/pages/JoinPage")) },
  },
  {
    manifestPattern: "/verein",
    route: {
      path: "verein",
      lazy: lazyComponent(() => import("@/pages/VereinPages"), "VereinPage"),
    },
  },
  {
    manifestPattern: "/verein/kassenbuch",
    route: {
      path: "verein/kassenbuch",
      lazy: lazyComponent(() => import("@/features/events-member"), "VereinLedgerPage"),
    },
  },
  {
    manifestPattern: "/unit/kassenbuch",
    route: {
      path: "unit/kassenbuch",
      lazy: lazyComponent(() => import("@/features/events-member"), "VereinLedgerPage"),
    },
  },
  {
    manifestPattern: "/verein/satzung",
    route: {
      path: "verein/satzung",
      lazy: lazyComponent(() => import("@/pages/VereinPages"), "StatutesPage"),
    },
  },
  {
    manifestPattern: "/unit/satzung",
    route: {
      path: "unit/satzung",
      lazy: lazyComponent(() => import("@/pages/VereinPages"), "StatutesPage"),
    },
  },
  {
    manifestPattern: "/verein/beschluesse",
    route: {
      path: "verein/beschluesse",
      lazy: lazyComponent(() => import("@/pages/VereinPages"), "MinutesPage"),
    },
  },
  {
    manifestPattern: "/unit/beschluesse",
    route: {
      path: "unit/beschluesse",
      lazy: lazyComponent(() => import("@/pages/VereinPages"), "MinutesPage"),
    },
  },
  {
    manifestPattern: "/verein/beschluesse/{DocumentId:long}",
    route: {
      path: "verein/beschluesse/:documentId",
      lazy: lazyComponent(() => import("@/pages/VereinPages"), "MinuteDetailPage"),
    },
  },
  {
    manifestPattern: "/unit/beschluesse/{DocumentId:long}",
    route: {
      path: "unit/beschluesse/:documentId",
      lazy: lazyComponent(() => import("@/pages/VereinPages"), "MinuteDetailPage"),
    },
  },
];

const commandRouteDefinitions: { manifestPattern: string; route: RouteObject }[] = [
  {
    manifestPattern: "/command",
    route: { index: true, lazy: lazyComponent(() => import("@/pages/CommandOverviewPage")) },
  },
  {
    manifestPattern: "/command/content",
    route: {
      path: "content",
      lazy: lazyComponent(() => import("@/features/admin/ContentHubPage")),
    },
  },
  {
    manifestPattern: "/command/content/pages",
    route: {
      path: "content/pages",
      lazy: lazyComponent(() => import("@/features/admin/PublicPagesPage")),
    },
  },
  {
    manifestPattern: "/command/content/pages/{Slug}",
    route: {
      path: "content/pages/:slug",
      lazy: lazyComponent(() => import("@/features/admin/PublicPageDetailPage")),
    },
  },
  {
    manifestPattern: "/command/content/pages/{Slug}/edit",
    route: {
      path: "content/pages/:slug/edit",
      lazy: lazyComponent(() => import("@/features/admin/PublicPageEditorPage")),
    },
  },
  {
    manifestPattern: "/command/content/gallery",
    route: {
      path: "content/gallery",
      lazy: lazyComponent(() => import("@/features/admin/MediaGalleryPage")),
    },
  },
  {
    manifestPattern: "/command/content/images",
    route: {
      path: "content/images",
      lazy: lazyComponent(() => import("@/features/admin/SiteImagesPage")),
    },
  },
  {
    manifestPattern: "/command/content/images/{Group}",
    route: {
      path: "content/images/:group",
      lazy: lazyComponent(() => import("@/features/admin/SiteImagesPage")),
    },
  },
  {
    manifestPattern: "/command/events",
    route: {
      path: "events",
      lazy: lazyComponent(() => import("@/features/admin/EventAdminPage")),
    },
  },
  {
    manifestPattern: "/command/events/create",
    route: {
      path: "events/create",
      lazy: lazyComponent(() => import("@/features/admin/EventCreatePage")),
    },
  },
  {
    manifestPattern: "/command/events/{EventId:guid}",
    route: {
      path: "events/:eventId",
      lazy: lazyComponent(() => import("@/features/admin/EventEditPage")),
    },
  },
  {
    manifestPattern: "/command/field-reports",
    route: {
      path: "field-reports",
      lazy: lazyComponent(() => import("@/features/admin/FieldReportsPage")),
    },
  },
  {
    manifestPattern: "/command/field-reports/create",
    route: {
      path: "field-reports/create",
      lazy: lazyComponent(() => import("@/features/admin/FieldReportEditorPage")),
    },
  },
  {
    manifestPattern: "/command/field-reports/{ReportId:guid}",
    route: {
      path: "field-reports/:reportId",
      lazy: lazyComponent(() => import("@/features/admin/FieldReportEditorPage")),
    },
  },
  {
    manifestPattern: "/command/members",
    route: {
      path: "members",
      lazy: lazyComponent(() => import("@/features/admin/MembersPage")),
    },
  },
  {
    manifestPattern: "/command/members/{UserId:guid}",
    route: {
      path: "members/:userId",
      lazy: lazyComponent(() => import("@/features/admin/MemberDetailPage")),
    },
  },
  {
    manifestPattern: "/command/claims",
    route: {
      path: "claims",
      lazy: lazyComponent(() => import("@/features/admin/ClaimsPage")),
    },
  },
  {
    manifestPattern: "/command/roles",
    route: {
      path: "roles",
      lazy: lazyComponent(() => import("@/features/admin/RolesPage")),
    },
  },
  {
    manifestPattern: "/command/roles/{RoleId:long}",
    route: {
      path: "roles/:roleId",
      lazy: lazyComponent(() => import("@/features/admin/RoleDetailPage")),
    },
  },
  {
    manifestPattern: "/command/terrains",
    route: {
      path: "terrains",
      lazy: lazyComponent(() => import("@/features/admin/TerrainsPage")),
    },
  },
  {
    manifestPattern: "/command/terrains/create",
    route: {
      path: "terrains/create",
      lazy: lazyComponent(() => import("@/features/admin/TerrainCreatePage")),
    },
  },
  {
    manifestPattern: "/command/terrains/{TerrainId:long}",
    route: {
      path: "terrains/:terrainId",
      lazy: lazyComponent(() => import("@/features/admin/TerrainDetailPage")),
    },
  },
  {
    manifestPattern: "/command/mod-packs",
    route: {
      path: "mod-packs",
      lazy: lazyComponent(() => import("@/features/admin/ModPacksPage")),
    },
  },
  {
    manifestPattern: "/command/mod-packs/create",
    route: {
      path: "mod-packs/create",
      lazy: lazyComponent(() => import("@/features/admin/ModPackCreatePage")),
    },
  },
  {
    manifestPattern: "/command/mod-packs/{ModPackId:long}",
    route: {
      path: "mod-packs/:modPackId",
      lazy: lazyComponent(() => import("@/features/admin/ModPackWorkspacePage")),
    },
  },
  {
    manifestPattern: "/command/mod-packs/{ModPackId:long}/{Section}",
    route: {
      path: "mod-packs/:modPackId/:section",
      lazy: lazyComponent(() => import("@/features/admin/ModPackWorkspacePage")),
    },
  },
  {
    manifestPattern: "/command/settings",
    route: {
      path: "settings",
      lazy: lazyComponent(() => import("@/features/admin/SettingsPage")),
    },
  },
  {
    manifestPattern: "/command/verein/settings",
    route: {
      path: "verein/settings",
      lazy: lazyComponent(() => import("@/features/admin/VereinSettingsPage")),
    },
  },
  {
    manifestPattern: "/command/verein/membership-requests",
    route: {
      path: "verein/membership-requests",
      lazy: lazyComponent(() => import("@/features/admin/VereinMembershipRequestsPage")),
    },
  },
  {
    manifestPattern: "/command/verein/kassenbuch",
    route: {
      path: "verein/kassenbuch",
      lazy: lazyComponent(() => import("@/features/admin/VereinKassenbuchPage")),
    },
  },
  {
    manifestPattern: "/command/verein/kassenbuch/create",
    route: {
      path: "verein/kassenbuch/create",
      lazy: lazyComponent(() => import("@/features/admin/VereinKassenbuchEditorPage")),
    },
  },
  {
    manifestPattern: "/command/verein/kassenbuch/{EntryId:long}/edit",
    route: {
      path: "verein/kassenbuch/:entryId/edit",
      lazy: lazyComponent(() => import("@/features/admin/VereinKassenbuchEditorPage")),
    },
  },
  {
    manifestPattern: "/command/verein/documents",
    route: {
      path: "verein/documents",
      lazy: lazyComponent(() => import("@/features/admin/VereinDocumentsPage")),
    },
  },
  {
    manifestPattern: "/command/verein/documents/create",
    route: {
      path: "verein/documents/create",
      lazy: lazyComponent(() => import("@/features/admin/VereinDocumentEditorPage")),
    },
  },
  {
    manifestPattern: "/command/verein/documents/{DocumentId:long}",
    route: {
      path: "verein/documents/:documentId",
      lazy: lazyComponent(() => import("@/features/admin/VereinDocumentDetailPage")),
    },
  },
  {
    manifestPattern: "/command/verein/documents/{DocumentId:long}/edit",
    route: {
      path: "verein/documents/:documentId/edit",
      lazy: lazyComponent(() => import("@/features/admin/VereinDocumentEditorPage")),
    },
  },
  {
    manifestPattern: "/command/settings/verein",
    route: {
      path: "settings/verein",
      lazy: lazyComponent(() => import("@/features/admin/VereinSettingsPage")),
    },
  },
  {
    manifestPattern: "/command/association/settings",
    route: {
      path: "association/settings",
      lazy: lazyComponent(() => import("@/features/admin/VereinSettingsPage")),
    },
  },
  {
    manifestPattern: "/command/servers",
    route: {
      path: "servers",
      lazy: lazyComponent(() => import("@/features/hosting"), "ServerFleetPage"),
    },
  },
  {
    manifestPattern: "/command/servers/create",
    route: {
      path: "servers/create",
      lazy: lazyComponent(() => import("@/features/hosting"), "CreateServerPage"),
    },
  },
  {
    manifestPattern: "/command/servers/{ServerId:guid}",
    route: {
      path: "servers/:serverId",
      lazy: lazyComponent(() => import("@/features/hosting"), "ServerWorkspacePage"),
    },
  },
  {
    manifestPattern: "/command/configuration-profiles",
    route: {
      path: "configuration-profiles",
      lazy: lazyComponent(() => import("@/features/hosting"), "ConfigurationProfilesPage"),
    },
  },
  {
    manifestPattern: "/command/hosting-finance",
    route: {
      path: "hosting-finance",
      lazy: lazyComponent(() => import("@/features/hosting"), "HostingFinancePage"),
    },
  },
];

const commandAliasRouteDefinitions: {
  manifestPattern: string;
  path: string;
  route: RouteObject;
}[] = [
  {
    manifestPattern: "/events/all",
    path: "events/all",
    route: {
      index: true,
      lazy: lazyComponent(() => import("@/features/admin/EventAdminPage")),
    },
  },
  {
    manifestPattern: "/events/create",
    path: "events/create",
    route: {
      index: true,
      lazy: lazyComponent(() => import("@/features/admin/EventCreatePage")),
    },
  },
  {
    manifestPattern: "/command/wiki-media",
    path: "command/wiki-media",
    route: {
      index: true,
      lazy: lazyComponent(() => import("@/features/admin/FieldReportsPage")),
    },
  },
  {
    manifestPattern: "/data/terrains/all",
    path: "data/terrains/all",
    route: {
      index: true,
      lazy: lazyComponent(() => import("@/features/admin/TerrainsPage")),
    },
  },
  {
    manifestPattern: "/data/terrains/create",
    path: "data/terrains/create",
    route: {
      index: true,
      lazy: lazyComponent(() => import("@/features/admin/TerrainCreatePage")),
    },
  },
  {
    manifestPattern: "/data/terrains/{TerrainId:long}",
    path: "data/terrains/:terrainId",
    route: {
      index: true,
      lazy: lazyComponent(() => import("@/features/admin/TerrainDetailPage")),
    },
  },
  {
    manifestPattern: "/data/mod-packs/all",
    path: "data/mod-packs/all",
    route: {
      index: true,
      lazy: lazyComponent(() => import("@/features/admin/ModPacksPage")),
    },
  },
  {
    manifestPattern: "/data/mod-packs/create",
    path: "data/mod-packs/create",
    route: {
      index: true,
      lazy: lazyComponent(() => import("@/features/admin/ModPackCreatePage")),
    },
  },
  {
    manifestPattern: "/data/mod-packs/{ModPackId:long}",
    path: "data/mod-packs/:modPackId",
    route: {
      index: true,
      lazy: lazyComponent(() => import("@/features/admin/ModPackWorkspacePage")),
    },
  },
];

export const implementedReactRoutePatterns = [
  ...communityRouteDefinitions,
  ...commandRouteDefinitions,
  ...commandAliasRouteDefinitions,
].map(({ manifestPattern }) => manifestPattern);

function createRoutes(configuration: PublicConfiguration): RouteObject[] {
  return [
    {
      path: "/",
      element: <CommunityLayout configuration={configuration} />,
      children: [
        ...communityRouteDefinitions.map(({ route }) => route),
        { path: "*", element: <NotFoundPage /> },
      ],
    },
    {
      path: "command",
      element: <CommandCenterLayout configuration={configuration} />,
      children: commandRouteDefinitions.map(({ route }) => route),
    },
    ...commandAliasRouteDefinitions.map(({ path, route }) => ({
      path,
      element: <CommandCenterLayout configuration={configuration} />,
      children: [route],
    })),
    { path: "command/*", element: <NotFoundPage /> },
  ];
}

export function App({ configuration }: { configuration: PublicConfiguration }) {
  const queryClient = useMemo(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            retry: (failureCount, error) =>
              !(
                error instanceof Error &&
                "status" in error &&
                [401, 403, 404].includes(Number((error as { status: unknown }).status))
              ) && failureCount < 2,
            refetchOnWindowFocus: false,
          },
          mutations: { retry: false },
        },
      }),
    [],
  );
  const router = useMemo(
    () =>
      createBrowserRouter(createRoutes(configuration), { basename: routeManifest.reactBasePath }),
    [configuration],
  );
  return (
    <FluentProvider theme={tacticalTheme}>
      <QueryClientProvider client={queryClient}>
        <SiteImagesProvider>
          <AuthProvider>
            <RouterProvider router={router} />
          </AuthProvider>
        </SiteImagesProvider>
      </QueryClientProvider>
    </FluentProvider>
  );
}
