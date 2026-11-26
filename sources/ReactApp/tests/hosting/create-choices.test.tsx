import { act, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "@/app/i18n";
import { AfterCreatingChoice } from "@/components/server/AfterCreatingChoice";
import {
  CostOwnerLine,
  costOwnerDisplayName,
  type CostOwnerLineProps,
} from "@/components/server/CostOwnerLine";
import { createOwners } from "../helpers/createFixtures";
import { renderWithProviders } from "../helpers/renderWithProviders";

const personalId = "10000000-0000-4000-8000-000000000010";
const sharedId = "10000000-0000-4000-8000-000000000011";

function lineProps(overrides: Partial<CostOwnerLineProps> = {}): CostOwnerLineProps {
  return {
    owners: createOwners(),
    costOwnerId: null,
    viewerName: "Creator",
    loading: false,
    failed: false,
    onRetry: vi.fn(),
    onChange: vi.fn(),
    ...overrides,
  };
}

afterEach(async () => {
  await act(async () => {
    await i18n.changeLanguage("en");
  });
});

describe("costOwnerDisplayName", () => {
  it("returns the selected owner's name for a known id", () => {
    expect(costOwnerDisplayName(sharedId, createOwners(), "Creator")).toBe("Shared budget");
  });

  it("returns the caller's own owner for its explicit id", () => {
    expect(costOwnerDisplayName(personalId, createOwners(), "Creator")).toBe("Personal budget");
  });

  it("returns undefined for an unknown id", () => {
    expect(
      costOwnerDisplayName("20000000-0000-4000-8000-000000000099", createOwners(), "Creator"),
    ).toBeUndefined();
  });

  it("returns the caller's own owner for a null id", () => {
    expect(costOwnerDisplayName(null, createOwners(), "Creator")).toBe("Personal budget");
  });

  it("falls back to the viewer name when no caller-owned owner exists", () => {
    expect(costOwnerDisplayName(null, createOwners({ withSelf: false }), "Creator")).toBe(
      "Creator",
    );
    expect(costOwnerDisplayName(null, undefined, "Creator")).toBe("Creator");
  });
});

describe("CostOwnerLine", () => {
  it("names the caller's owner and offers one change button", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    renderWithProviders(<CostOwnerLine {...lineProps({ onChange })} />);

    expect(screen.getByText("Billed to Personal budget")).toBeInTheDocument();
    const buttons = screen.getAllByRole("button", { name: "Change cost owner" });
    expect(buttons).toHaveLength(1);
    await user.click(buttons[0]!);
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it("names the selected owner", () => {
    renderWithProviders(<CostOwnerLine {...lineProps({ costOwnerId: sharedId })} />);

    expect(screen.getByText("Billed to Shared budget")).toBeInTheDocument();
  });

  it("shows the viewer nickname when no personal owner exists yet", () => {
    renderWithProviders(
      <CostOwnerLine {...lineProps({ owners: createOwners({ withSelf: false }) })} />,
    );

    expect(screen.getByText("Billed to Creator")).toBeInTheDocument();
  });

  it("shows the frozen warning with exactly one change button inside the panel", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    renderWithProviders(
      <CostOwnerLine {...lineProps({ owners: createOwners({ frozenSelf: true }), onChange })} />,
    );

    const panel = screen.getByRole("status");
    expect(panel).toHaveTextContent(
      "Personal budget is frozen and cannot take on a new server. Choose another cost owner.",
    );
    const buttons = screen.getAllByRole("button", { name: "Change cost owner" });
    expect(buttons).toHaveLength(1);
    expect(within(panel).getByRole("button", { name: "Change cost owner" })).toBe(buttons[0]);
    expect(screen.queryByText(/^Billed to/)).not.toBeInTheDocument();
    await user.click(buttons[0]!);
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it("shows the checking text and no change button while loading", () => {
    renderWithProviders(<CostOwnerLine {...lineProps({ owners: undefined, loading: true })} />);

    expect(screen.getByText("Checking who pays…")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("shows the failure with a retry button", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    renderWithProviders(
      <CostOwnerLine {...lineProps({ owners: undefined, failed: true, onRetry })} />,
    );

    expect(screen.getByText("Who pays could not be checked.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Change cost owner" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Check cost owner again" }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it("renders an owner name with markup as literal text", () => {
    const owners = createOwners();
    owners[0]!.displayName = "<img src=x onerror=alert(1)>";
    const { container } = renderWithProviders(<CostOwnerLine {...lineProps({ owners })} />);

    expect(screen.getByText("Billed to <img src=x onerror=alert(1)>")).toBeInTheDocument();
    expect(container.querySelector("img")).toBeNull();
  });
});

describe("AfterCreatingChoice", () => {
  it("renders the group with a heading legend, both options and their help texts", () => {
    renderWithProviders(
      <AfterCreatingChoice value="keep-off" onChange={vi.fn()} startDisabled={false} />,
    );

    const group = screen.getByRole("group", { name: "After creating" });
    expect(group.tagName).toBe("FIELDSET");
    expect(
      within(group).getByRole("heading", { level: 2, name: "After creating" }),
    ).toBeInTheDocument();
    const keepOff = screen.getByRole("radio", { name: /Keep it off/ });
    const start = screen.getByRole("radio", { name: /Start it now/ });
    expect(keepOff).toBeChecked();
    expect(start).not.toBeChecked();
    expect(
      screen.getByText("The server is created and stays off. Nothing costs until you start it."),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Creates the server, then asks you to confirm the cost before it starts."),
    ).toBeInTheDocument();
  });

  it("checks the option matching the value and reports a chosen start", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    renderWithProviders(
      <AfterCreatingChoice value="keep-off" onChange={onChange} startDisabled={false} />,
    );

    await user.click(screen.getByRole("radio", { name: /Start it now/ }));

    expect(onChange).toHaveBeenCalledWith("start");
  });

  it("checks start when it is the value", () => {
    renderWithProviders(
      <AfterCreatingChoice value="start" onChange={vi.fn()} startDisabled={false} />,
    );

    expect(screen.getByRole("radio", { name: /Start it now/ })).toBeChecked();
  });

  it("disables start with the visible launch-disabled reason", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    renderWithProviders(<AfterCreatingChoice value="keep-off" onChange={onChange} startDisabled />);

    const start = screen.getByRole("radio", { name: /Start it now/ });
    expect(start).toBeDisabled();
    const reason = screen.getByText("Starting is turned off for this server.");
    expect(reason).toBeVisible();
    expect(start.getAttribute("aria-describedby")?.split(" ")).toContain(reason.id);
    await user.click(start);
    expect(onChange).not.toHaveBeenCalled();
  });

  it("renders German copy", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });
    renderWithProviders(
      <AfterCreatingChoice value="keep-off" onChange={vi.fn()} startDisabled={false} />,
    );

    expect(screen.getByRole("group", { name: "Nach dem Erstellen" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /Aus lassen/ })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /Jetzt starten/ })).toBeInTheDocument();
  });
});
