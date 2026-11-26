import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import {
  CostConfirmDialog,
  type CostConfirmDialogProps,
} from "@/components/server/CostConfirmDialog";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, GameServerAccruedCost, ServerAction } from "@/features/hosting/types";

function makeServer(overrides: Partial<GameServer> = {}): GameServer {
  return {
    primaryKey: "server-1",
    title: "Alpha",
    estimatedHourlyCostEur: 0.2,
    estimatedMonthlyCostEur: 144,
    costOwnerName: "Falcon One",
    isLaunchDisabled: false,
    deployment: null,
    actions: [],
    pendingJobCount: 0,
    ...overrides,
  } as GameServer;
}

const startAction = {
  id: "start",
  label: "Start server",
  description: "Starts the server.",
  enabled: true,
  pending: false,
  dangerous: false,
  costImpact: "StartsCost",
  costDescription: "Starts billing.",
  expectedDurationSeconds: null,
} as ServerAction;

const stopAction = {
  ...startAction,
  id: "stop",
  label: "Stop",
  costImpact: "StopsCost",
  expectedDurationSeconds: 120,
} as ServerAction;

const terminateAction = {
  ...stopAction,
  id: "terminate",
  label: "Terminate",
  confirmationChallenge: "Alpha",
} as ServerAction;

/** The accrued cost the dialog reads from the shared query cache; null leaves the cache empty. */
function accruedOf(amount: number | null, isFinal = false): GameServerAccruedCost {
  return {
    gameServerId: "server-1",
    session: {
      deploymentId: "d0000000-0000-4000-8000-000000000001",
      amount,
      currency: "EUR",
      asOf: "2026-11-21T14:05:00Z",
      isFinal,
      unavailableReason: null,
      billingUnitSeconds: null,
      billedThrough: null,
    },
  };
}

function renderDialog(
  props: Partial<CostConfirmDialogProps> = {},
  accrued: GameServerAccruedCost | null = null,
) {
  const onConfirm = vi.fn();
  const onDismiss = vi.fn();
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  if (accrued) queryClient.setQueryData(hostingQueryKeys.accruedCost("server-1"), accrued);
  const view = render(
    <QueryClientProvider client={queryClient}>
      <I18nextProvider i18n={i18n}>
        <CostConfirmDialog
          server={makeServer()}
          action={startAction}
          busy={false}
          requestFailed={false}
          stale={false}
          onConfirm={onConfirm}
          onDismiss={onDismiss}
          {...props}
        />
      </I18nextProvider>
    </QueryClientProvider>,
  );
  return { onConfirm, onDismiss, queryClient, ...view };
}

/* Pins the clock to the fixture day so time-only expectations hold on any run date. */
beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"], shouldAdvanceTime: true });
  vi.setSystemTime(new Date("2026-11-21T14:30:00Z"));
});

afterEach(() => {
  vi.useRealTimers();
});

