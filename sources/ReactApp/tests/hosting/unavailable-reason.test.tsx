import { act, render, screen } from "@testing-library/react";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { UnavailableReason } from "@/components/server/UnavailableReason";
import {
  deploymentStateReason,
  launchDisabledReason,
  operatePermissionReason,
  operationPendingReason,
  pausedReason,
  terminationPendingReason,
} from "@/components/server/unavailableReasonMapping";
import de from "@/locales/de.json";
import en from "@/locales/en.json";

function renderReason(props: Parameters<typeof UnavailableReason>[0]) {
  return render(
    <I18nextProvider i18n={i18n}>
      <UnavailableReason {...props} />
    </I18nextProvider>,
  );
}

async function switchLanguage(language: "en" | "de") {
  await act(async () => {
    await i18n.changeLanguage(language);
  });
}

describe("UnavailableReason", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(async () => {
    await switchLanguage("en");
  });

  const exactCases = [
    [operatePermissionReason, "Server_Reason_Operate"],
    [operationPendingReason, "Server_Reason_Pending"],
    [launchDisabledReason, "Server_Reason_LaunchDisabled"],
    [terminationPendingReason, "Server_Reason_Terminating"],
    [pausedReason, "Server_Reason_Paused"],
  ] as const;

  it.each(exactCases)("maps %s to localized English and German copy", async (reason, key) => {
    const { container, rerender } = renderReason({ reason });
    expect(container).toHaveTextContent(en[key]);
    expect(container.querySelector("q")).toBeNull();

    await switchLanguage("de");
    rerender(
      <I18nextProvider i18n={i18n}>
        <UnavailableReason reason={reason} />
      </I18nextProvider>,
    );
    expect(container).toHaveTextContent(de[key]);
  });

  it("substitutes the state label into the deployment-state reason in both languages", async () => {
    const { container, rerender } = renderReason({
      reason: deploymentStateReason,
      stateLabel: "Running",
    });
    expect(container).toHaveTextContent('This isn\'t possible while the server is "Running".');

    await switchLanguage("de");
    rerender(
      <I18nextProvider i18n={i18n}>
        <UnavailableReason reason={deploymentStateReason} stateLabel="Läuft" />
      </I18nextProvider>,
    );
    expect(container).toHaveTextContent("Das ist nicht möglich, solange der Server „Läuft“ ist.");
  });

  it("uses the generic state sentence when no state label is supplied", () => {
    const { container } = renderReason({ reason: deploymentStateReason });
    expect(container).toHaveTextContent(en.Server_Reason_StateGeneric);
  });

  it("quotes unknown API text in a lang=en element after the generic sentence", () => {
    const { container } = renderReason({ reason: "Quota exceeded <b>now</b>." });
    expect(container).toHaveTextContent(`${en.Server_Reason_Other} Quota exceeded <b>now</b>.`);
    const quote = container.querySelector("q");
    expect(quote).toHaveAttribute("lang", "en");
    expect(quote?.textContent).toBe("Quota exceeded <b>now</b>.");
    expect(container.querySelector("b")).toBeNull();
  });

  it("renders only the generic sentence for an empty or missing reason", () => {
    const empty = renderReason({ reason: "" });
    expect(empty.container).toHaveTextContent(en.Server_Reason_Other);
    expect(empty.container.querySelector("q")).toBeNull();
    empty.unmount();

    const missing = renderReason({ reason: null });
    expect(missing.container).toHaveTextContent(en.Server_Reason_Other);
    expect(missing.container.querySelector("q")).toBeNull();
  });

  it("maps custom text to the launch-disabled copy only when told so, and exact reasons win", () => {
    const custom = renderReason({ reason: "Disabled for maintenance.", launchDisabled: true });
    expect(custom.container).toHaveTextContent(en.Server_Reason_LaunchDisabled);
    expect(custom.container.querySelector("q")).toBeNull();
    custom.unmount();

    const exact = renderReason({ reason: operatePermissionReason, launchDisabled: true });
    expect(exact.container).toHaveTextContent(en.Server_Reason_Operate);
    expect(exact.container).not.toHaveTextContent(en.Server_Reason_LaunchDisabled);
  });

  it("applies the id to the wrapper so controls can reference it", () => {
    renderReason({ reason: operatePermissionReason, id: "reason-1" });
    expect(document.getElementById("reason-1")).toHaveTextContent(en.Server_Reason_Operate);
    expect(screen.queryByRole("alert")).toBeNull();
  });
});
