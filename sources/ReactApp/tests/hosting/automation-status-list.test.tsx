import { render, screen, within } from "@testing-library/react";
import { I18nextProvider } from "react-i18next";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { AutomationStatusList } from "@/components/server/AutomationStatusList";
import { formatInstant } from "@/features/hosting/costAlertFormat";
import type { ServerAutomation } from "@/features/hosting/types";
import { serverAutomation } from "../helpers/fleetFixtures";

/* The always-rendered status list of the saved automation state (D-12, E1 states). */

const since = "2030-03-04T10:00:00Z";
const stops = "2030-03-04T10:30:00Z";
const nextAt = "2030-03-04T14:30:00Z";

/** The automation block with settings and status members replaced. */
function automationWith(
  settings: Partial<ServerAutomation["settings"]> = {},
  status: Partial<ServerAutomation["status"]> = {},
  overrides: Partial<ServerAutomation> = {},
): ServerAutomation {
  const base = serverAutomation(overrides);
  return {
    ...base,
    settings: { ...base.settings, ...settings },
    status: { ...base.status, ...status },
  };
}

/** Renders the list for the given automation block. */
function show(automation: ServerAutomation, locale = "en-US") {
  return render(
    <I18nextProvider i18n={i18n}>
      <AutomationStatusList automation={automation} locale={locale} />
    </I18nextProvider>,
  );
}

/** The text of the value that follows the given term. */
function valueOf(term: string): HTMLElement {
  const dt = screen.getByText(term, { selector: "dt" });
  const dd = dt.nextElementSibling;
  if (!(dd instanceof HTMLElement)) throw new Error(`No value follows ${term}.`);
  return dd;
}

describe("Automation status list", () => {
  beforeAll(async () => i18nReady);
  beforeEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("always renders three labelled rows, with Off and No schedules for an unset server", () => {
    const { container } = show(serverAutomation());

    const list = container.querySelector("dl");
    expect(list).toHaveAttribute("aria-label", "Current automation status");
    expect(container.querySelectorAll("dt")).toHaveLength(3);
    expect(container.querySelectorAll("dd")).toHaveLength(3);
    expect(valueOf("Idle shutdown")).toHaveTextContent("Off");
    expect(valueOf("Maximum runtime")).toHaveTextContent("Off");
    expect(valueOf("Next scheduled action")).toHaveTextContent("No schedules.");
    expect(list).not.toHaveAttribute("aria-live");
  });

  it("says idle shutdown is not available for a game without a players signal", () => {
    show(automationWith({ idleShutdownEnabled: true }, {}, { idleShutdown: { available: false } }));

    expect(valueOf("Idle shutdown")).toHaveTextContent("Not available for this game.");
  });

  it("writes the idle row for armed, players online and running", () => {
    const armed = automationWith({ idleShutdownEnabled: true, idleShutdownMinutes: 45 });
    const { unmount } = show(armed);
    expect(valueOf("Idle shutdown")).toHaveTextContent("After 45 minutes without players.");
    unmount();

    const players = automationWith(
      { idleShutdownEnabled: true },
      { idle: { state: "PlayersOnline", since: null, stopsAt: null } },
    );
    const second = show(players);
    expect(valueOf("Idle shutdown")).toHaveTextContent(
      "Players are online. The idle timer is not running.",
    );
    second.unmount();

    const idle = automationWith(
      { idleShutdownEnabled: true },
      { idle: { state: "Idle", since, stopsAt: stops } },
    );
    show(idle);
    const value = valueOf("Idle shutdown");
    const times = within(value).getAllByText(/\d/, { selector: "time" });
    expect(times).toHaveLength(2);
    expect(times[0]).toHaveAttribute("datetime", since);
    expect(times[0]).toHaveTextContent(formatInstant(since));
    expect(times[1]).toHaveAttribute("datetime", stops);
    expect(value.textContent).toBe(
      `Idle since ${formatInstant(since)}. Shuts down at ${formatInstant(stops)} if nobody joins.`,
    );
  });

  it("shows the fixed unknown sentence for Unknown and for an idle state a newer API adds", () => {
    const unknown = automationWith(
      { idleShutdownEnabled: true },
      { idle: { state: "Unknown", since: null, stopsAt: null } },
    );
    const { unmount } = show(unknown);
    const sentence =
      "The player count is not available right now. The idle timer waits and never shuts the server down on a guess.";
    expect(valueOf("Idle shutdown")).toHaveTextContent(sentence);
    unmount();

    const future = automationWith(
      { idleShutdownEnabled: true },
      { idle: { state: "Hibernating" as never, since: null, stopsAt: null } },
    );
    show(future);
    expect(valueOf("Idle shutdown")).toHaveTextContent(sentence);
    expect(screen.queryByText(/Hibernating/)).not.toBeInTheDocument();
  });

  it("writes the maximum runtime row: armed, or the stop instant the API sent", () => {
    const armed = automationWith({ maxRuntimeEnabled: true, maxRuntimeHours: 8 });
    const { unmount } = show(armed);
    expect(valueOf("Maximum runtime")).toHaveTextContent("After 8 hours of running.");
    unmount();

    const running = automationWith({ maxRuntimeEnabled: true }, { maxRuntimeStopsAt: stops });
    show(running);
    const value = valueOf("Maximum runtime");
    expect(value.textContent).toBe(`Shuts down at ${formatInstant(stops)} at the latest.`);
    expect(within(value).getByText(formatInstant(stops))).toHaveAttribute("datetime", stops);
  });

  it("writes the next scheduled action with weekday, date and time in the saved zone", () => {
    const automation = automationWith(
      { timeZone: "Europe/Berlin" },
      { nextScheduled: { action: "Start", at: nextAt } },
    );
    show(automation);

    const value = valueOf("Next scheduled action");
    expect(value).toHaveTextContent(/^Start server: Mon, Mar 4, 3:30\sPM \(Europe\/Berlin\)$/);
    expect(value.querySelector("time")).toHaveAttribute("datetime", nextAt);
  });

  it("shows the saved settings even while a draft differs, because it only reads the saved block", () => {
    const saved = automationWith({ idleShutdownEnabled: false });
    show(saved);

    expect(valueOf("Idle shutdown")).toHaveTextContent("Off");
  });

  it("writes German sentences and the German weekday format", async () => {
    await i18n.changeLanguage("de");
    const automation = automationWith(
      { timeZone: "Europe/Berlin", idleShutdownEnabled: true, idleShutdownMinutes: 45 },
      { nextScheduled: { action: "Stop", at: nextAt } },
    );
    show(automation, "de-DE");

    expect(valueOf("Herunterfahren bei Leerlauf")).toHaveTextContent(
      "Nach 45 Minuten ohne Spieler.",
    );
    expect(valueOf("Maximale Laufzeit")).toHaveTextContent("Aus");
    expect(valueOf("Nächste geplante Aktion")).toHaveTextContent(
      /^Herunterfahren und Abrechnung beenden: Mo\.?,? .*15:30 \(Europe\/Berlin\)$/,
    );
  });
});
