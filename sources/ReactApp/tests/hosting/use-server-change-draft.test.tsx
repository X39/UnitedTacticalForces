import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { GameConfigurationEntry } from "@/features/hosting/types";
import type { SavedServerChanges } from "@/features/hosting/serverChangeDraft";
import {
  ServerChangeDraftProvider,
  useServerChangeDraft,
} from "@/features/hosting/useServerChangeDraft";
import { configurationEntry } from "../helpers/configurationFixtures";

const entries: GameConfigurationEntry[] = [
  configurationEntry({ key: "name", value: "Alpha", source: "Server" }),
  configurationEntry({ key: "port", kind: "Integer", value: 2302 }),
];

function savedWith(
  list: GameConfigurationEntry[],
  overrides: Partial<SavedServerChanges> = {},
): SavedServerChanges {
  return {
    modPackId: 1,
    profile: { profileId: null, revisionId: null },
    entries: list,
    ...overrides,
  };
}

function Harness() {
  const draft = useServerChangeDraft();
  return (
    <div>
      <output data-testid="count">{draft.count}</output>
      <output data-testid="values">{[...draft.draft.values.keys()].join(",")}</output>
      <output data-testid="secrets">{[...draft.draft.secrets.keys()].join(",")}</output>
      <output data-testid="applying">{String(draft.applying)}</output>
      <output data-testid="attempt">{draft.reviewAttempt}</output>
      <output data-testid="announcement">{draft.announcement}</output>
      <output data-testid="readonly">{String(draft.readOnly)}</output>
      <button onClick={() => draft.stageValue("name", "Alpha")}>stage-equal</button>
      <button onClick={() => draft.stageValue("name", "Bravo")}>stage-name</button>
      <button onClick={() => draft.stageValue("port", "2310")}>stage-port</button>
      <button onClick={() => draft.stageModPack(2)}>stage-pack</button>
      <button
        onClick={() =>
          draft.stageSecret("token", { kind: "set", value: "typed", externalProvider: false })
        }
      >
        stage-secret
      </button>
      <button
        onClick={() =>
          draft.applyResults([
            { id: "settings", itemCount: 2, state: "applied" },
            { id: "modPack", itemCount: 1, state: "failed", reason: "other" },
          ])
        }
      >
        apply-results
      </button>
      <button onClick={() => draft.discard()}>discard</button>
      <button onClick={() => draft.markReviewAttempt()}>attempt</button>
      <button onClick={() => draft.setApplying(true)}>applying</button>
      <button onClick={() => draft.announce("x")}>announce</button>
    </div>
  );
}

function renderDraft(saved: SavedServerChanges, readOnly = false) {
  const view = render(
    <ServerChangeDraftProvider saved={saved} readOnly={readOnly}>
      <Harness />
    </ServerChangeDraftProvider>,
  );
  return {
    ...view,
    update: (next: SavedServerChanges) =>
      view.rerender(
        <ServerChangeDraftProvider saved={next} readOnly={readOnly}>
          <Harness />
        </ServerChangeDraftProvider>,
      ),
  };
}

afterEach(() => vi.restoreAllMocks());

describe("ServerChangeDraftProvider", () => {
  it("exposes the normalized draft and count; an equal value is not a change", async () => {
    const user = userEvent.setup();
    renderDraft(savedWith(entries));
    expect(screen.getByTestId("count")).toHaveTextContent("0");
    await user.click(screen.getByText("stage-equal"));
    expect(screen.getByTestId("count")).toHaveTextContent("0");
    await user.click(screen.getByText("stage-name"));
    await user.click(screen.getByText("stage-pack"));
    expect(screen.getByTestId("count")).toHaveTextContent("2");
    expect(screen.getByTestId("values")).toHaveTextContent("name");
  });

  it("re-normalizes against changed saved data without dispatching", async () => {
    const user = userEvent.setup();
    const view = renderDraft(savedWith(entries));
    await user.click(screen.getByText("stage-port"));
    expect(screen.getByTestId("count")).toHaveTextContent("1");
    view.update(
      savedWith([
        entries[0] as GameConfigurationEntry,
        configurationEntry({ key: "port", kind: "Integer", value: 2310 }),
      ]),
    );
    expect(screen.getByTestId("count")).toHaveTextContent("0");
    expect(screen.getByTestId("values")).toHaveTextContent("");
  });

  it("discards, applies results, marks attempts, tracks applying and announces", async () => {
    const user = userEvent.setup();
    renderDraft(savedWith(entries), true);
    expect(screen.getByTestId("readonly")).toHaveTextContent("true");
    await user.click(screen.getByText("stage-name"));
    await user.click(screen.getByText("stage-port"));
    await user.click(screen.getByText("stage-pack"));
    expect(screen.getByTestId("count")).toHaveTextContent("3");
    await user.click(screen.getByText("apply-results"));
    expect(screen.getByTestId("count")).toHaveTextContent("1");
    await user.click(screen.getByText("stage-name"));
    await user.click(screen.getByText("discard"));
    expect(screen.getByTestId("count")).toHaveTextContent("0");
    await user.click(screen.getByText("attempt"));
    await user.click(screen.getByText("attempt"));
    expect(screen.getByTestId("attempt")).toHaveTextContent("2");
    await user.click(screen.getByText("applying"));
    expect(screen.getByTestId("applying")).toHaveTextContent("true");
    await user.click(screen.getByText("announce"));
    expect(screen.getByTestId("announcement")).toHaveTextContent("x");
  });

  it("starts empty after a remount and never writes to browser storage", async () => {
    const user = userEvent.setup();
    const local = vi.spyOn(Storage.prototype, "setItem");
    const view = renderDraft(savedWith(entries));
    await user.click(screen.getByText("stage-name"));
    await user.click(screen.getByText("stage-secret"));
    expect(screen.getByTestId("count")).toHaveTextContent("2");
    expect(screen.getByTestId("secrets")).toHaveTextContent("token");
    view.unmount();
    renderDraft(savedWith(entries));
    expect(screen.getByTestId("count")).toHaveTextContent("0");
    expect(screen.getByTestId("secrets")).toHaveTextContent("");
    expect(local).not.toHaveBeenCalled();
  });
});

describe("useServerChangeDraft", () => {
  it("throws a descriptive error outside the provider", () => {
    const errors = vi.spyOn(globalThis.console, "error").mockImplementation(() => undefined);
    expect(() => render(<Harness />)).toThrow(/ServerChangeDraftProvider/);
    errors.mockRestore();
  });
});
