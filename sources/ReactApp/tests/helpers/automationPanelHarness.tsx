import { QueryClient, useQuery } from "@tanstack/react-query";
import { screen } from "@testing-library/react";
import { vi } from "vitest";
import { SettingsPanelStateProvider } from "@/components/server/useSettingsPanelState";
import { AutomationPanel } from "@/features/hosting/AutomationPanel";
import { hostingQueryKeys } from "@/features/hosting/services";
import type { GameServer, ServerAutomation, ServerSchedule } from "@/features/hosting/types";
import { fleetServer, serverAutomation } from "./fleetFixtures";
import { renderWithProviders } from "./renderWithProviders";

/**
 * The automation block with the given saved settings members and top-level members replaced.
 *
 * @param settings Members of the saved settings to replace.
 * @param overrides Top-level members of the automation block to replace.
 * @returns A fresh automation block.
 */
export function automationWith(
  settings: Partial<ServerAutomation["settings"]> = {},
  overrides: Partial<ServerAutomation> = {},
): ServerAutomation {
  const base = serverAutomation(overrides);
  return { ...base, settings: { ...base.settings, ...settings } };
}

/**
 * A saved weekly rule as the API sends it.
 *
 * @param overrides Members to replace.
 * @returns A saved rule that starts the server on Friday at 18:00 and has not run.
 */
export function savedRule(overrides: Partial<ServerSchedule> = {}): ServerSchedule {
  return {
    id: "a0000000-0000-4000-8000-000000000001",
    action: "Start",
    days: [5],
    time: "18:00",
    nextRunAt: "2026-09-04T16:00:00Z",
    lastRun: null,
    ...overrides,
  } as ServerSchedule;
}

/** Options of {@link managedServer}. */
export interface ManagedServerOptions {
  /** The automation block; defaults to nothing saved. */
  automation?: ServerAutomation | null;
  /** Whether the viewer may manage the automation; defaults to true. */
  canManage?: boolean | null;
}

/**
 * A server whose automation the viewer may manage unless the options say otherwise.
 *
 * @param options The automation block and the right of the viewer.
 * @returns The server.
 */
export function managedServer(options: ManagedServerOptions = {}): GameServer {
  return fleetServer({
    automation: options.automation === undefined ? automationWith() : options.automation,
    canManageAutomation: options.canManage === undefined ? true : options.canManage,
  });
}

/** Hosts the panel on the server query, so a save or a refetch changes the server the panel receives. */
function Harness({
  server,
  onDirtyChange,
  registerDiscard,
  queryClient,
}: {
  server: GameServer;
  onDirtyChange: (dirty: boolean) => void;
  registerDiscard: (discard: () => void) => void;
  queryClient: QueryClient;
}) {
  const key = hostingQueryKeys.server(server.primaryKey);
  const query = useQuery({
    queryKey: key,
    queryFn: () => Promise.resolve(queryClient.getQueryData<GameServer>(key) ?? server),
    staleTime: Infinity,
  });
  return (
    <SettingsPanelStateProvider>
      <AutomationPanel
        server={query.data ?? server}
        onDirtyChange={onDirtyChange}
        registerDiscard={registerDiscard}
      />
    </SettingsPanelStateProvider>
  );
}

/**
 * Renders the panel for the given server.
 *
 * @param server The server to show.
 * @returns The render result, the query client and the spies of the two host callbacks.
 */
export function renderPanel(server: GameServer = managedServer()) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  queryClient.setQueryData(hostingQueryKeys.server(server.primaryKey), server);
  const onDirtyChange = vi.fn();
  const registerDiscard = vi.fn();
  const view = renderWithProviders(
    <Harness
      server={server}
      onDirtyChange={onDirtyChange}
      registerDiscard={registerDiscard}
      queryClient={queryClient}
    />,
    { queryClient },
  );
  return { ...view, queryClient, onDirtyChange, registerDiscard, server };
}

/** The Save button of the panel, also while it reads "Saving...". */
export const saveButton = () =>
  screen.getByRole("button", { name: /^(Save automation settings|Saving\.\.\.)$/ });
