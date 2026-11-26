import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState, type ReactNode } from "react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import { SettingsPanel } from "@/components/server/SettingsPanel";
import {
  SettingsPanelStateProvider,
  settingsPanelStorageKey,
  useSettingsPanelState,
} from "@/components/server/useSettingsPanelState";
import { clearSettingsPanels, seedSettingsPanels } from "./helpers/settingsPanels";

/* The collapsible Settings panel and its open state: first-visit default, storage, staged opening and the hash. */

/** Exposes Expand all, Collapse all and the collapsible-open flag of the surrounding provider. */
function StateProbe() {
  const state = useSettingsPanelState();
  return (
    <div>
      <button type="button" onClick={state.expandAll}>
        probe-expand
      </button>
      <button type="button" onClick={state.collapseAll}>
        probe-collapse
      </button>
      <output data-testid="any-open">{String(state.anyCollapsibleOpen)}</output>
    </div>
  );
}

/** Mounts the children under the provider and a memory router at the given location. */
function renderUnderProvider(children: ReactNode, route = "/") {
  const router = createMemoryRouter(
    [{ path: "*", element: <SettingsPanelStateProvider>{children}</SettingsPanelStateProvider> }],
    { initialEntries: [route] },
  );
  return { router, ...render(<RouterProvider router={router} />) };
}

/** A panel with one field, so a test can see whether its content survives a collapse. */
function Panel({
  id,
  title,
  stagedKeys,
}: {
  id: string;
  title: string;
  stagedKeys?: readonly string[];
}) {
  return (
    <SettingsPanel id={id} title={title} summary={`${title} summary`} stagedKeys={stagedKeys}>
      <input aria-label={`${title} field`} />
    </SettingsPanel>
  );
}

/** Two panels, the first with staged keys the test changes through buttons, plus the state probe. */
function StagingHost() {
  const [keys, setKeys] = useState<readonly string[]>([]);
  return (
    <>
      <button type="button" onClick={() => setKeys(["a"])}>
        stage-a
      </button>
      <button type="button" onClick={() => setKeys(["a", "b"])}>
        stage-ab
      </button>
      <button type="button" onClick={() => setKeys([])}>
        unstage
      </button>
      <Panel id="server-a" title="Alpha" stagedKeys={keys} />
      <Panel id="server-b" title="Beta" />
      <StateProbe />
    </>
  );
}

/** The toggle button in the header of a panel. */
const header = (name: string) => screen.getByRole("button", { name });

/** A button of the test hosts by its name. */
const control = (name: string) => screen.getByRole("button", { name });

describe("SettingsPanel", () => {
  beforeEach(() => clearSettingsPanels());

  it("renders collapsed with its anchor, a heading and a hidden region on the first visit", () => {
    const { container } = renderUnderProvider(<Panel id="server-a" title="Alpha" />);

    const section = container.querySelector("section#server-a");
    expect(section).toHaveAttribute("data-settings-panel", "server-a");
    expect(section).toHaveClass("workspace-panel", "settings-panel");
    expect(screen.getByRole("heading", { level: 2, name: "Alpha" })).toBeInTheDocument();
    expect(header("Alpha")).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByText("Alpha summary")).toBeInTheDocument();
    expect(screen.getByLabelText("Alpha field", { selector: "input" })).not.toBeVisible();
  });

  it("toggles by click and stores the open anchor ids as a JSON array", async () => {
    const user = userEvent.setup();
    renderUnderProvider(
      <>
        <Panel id="server-a" title="Alpha" />
        <Panel id="server-b" title="Beta" />
      </>,
    );
    const stored = () => JSON.parse(sessionStorage.getItem(settingsPanelStorageKey) ?? "null");

    await user.click(header("Beta"));
    expect(header("Beta")).toHaveAttribute("aria-expanded", "true");
    expect(stored()).toEqual(["server-b"]);

    await user.click(header("Alpha"));
    await user.click(header("Beta"));
    expect(stored()).toEqual(["server-a"]);
  });

  it("starts from the stored open ids", () => {
    seedSettingsPanels(["server-b"]);
    renderUnderProvider(
      <>
        <Panel id="server-a" title="Alpha" />
        <Panel id="server-b" title="Beta" />
      </>,
    );

    expect(header("Alpha")).toHaveAttribute("aria-expanded", "false");
    expect(header("Beta")).toHaveAttribute("aria-expanded", "true");
  });

  it.each([
    ["not JSON", "{not json"],
    ["an object", '{"server-a":true}'],
    ["a string", '"server-a"'],
  ])("treats a stored value that is %s as all collapsed without an error", (_name, stored) => {
    sessionStorage.setItem(settingsPanelStorageKey, stored);
    renderUnderProvider(<Panel id="server-a" title="Alpha" />);

    expect(header("Alpha")).toHaveAttribute("aria-expanded", "false");
  });

  it("drops stored entries that are not strings", () => {
    sessionStorage.setItem(settingsPanelStorageKey, JSON.stringify(["server-a", 7, null, {}]));
    renderUnderProvider(
      <>
        <Panel id="server-a" title="Alpha" />
        <Panel id="server-b" title="Beta" />
      </>,
    );

    expect(header("Alpha")).toHaveAttribute("aria-expanded", "true");
    expect(header("Beta")).toHaveAttribute("aria-expanded", "false");
  });

  it("stays usable when the storage throws", async () => {
    const user = userEvent.setup();
    const read = vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("storage unavailable");
    });
    const write = vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("storage unavailable");
    });
    try {
      renderUnderProvider(<Panel id="server-a" title="Alpha" />);
      expect(header("Alpha")).toHaveAttribute("aria-expanded", "false");

      await user.click(header("Alpha"));
      expect(header("Alpha")).toHaveAttribute("aria-expanded", "true");
    } finally {
      read.mockRestore();
      write.mockRestore();
    }
  });

  it("keeps the content mounted while collapsed", async () => {
    const user = userEvent.setup();
    seedSettingsPanels(["server-a"]);
    renderUnderProvider(<Panel id="server-a" title="Alpha" />);

    const field = screen.getByLabelText("Alpha field");
    await user.type(field, "kept");
    await user.click(header("Alpha"));
    expect(field).not.toBeVisible();
    await user.click(header("Alpha"));

    expect(screen.getByLabelText("Alpha field")).toHaveValue("kept");
  });

  it("throws outside the provider", () => {
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    try {
      expect(() => render(<StateProbe />)).toThrow(/SettingsPanelStateProvider/);
    } finally {
      error.mockRestore();
    }
  });
});

