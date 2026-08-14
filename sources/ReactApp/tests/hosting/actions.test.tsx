import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerActions } from "@/features/hosting/components";
import type { ServerAction } from "@/features/hosting/types";

const terminate = {
  id: "terminate",
  label: "Terminate now",
  description: "Release all provider resources.",
  enabled: true,
  dangerous: true,
  pending: false,
  order: 1,
  confirmationChallenge: "TERMINATE",
  confirmationText: "This removes the deployment.",
  costDescription: "Stops possible provider cost.",
} as ServerAction;

describe("hosting lifecycle action confirmation", () => {
  beforeAll(async () => i18nReady);

  it("never invokes a challenge action until the exact challenge is entered", async () => {
    const execute = vi.fn().mockResolvedValue(undefined);
    render(
      <I18nextProvider i18n={i18n}>
        <ServerActions actions={[terminate]} busy={false} onExecute={execute} />
      </I18nextProvider>,
    );
    const user = userEvent.setup();
    await user.click(screen.getByRole("button", { name: "Terminate now" }));
    const input = screen.getByRole("textbox");
    await user.type(input, "terminate");
    await user.click(screen.getAllByRole("button", { name: "Terminate now" })[1]!);
    expect(execute).not.toHaveBeenCalled();
    await user.clear(input);
    await user.type(input, "TERMINATE");
    await user.click(screen.getAllByRole("button", { name: "Terminate now" })[1]!);
    expect(execute).toHaveBeenCalledWith(terminate, "TERMINATE");
  });

  it("keeps unavailable server-authoritative actions disabled", () => {
    render(
      <I18nextProvider i18n={i18n}>
        <ServerActions
          actions={[{ ...terminate, enabled: false, unavailableReason: "Worker offline" }]}
          busy={false}
          onExecute={vi.fn()}
        />
      </I18nextProvider>,
    );
    expect(screen.getByRole("button", { name: "Terminate now" })).toBeDisabled();
    expect(screen.getByText("Worker offline")).toBeInTheDocument();
  });
});
