import { act, renderHook } from "@testing-library/react";
import { StrictMode, type ReactNode } from "react";
import { I18nextProvider } from "react-i18next";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { useFleetAnnouncements } from "@/features/hosting/useFleetAnnouncements";
import type { GameServer } from "@/features/hosting/types";
import { fleetServer } from "../helpers/fleetFixtures";

/*
 * Polite fleet state-change announcements (UI-SPEC "State-change announcements"). Only state changes that arrive
 * through a refetch are announced: never the first load, new or removed servers, a refetch without changes or a
 * change the viewer caused.
 */

interface HookProps {
  servers: GameServer[] | undefined;
  dataUpdatedAt: number;
  markedAt?: ReadonlyMap<string, number>;
}

const noMarks: ReadonlyMap<string, number> = new Map();

function wrapper({ children }: { children: ReactNode }) {
  return (
    <StrictMode>
      <I18nextProvider i18n={i18n}>{children}</I18nextProvider>
    </StrictMode>
  );
}

function renderAnnouncements(initial: HookProps) {
  return renderHook(
    ({ servers, dataUpdatedAt, markedAt }: HookProps) =>
      useFleetAnnouncements(servers, dataUpdatedAt, markedAt ?? noMarks),
    { initialProps: initial, wrapper },
  );
}

function running(index: number): GameServer {
  return fleetServer({ index, status: "Running" });
}

function stopped(index: number): GameServer {
  return fleetServer({ index, status: "Stopped" });
}

async function switchLanguage(language: "en" | "de"): Promise<void> {
  await act(async () => {
    await i18n.changeLanguage(language);
  });
}

beforeEach(async () => {
  await i18nReady;
  await i18n.changeLanguage("en");
});

afterEach(async () => {
  vi.restoreAllMocks();
  await i18n.changeLanguage("en");
});

describe("useFleetAnnouncements", () => {
  it("announces nothing for the first load or for servers that appear or disappear", () => {
    const loaded = renderAnnouncements({ servers: [running(1)], dataUpdatedAt: 100 });
    expect(loaded.result.current).toBe("");

    const { result, rerender } = renderAnnouncements({ servers: undefined, dataUpdatedAt: 0 });
    expect(result.current).toBe("");
    rerender({ servers: [running(1)], dataUpdatedAt: 100 });
    expect(result.current).toBe("");

    rerender({ servers: [running(1), stopped(2)], dataUpdatedAt: 200 });
    expect(result.current).toBe("");
    rerender({ servers: [stopped(2)], dataUpdatedAt: 300 });
    expect(result.current).toBe("");
  });

  it("announces a changed server with its new state label in English and German", async () => {
    const english = renderAnnouncements({ servers: [running(1), running(2)], dataUpdatedAt: 100 });
    english.rerender({ servers: [stopped(1), running(2)], dataUpdatedAt: 200 });
    expect(english.result.current).toBe("Fleet Server 1 is now Off.");
    english.unmount();

    await switchLanguage("de");
    const german = renderAnnouncements({ servers: [running(1), running(2)], dataUpdatedAt: 100 });
    german.rerender({ servers: [stopped(1), running(2)], dataUpdatedAt: 200 });
    expect(german.result.current).toBe("Fleet Server 1 ist jetzt: Aus.");
  });

  it("announces up to three servers in snapshot order and summarizes the rest", () => {
    const four = renderAnnouncements({
      servers: [running(1), running(2), running(3), running(4)],
      dataUpdatedAt: 100,
    });
    four.rerender({
      servers: [stopped(1), stopped(2), stopped(3), stopped(4)],
      dataUpdatedAt: 200,
    });
    expect(four.result.current).toBe(
      "Fleet Server 1 is now Off. Fleet Server 2 is now Off. Fleet Server 3 is now Off. " +
        "1 more server changed.",
    );

    const five = renderAnnouncements({
      servers: [running(5), running(4), running(3), running(2), running(1)],
      dataUpdatedAt: 100,
    });
    five.rerender({
      servers: [stopped(5), stopped(4), stopped(3), stopped(2), stopped(1)],
      dataUpdatedAt: 200,
    });
    expect(five.result.current).toBe(
      "Fleet Server 5 is now Off. Fleet Server 4 is now Off. Fleet Server 3 is now Off. " +
        "2 more servers changed.",
    );
  });

  it("skips a change the viewer caused but announces a later change of the same server", () => {
    const { result, rerender } = renderAnnouncements({
      servers: [running(1), running(2)],
      dataUpdatedAt: 100,
    });
    const first = fleetServer({ index: 1 }).primaryKey;
    const marks: ReadonlyMap<string, number> = new Map([[first, 100]]);

    rerender({ servers: [stopped(1), running(2)], dataUpdatedAt: 200, markedAt: marks });
    expect(result.current).toBe("");

    rerender({ servers: [running(1), running(2)], dataUpdatedAt: 300, markedAt: marks });
    expect(result.current).toBe("Fleet Server 1 is now Running.");

    rerender({ servers: [stopped(1), stopped(2)], dataUpdatedAt: 400, markedAt: marks });
    expect(result.current).toBe("Fleet Server 1 is now Off. Fleet Server 2 is now Off.");
  });

  it("clears the message on a refetch without new data and replaces it on a new snapshot", () => {
    const changed = [stopped(1), running(2)];
    const { result, rerender } = renderAnnouncements({
      servers: [running(1), running(2)],
      dataUpdatedAt: 100,
    });
    rerender({ servers: changed, dataUpdatedAt: 200 });
    expect(result.current).toBe("Fleet Server 1 is now Off.");

    rerender({ servers: changed, dataUpdatedAt: 200 });
    expect(result.current).toBe("Fleet Server 1 is now Off.");

    rerender({ servers: changed, dataUpdatedAt: 300 });
    expect(result.current).toBe("");

    rerender({ servers: [stopped(1), stopped(2)], dataUpdatedAt: 400 });
    expect(result.current).toBe("Fleet Server 2 is now Off.");
  });

  it("re-renders the existing message in the new language", async () => {
    const { result, rerender } = renderAnnouncements({
      servers: [running(1)],
      dataUpdatedAt: 100,
    });
    rerender({ servers: [stopped(1)], dataUpdatedAt: 200 });
    expect(result.current).toBe("Fleet Server 1 is now Off.");

    await switchLanguage("de");
    expect(result.current).toBe("Fleet Server 1 ist jetzt: Aus.");

    await switchLanguage("en");
    expect(result.current).toBe("Fleet Server 1 is now Off.");
  });
});