describe("SettingsPanel staged changes", () => {
  beforeEach(() => clearSettingsPanels());

  it("opens on a new key, stays closable, reopens for another new key and pins while staged", async () => {
    const user = userEvent.setup();
    renderUnderProvider(<StagingHost />);

    await user.click(control("stage-a"));
    expect(header("Alpha")).toHaveAttribute("aria-expanded", "true");

    await user.click(header("Alpha"));
    expect(header("Alpha")).toHaveAttribute("aria-expanded", "false");

    await user.click(control("stage-ab"));
    expect(header("Alpha")).toHaveAttribute("aria-expanded", "true");

    // Collapse all skips the pinned panel and closes the other open panel.
    await user.click(header("Beta"));
    await user.click(control("probe-collapse"));
    expect(header("Alpha")).toHaveAttribute("aria-expanded", "true");
    expect(header("Beta")).toHaveAttribute("aria-expanded", "false");

    // With the staging gone the panel is no longer pinned.
    await user.click(control("unstage"));
    await user.click(control("probe-collapse"));
    expect(header("Alpha")).toHaveAttribute("aria-expanded", "false");
  });

  it("does not reopen a closed panel when the same keys are still staged", async () => {
    const user = userEvent.setup();
    renderUnderProvider(<StagingHost />);

    await user.click(control("stage-a"));
    await user.click(header("Alpha"));
    await user.click(control("stage-a"));

    expect(header("Alpha")).toHaveAttribute("aria-expanded", "false");
  });

  it("reports whether a collapsible panel is open and expands every rendered panel", async () => {
    const user = userEvent.setup();
    renderUnderProvider(<StagingHost />);
    const anyOpen = () => screen.getByTestId("any-open").textContent;

    expect(anyOpen()).toBe("false");
    await user.click(control("stage-a"));
    // The only open panel is pinned, so nothing collapsible is open and the label offers Expand all.
    expect(anyOpen()).toBe("false");
    await user.click(header("Beta"));
    expect(anyOpen()).toBe("true");

    await user.click(header("Beta"));
    await user.click(control("probe-expand"));
    expect(header("Alpha")).toHaveAttribute("aria-expanded", "true");
    expect(header("Beta")).toHaveAttribute("aria-expanded", "true");
  });
});

describe("SettingsPanel location hash", () => {
  beforeEach(() => clearSettingsPanels());

  it("opens only the named panel and focuses its header button on the next frame", async () => {
    renderUnderProvider(
      <>
        <Panel id="server-a" title="Alpha" />
        <Panel id="server-b" title="Beta" />
      </>,
      "/?tab=settings#server-b",
    );

    expect(header("Beta")).toHaveAttribute("aria-expanded", "true");
    expect(header("Alpha")).toHaveAttribute("aria-expanded", "false");
    await waitFor(() => expect(header("Beta")).toHaveFocus());
  });

  it("leaves every panel collapsed without a hash", () => {
    renderUnderProvider(
      <>
        <Panel id="server-a" title="Alpha" />
        <Panel id="server-b" title="Beta" />
      </>,
      "/?tab=settings",
    );

    expect(header("Alpha")).toHaveAttribute("aria-expanded", "false");
    expect(header("Beta")).toHaveAttribute("aria-expanded", "false");
  });

  it("opens the panel again for a new location key that carries the hash, but never closes one", async () => {
    const user = userEvent.setup();
    const { router } = renderUnderProvider(<Panel id="server-a" title="Alpha" />, "/#server-a");
    await waitFor(() => expect(header("Alpha")).toHaveFocus());

    await user.click(header("Alpha"));
    expect(header("Alpha")).toHaveAttribute("aria-expanded", "false");

    await router.navigate("/?again=1#server-a");
    await waitFor(() => expect(header("Alpha")).toHaveAttribute("aria-expanded", "true"));

    await router.navigate("/?other=1");
    expect(header("Alpha")).toHaveAttribute("aria-expanded", "true");
  });
});