describe("CostConfirmDialog", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    // A cost-stopping dialog reads the accrued cost; an empty cache must never reach the network in a unit test.
    vi.spyOn(hostingService, "accruedCost").mockResolvedValue({
      gameServerId: "server-1",
      session: null,
    });
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    await act(async () => {
      await i18n.changeLanguage("en");
    });
  });

  it("renders the start confirmation with cost facts and focuses the dismiss button", () => {
    renderDialog();
    const dialog = screen.getByRole("dialog", { name: "Start Alpha?" });
    expect(within(dialog).getByText("Estimated cost")).toBeInTheDocument();
    expect(within(dialog).getByText(/per hour while it runs/)).toBeInTheDocument();
    expect(within(dialog).getByText(/per month if left running/)).toBeInTheDocument();
    expect(within(dialog).getByText("Falcon One")).toBeInTheDocument();
    expect(within(dialog).getByText(/Shut down and stop billing/)).toBeInTheDocument();
    const dismiss = screen.getByRole("button", { name: "Don't start server" });
    expect(dismiss).toHaveFocus();
    const confirm = screen.getByRole("button", { name: "Start server" });
    expect(confirm.className).toContain("button--gold");
    expect(screen.queryByRole("textbox")).toBeNull();
  });

  it("confirms a start without a confirmation text", async () => {
    const { onConfirm } = renderDialog();
    await userEvent.setup().click(screen.getByRole("button", { name: "Start server" }));
    expect(onConfirm).toHaveBeenCalledWith(undefined);
  });

  it("uses the danger variant and the stop sentences, with and without minutes", () => {
    const { unmount } = renderDialog({ action: stopAction });
    expect(screen.getByRole("dialog", { name: "Shut down Alpha and stop billing?" })).toBeVisible();
    expect(screen.getByText(/Stops about/)).toBeInTheDocument();
    expect(screen.getByText(/about 2 minutes/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Shut down and stop billing" }).className).toContain(
      "button--danger",
    );
    unmount();
    renderDialog({ action: { ...stopAction, expectedDurationSeconds: null } as ServerAction });
    expect(screen.queryByText(/minutes/)).toBeNull();
    expect(
      screen.getByText("Billing stops once the provider confirms the machine is gone."),
    ).toBeInTheDocument();
  });

  it("keeps Confirm aria-disabled until the exact challenge is typed, then sends it", async () => {
    const user = userEvent.setup();
    const { onConfirm } = renderDialog({ action: terminateAction });
    const confirm = screen.getByRole("button", { name: "Shut down without saving" });
    expect(confirm).toHaveAttribute("aria-disabled", "true");
    const input = screen.getByRole("textbox");
    await user.type(input, "alpha");
    await user.click(confirm);
    expect(onConfirm).not.toHaveBeenCalled();
    await user.clear(input);
    await user.type(input, "Alpha");
    expect(confirm).not.toHaveAttribute("aria-disabled");
    await user.click(confirm);
    expect(onConfirm).toHaveBeenCalledWith("Alpha");
  });

  it("always renders the facts rows with documented fallbacks", () => {
    const { unmount } = renderDialog({
      server: makeServer({
        estimatedHourlyCostEur: null,
        estimatedMonthlyCostEur: null,
        costOwnerName: "Assigned owner",
      }),
    });
    expect(screen.getByText("Cost estimate not available")).toBeInTheDocument();
    expect(screen.getByText("Assigned owner")).toBeInTheDocument();
    unmount();

    const unknownOwner = renderDialog({ server: makeServer({ costOwnerName: "Unknown owner" }) });
    expect(screen.getByText("Cost owner unknown")).toBeInTheDocument();
    unknownOwner.unmount();

    const restart = renderDialog({
      action: {
        ...startAction,
        id: "restart",
        label: "Restart",
        costImpact: "KeepsCostRunning",
      } as ServerAction,
    });
    expect(screen.getByText("Cost keeps running")).toBeInTheDocument();
    expect(screen.getByRole("dialog", { name: 'Run "Restart game" on Alpha?' })).toBeVisible();
    restart.unmount();

    const logs = renderDialog({
      action: {
        ...startAction,
        id: "collect-logs",
        label: "Collect logs",
        costImpact: "NoCostImpact",
      } as ServerAction,
    });
    expect(screen.queryByText("Estimated cost")).toBeNull();
    expect(screen.getByText("Cost owner")).toBeInTheDocument();
    expect(screen.getByText("How to stop")).toBeInTheDocument();
    logs.unmount();

    renderDialog({
      action: { ...startAction, id: "restart", costImpact: "Unknown" } as ServerAction,
    });
    expect(screen.getByText("Cost impact unknown")).toBeInTheDocument();
  });

  it("marks API text for an unknown action as English and offers a server note", () => {
    renderDialog({
      action: {
        ...startAction,
        id: "rotate-keys",
        label: "Rotate keys",
        costImpact: "NoCostImpact",
        confirmationText: "Keys are replaced.",
      } as ServerAction,
    });
    expect(screen.getByRole("dialog", { name: "Confirm this action on Alpha?" })).toBeVisible();
    const confirm = screen.getByRole("button", { name: "Rotate keys" });
    expect(confirm.querySelector('[lang="en"]')).not.toBeNull();
    expect(screen.getByText("Server note")).toBeInTheDocument();
    const note = screen.getByText(/Keys are replaced\./);
    expect(note).toHaveAttribute("lang", "en");
    expect(screen.getByRole("button", { name: "Don't run this action" })).toBeInTheDocument();
  });

  it("shows no server note for the dedicated actions", () => {
    renderDialog();
    expect(screen.queryByText("Server note")).toBeNull();
  });

  describe("server note disclosure", () => {
    const noteAction = {
      ...startAction,
      id: "rotate-keys",
      label: "Rotate keys",
      costImpact: "NoCostImpact",
      confirmationText: "Keys are replaced.",
    } as ServerAction;

    it("expands the note through a disclosure button instead of a native disclosure", async () => {
      const user = userEvent.setup();
      renderDialog({ action: noteAction });
      const dialog = screen.getByRole("dialog");
      const toggle = within(dialog).getByRole("button", { name: "Server note" });

      expect(dialog.querySelector("details")).toBeNull();
      expect(toggle).toHaveAttribute("aria-expanded", "false");
      expect(within(dialog).getByText(/Keys are replaced\./)).not.toBeVisible();
      await user.click(toggle);
      expect(toggle).toHaveAttribute("aria-expanded", "true");
      expect(within(dialog).getByText(/Keys are replaced\./)).toBeVisible();
      await user.click(toggle);
      expect(toggle).toHaveAttribute("aria-expanded", "false");
    });

    it("keeps initial focus on the dismiss control and the facts block unchanged", () => {
      const withNote = renderDialog({ action: noteAction });
      expect(screen.getByRole("button", { name: "Don't run this action" })).toHaveFocus();
      const facts = screen.getByRole("dialog").querySelector(".cost-confirm__facts");
      const factsHtml = facts?.innerHTML;
      withNote.unmount();

      renderDialog({
        action: {
          ...noteAction,
          confirmationText: null,
          description: null,
          costDescription: null,
        } as unknown as ServerAction,
      });
      expect(screen.queryByRole("button", { name: "Server note" })).toBeNull();
      expect(screen.getByRole("dialog").querySelector(".cost-confirm__facts")?.innerHTML).toBe(
        factsHtml,
      );
    });

    it("reaches the note toggle, dismiss and confirm by Tab in DOM order", async () => {
      const user = userEvent.setup();
      renderDialog({ action: noteAction });
      const dialog = screen.getByRole("dialog");
      const domOrder = within(dialog)
        .getAllByRole("button")
        .map((button) => button.textContent);

      expect(domOrder).toHaveLength(3);
      expect(domOrder).toContain("Server note");
      expect(domOrder).toContain("Don't run this action");
      expect(domOrder).toContain("Rotate keys");
      const visited: (string | null)[] = [];
      within(dialog).getAllByRole("button")[0]!.focus();
      visited.push(document.activeElement?.textContent ?? null);
      for (let step = 0; step < domOrder.length - 1; step += 1) {
        await user.tab();
        visited.push(document.activeElement?.textContent ?? null);
      }
      expect(visited).toEqual(domOrder);
    });
  });

  it("shows pending, request failure and stale states", () => {
    const busy = renderDialog({ busy: true });
    expect(screen.getByRole("dialog")).toHaveAttribute("aria-busy", "true");
    expect(screen.getByRole("button", { name: "Starting..." })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Don't start server" })).toBeDisabled();
    busy.unmount();

    const failed = renderDialog({ requestFailed: true });
    expect(screen.getByRole("alert")).toHaveTextContent(
      "The action could not be queued. Try again, or open the server page to check its current state.",
    );
    expect(screen.getByRole("button", { name: "Start server" })).not.toHaveAttribute(
      "aria-disabled",
    );
    failed.unmount();

    renderDialog({
      stale: true,
      action: {
        ...startAction,
        enabled: false,
        unavailableReason: "Another lifecycle operation is pending.",
      } as ServerAction,
    });
    expect(screen.getByRole("button", { name: "Start server" })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
    expect(screen.getByRole("dialog").querySelector(".cost-confirm__stale")).toHaveTextContent(
      /another/i,
    );
    expect(screen.queryByRole("button", { name: /cancel|abbrechen/i })).toBeNull();
  });

  it("localizes the dismiss label and the hourly rate in German", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });
    renderDialog();
    expect(screen.getByRole("button", { name: "Server nicht starten" })).toBeInTheDocument();
    // Testing Library normalizes whitespace in text queries, so read the raw text content.
    expect(document.querySelector(".cost-confirm__amount")?.textContent).toContain("0,20 €");
  });

  describe("session so far", () => {
    const clock = (instant: string) =>
      new Intl.DateTimeFormat("en-US", { timeStyle: "short" }).format(new Date(instant));
    const money = (value: number) =>
      new Intl.NumberFormat("en-US", { style: "currency", currency: "EUR" }).format(value);

    it("shows the amount with its as-of time for a cost-stopping action", () => {
      renderDialog({ action: stopAction }, accruedOf(1.31));
      const dialog = screen.getByRole("dialog", { name: "Shut down Alpha and stop billing?" });

      expect(within(dialog).getByText("This session so far")).toBeInTheDocument();
      expect(
        within(dialog).getByText(`${money(1.31)}, as of ${clock("2026-11-21T14:05:00Z")}`),
      ).toBeInTheDocument();
    });

    it("shows no such fact for a start action", () => {
      renderDialog({ action: startAction }, accruedOf(1.31));
      expect(screen.queryByText("This session so far")).toBeNull();
    });

    it("shows no such fact while the amount is unknown or there is no session", () => {
      const { unmount } = renderDialog({ action: stopAction }, accruedOf(null));
      expect(screen.queryByText("This session so far")).toBeNull();
      unmount();
      renderDialog({ action: stopAction }, { gameServerId: "server-1", session: null });
      expect(screen.queryByText("This session so far")).toBeNull();
    });

    it("formats a tiny amount like the strip while cost runs", () => {
      renderDialog({ action: stopAction }, accruedOf(0.004));
      expect(
        screen.getByText(`Less than ${money(0.01)}, as of ${clock("2026-11-21T14:05:00Z")}`),
      ).toBeInTheDocument();
    });

    it("follows a newer value written into the cache", async () => {
      const { queryClient } = renderDialog({ action: stopAction }, accruedOf(1.31));
      expect(screen.getByText(/1\.31/)).toBeInTheDocument();

      act(() => {
        queryClient.setQueryData(hostingQueryKeys.accruedCost("server-1"), accruedOf(2.4));
      });

      await waitFor(() => expect(screen.getByText(/2\.40/)).toBeInTheDocument());
    });
  });
});
