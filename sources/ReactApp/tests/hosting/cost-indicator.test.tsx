import { act, render } from "@testing-library/react";
import type { ReactNode } from "react";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { ApiText } from "@/components/server/ApiText";
import { CostIndicator } from "@/components/server/CostIndicator";
import { ServerStateSummary } from "@/components/server/ServerStateSummary";
import { deriveServerState, deriveServerStateFromInput } from "@/features/hosting/serverState";
import de from "@/locales/de.json";
import en from "@/locales/en.json";

const live = deriveServerStateFromInput({
  hasDeployment: true,
  status: "Running",
  possibleCost: true,
  pendingJobCount: 0,
});
const maybe = deriveServerStateFromInput({
  hasDeployment: true,
  status: "Stopped",
  possibleCost: true,
  pendingJobCount: 0,
});
const none = deriveServerStateFromInput({
  hasDeployment: false,
  status: undefined,
  possibleCost: false,
  pendingJobCount: 0,
});

function withI18n(children: ReactNode) {
  return <I18nextProvider i18n={i18n}>{children}</I18nextProvider>;
}

async function switchLanguage(language: "en" | "de") {
  await act(async () => {
    await i18n.changeLanguage(language);
  });
}

describe("CostIndicator", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(async () => {
    await switchLanguage("en");
  });

  it.each([
    [live, "live", "Money16Regular", en.Server_Cost_Live],
    [maybe, "maybe", "Warning16Regular", en.Server_Cost_Maybe],
    [none, "none", "CheckmarkCircle16Regular", en.Server_Cost_None],
  ] as const)(
    "shows the %# cost with a decorative glyph and visible text",
    (derived, cost, glyph, text) => {
      const { container } = render(withI18n(<CostIndicator derived={derived} />));

      const indicator = container.querySelector(".server-cost-indicator");
      expect(indicator).toHaveAttribute("data-cost", cost);
      expect(indicator).toHaveClass(`server-cost-indicator--${cost}`);
      expect(indicator).toHaveTextContent(text);
      const glyphWrapper = indicator?.querySelector("[data-glyph]");
      expect(glyphWrapper).toHaveAttribute("data-glyph", glyph);
      expect(glyphWrapper).toHaveAttribute("aria-hidden", "true");
    },
  );

  it("renders nothing when the cost is not visible", () => {
    const hidden = deriveServerState(null);
    expect(hidden.costVisible).toBe(false);
    const { container } = render(withI18n(<CostIndicator derived={hidden} />));
    expect(container).toBeEmptyDOMElement();
  });
});

describe("ServerStateSummary", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(async () => {
    await switchLanguage("en");
  });

  it("shows the state label and the cost label in English and German", async () => {
    const { container, rerender } = render(withI18n(<ServerStateSummary derived={live} />));
    expect(container).toHaveTextContent(en.Server_State_Running);
    expect(container).toHaveTextContent(en.Server_Cost_Live);

    await switchLanguage("de");
    rerender(withI18n(<ServerStateSummary derived={live} />));
    expect(container).toHaveTextContent(de.Server_State_Running);
    expect(container).toHaveTextContent(de.Server_Cost_Live);
  });

  it("omits the cost chip when the cost is hidden but keeps the badge", () => {
    const { container } = render(
      withI18n(<ServerStateSummary derived={deriveServerState(null)} />),
    );
    expect(container).toHaveTextContent(en.Server_State_Unknown);
    expect(container.querySelector(".server-cost-indicator")).toBeNull();
  });

  it("marks every glyph as decorative", () => {
    const { container } = render(withI18n(<ServerStateSummary derived={maybe} />));
    const glyphs = container.querySelectorAll("[data-glyph]");
    expect(glyphs).toHaveLength(2);
    for (const glyph of glyphs) expect(glyph).toHaveAttribute("aria-hidden", "true");
  });
});

describe("ApiText", () => {
  it("renders children in an English-language element", () => {
    const { container } = render(<ApiText>Stops billing immediately.</ApiText>);
    const element = container.querySelector("[lang]");
    expect(element).toHaveAttribute("lang", "en");
    expect(element).toHaveTextContent("Stops billing immediately.");
  });
});
