import { createRef } from "react";
import { act, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "@/app/i18n";
import {
  CreateSummary,
  createBlockReasonKeys,
  type CreateSummaryProps,
} from "@/components/server/CreateSummary";
import { createBlockReasonOrder } from "@/features/hosting/createServerModel";
import { formatHourlyRate, formatMoney } from "@/features/hosting/model";
import { renderWithProviders } from "../helpers/renderWithProviders";

function summaryProps(overrides: Partial<CreateSummaryProps> = {}): CreateSummaryProps {
  return {
    estimate: { kind: "known", hourly: 0.42, monthly: 306.6 },
    sizeLabel: "CX32",
    storageLabel: "Included disk, 80 GB",
    ownerLabel: "Personal budget",
    reason: null,
    reasonOwnerName: undefined,
    afterCreating: "keep-off",
    pending: false,
    invalidCount: 0,
    requestFailed: false,
    ...overrides,
  };
}

const expectedReasons: Record<(typeof createBlockReasonOrder)[number], string> = {
  "owner-frozen":
    "Personal budget is frozen, so it cannot pay for a new server. Choose another cost owner.",
  "owner-loading": "Checking who pays…",
  "owner-failed": "Who pays could not be checked. Try again.",
  "options-loading": "Loading the options for this game…",
  "options-failed": "The options for this game could not be loaded. Try again.",
  "no-provider": "No cloud provider is available for this game right now.",
  "estimate-running": "Checking whether this setup fits…",
  "estimate-failed": "The storage check failed. Try again.",
  "storage-misfit":
    "The selected storage does not fit this setup. Change the size or storage under Advanced options.",
};

afterEach(async () => {
  await act(async () => {
    await i18n.changeLanguage("en");
  });
});

describe("CreateSummary cost", () => {
  it("leads with the estimated cost heading, the hourly and then the monthly sentence", () => {
    renderWithProviders(<CreateSummary {...summaryProps()} />);

    expect(screen.getByRole("heading", { level: 2, name: "Estimated cost" })).toBeInTheDocument();
    const hourly = screen.getByText(`${formatHourlyRate(0.42)} per hour while it runs`);
    const monthly = screen.getByText(`about ${formatMoney(306.6)} per month if left running`);
    const submit = screen.getByRole("button", { name: "Create server and keep off" });
    expect(hourly.compareDocumentPosition(monthly) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(monthly.compareDocumentPosition(submit) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("omits the monthly sentence when no monthly figure exists", () => {
    renderWithProviders(
      <CreateSummary
        {...summaryProps({ estimate: { kind: "known", hourly: 0.42, monthly: undefined } })}
      />,
    );

    expect(screen.getByText(/per hour while it runs/)).toBeInTheDocument();
    expect(screen.queryByText(/per month if left running/)).not.toBeInTheDocument();
  });

  it("shows Calculating without any amount while the estimate fetches", () => {
    const { container } = renderWithProviders(
      <CreateSummary {...summaryProps({ estimate: { kind: "calculating" } })} />,
    );

    expect(screen.getByText("Calculating…")).toBeInTheDocument();
    expect(container.querySelector(".create-summary__hourly")).toHaveTextContent(/^Calculating…$/);
    expect(container).not.toHaveTextContent(/[€$]/);
  });

  it("shows the not-available copy instead of an amount, never 0", () => {
    const { container } = renderWithProviders(
      <CreateSummary {...summaryProps({ estimate: { kind: "unavailable" } })} />,
    );

    expect(
      screen.getByText("Cost estimate not available. You can still create the server."),
    ).toBeInTheDocument();
    expect(container.querySelector(".create-summary__hourly")).not.toHaveTextContent("0");
    expect(container.querySelector(".create-summary__monthly")).toBeNull();
    expect(container).not.toHaveTextContent(/[€$]/);
  });

  it("lists size, storage and cost owner and the while-off note", () => {
    renderWithProviders(<CreateSummary {...summaryProps()} />);

    expect(screen.getByText("Size").nextElementSibling).toHaveTextContent("CX32");
    expect(screen.getByText("Storage").nextElementSibling).toHaveTextContent(
      "Included disk, 80 GB",
    );
    expect(screen.getByText("Cost owner").nextElementSibling).toHaveTextContent("Personal budget");
    expect(
      screen.getByText(
        "Creating the server does not start a cloud machine. Cost starts only when you start it.",
      ),
    ).toBeInTheDocument();
  });

  it("shows unknown owner and an em dash for missing values", () => {
    renderWithProviders(
      <CreateSummary
        {...summaryProps({ ownerLabel: undefined, sizeLabel: undefined, storageLabel: undefined })}
      />,
    );

    expect(screen.getByText("Cost owner").nextElementSibling).toHaveTextContent(
      "Cost owner unknown",
    );
    expect(screen.getByText("Size").nextElementSibling).toHaveTextContent("—");
    expect(screen.getByText("Storage").nextElementSibling).toHaveTextContent("—");
  });

  it("formats amounts with a comma decimal separator in German", async () => {
    await act(async () => {
      await i18n.changeLanguage("de");
    });
    const { container } = renderWithProviders(<CreateSummary {...summaryProps()} />);

    expect(container.querySelector(".create-summary__hourly")).toHaveTextContent("0,42");
    expect(container.querySelector(".create-summary__monthly")).toHaveTextContent("306,60");
  });
});

describe("CreateSummary submit", () => {
  it("names the outcome on a submit button", () => {
    const { rerender } = renderWithProviders(<CreateSummary {...summaryProps()} />);
    expect(screen.getByRole("button", { name: "Create server and keep off" })).toHaveAttribute(
      "type",
      "submit",
    );

    rerender(<CreateSummary {...summaryProps({ afterCreating: "start" })} />);
    expect(screen.getByRole("button", { name: "Create server and start" })).toHaveAttribute(
      "type",
      "submit",
    );
  });

  it.each(createBlockReasonOrder)("blocks with exactly one visible reason for %s", (reason) => {
    const { container } = renderWithProviders(
      <CreateSummary {...summaryProps({ reason, reasonOwnerName: "Personal budget" })} />,
    );

    const submit = screen.getByRole("button", { name: "Create server and keep off" });
    expect(submit).toHaveAttribute("aria-disabled", "true");
    const reasonId = submit.getAttribute("aria-describedby");
    expect(reasonId).toBeTruthy();
    const reasonElement = document.getElementById(reasonId ?? "");
    expect(reasonElement).toHaveTextContent(expectedReasons[reason]);
    expect(container.querySelectorAll(".action-reason")).toHaveLength(1);
  });

  it("maps every reason to a key that has English copy", () => {
    expect(Object.keys(createBlockReasonKeys).sort()).toEqual([...createBlockReasonOrder].sort());
  });

  it("never submits the form while blocked, by click or by Enter in a text field", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn((event: { preventDefault(): void }) => event.preventDefault());
    renderWithProviders(
      <form onSubmit={onSubmit}>
        <input aria-label="Name" />
        <CreateSummary
          {...summaryProps({ reason: "owner-frozen", reasonOwnerName: "Personal budget" })}
        />
      </form>,
    );

    await user.click(screen.getByRole("button", { name: "Create server and keep off" }));
    await user.type(screen.getByLabelText("Name"), "Alpha{Enter}");

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("submits once when nothing blocks it", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn((event: { preventDefault(): void }) => event.preventDefault());
    renderWithProviders(
      <form onSubmit={onSubmit}>
        <input aria-label="Name" />
        <CreateSummary {...summaryProps()} />
      </form>,
    );

    await user.click(screen.getByRole("button", { name: "Create server and keep off" }));

    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it("shows the spinner and the pending label while creating", () => {
    renderWithProviders(<CreateSummary {...summaryProps({ pending: true })} />);

    const submit = screen.getByRole("button", { name: /Creating server…/ });
    expect(submit).toBeInTheDocument();
    expect(submit.querySelector("span")).not.toBeNull();
  });
});

describe("CreateSummary alerts", () => {
  it("shows the singular and plural invalid-fields messages", () => {
    const { rerender } = renderWithProviders(
      <CreateSummary {...summaryProps({ invalidCount: 1 })} />,
    );
    expect(screen.getByRole("alert")).toHaveTextContent(
      "Check the highlighted field before creating.",
    );

    rerender(<CreateSummary {...summaryProps({ invalidCount: 3 })} />);
    expect(screen.getByRole("alert")).toHaveTextContent(
      "Check the 3 highlighted fields before creating.",
    );
  });

  it("shows no invalid-fields alert for zero invalid fields", () => {
    renderWithProviders(<CreateSummary {...summaryProps()} />);

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("shows a focusable request error that receives the passed ref", () => {
    const errorRef = createRef<HTMLParagraphElement>();
    renderWithProviders(<CreateSummary {...summaryProps({ requestFailed: true, errorRef })} />);

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent(
      "The server could not be created. Nothing was started and nothing costs.",
    );
    expect(alert).toHaveAttribute("tabindex", "-1");
    expect(errorRef.current).toBe(alert);
  });

  it("contains no live region", () => {
    const { container } = renderWithProviders(
      <CreateSummary
        {...summaryProps({
          reason: "owner-loading",
          invalidCount: 2,
          requestFailed: true,
          estimate: { kind: "calculating" },
        })}
      />,
    );

    // The render container also holds the toast provider's persistent live regions, so only the summary is checked.
    const summary = container.querySelector<HTMLElement>("aside.create-summary");
    expect(summary).not.toBeNull();
    expect(summary?.querySelector("[aria-live]")).toBeNull();
    expect(within(summary as HTMLElement).queryByRole("status")).toBeNull();
  });
});
