import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider, useAppRouteToast, useAppToast } from "@/components/AppToasts";
import { findToast, toastPoliteness } from "./helpers/toastQueries";

function ToastHarness() {
  const { showToast } = useAppToast();
  return (
    <>
      <button type="button" onClick={() => showToast("Saved", "success")}>
        Save
      </button>
      <button type="button" onClick={() => showToast("Could not save", "error")}>
        Fail
      </button>
    </>
  );
}

function RouteToastHarness() {
  const location = useLocation();
  useAppRouteToast((location.state as { message?: string } | null)?.message);
  return <div data-testid="route-state">{location.state ? "pending" : "consumed"}</div>;
}

/** Finds a toast synchronously, because the asynchronous queries wait on timers that the fake clock freezes. */
function toastByText(text: string): HTMLElement {
  const toast = screen.getByText(text).closest<HTMLElement>(".app-toast");
  if (!toast) throw new Error(`The text ${text} is not inside a toast.`);
  return toast;
}

describe("application toasts", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders one persistent polite and one assertive region before any toast", () => {
    const { container } = render(
      <I18nextProvider i18n={i18n}>
        <AppToastProvider>
          <ToastHarness />
        </AppToastProvider>
      </I18nextProvider>,
    );

    const regions = container.querySelectorAll(".app-toast-viewport > .app-toast-region");
    expect(regions).toHaveLength(2);
    expect(regions[0]).toHaveAttribute("aria-live", "polite");
    expect(regions[1]).toHaveAttribute("aria-live", "assertive");
    for (const region of regions) {
      expect(region).toHaveAttribute("aria-atomic", "false");
      expect(region).toHaveAttribute("aria-relevant", "additions text");
      expect(region).toBeEmptyDOMElement();
    }
  });

  it("puts each toast into the region that already exists and keeps the regions mounted", async () => {
    vi.useFakeTimers();
    const { container } = render(
      <I18nextProvider i18n={i18n}>
        <AppToastProvider>
          <ToastHarness />
        </AppToastProvider>
      </I18nextProvider>,
    );
    const [polite, assertive] = [
      ...container.querySelectorAll(".app-toast-viewport > .app-toast-region"),
    ];

    act(() => screen.getByRole("button", { name: "Save" }).click());
    act(() => screen.getByRole("button", { name: "Fail" }).click());

    const viewport = container.querySelector(".app-toast-viewport") as HTMLElement;
    expect(polite).toContainElement(toastByText("Saved"));
    expect(assertive).toContainElement(toastByText("Could not save"));
    expect(toastPoliteness(toastByText("Saved"))).toBe("polite");
    expect(toastPoliteness(toastByText("Could not save"))).toBe("assertive");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Close" })).toHaveLength(2);
    expect(viewport.querySelector('[role="status"], [role="alert"]')).toBeNull();

    // The regions are the identical nodes and stay mounted and empty once every toast has expired.
    act(() => vi.advanceTimersByTime(8_000));
    expect(container.querySelectorAll(".app-toast-viewport > .app-toast-region")).toHaveLength(2);
    expect(container.querySelectorAll(".app-toast-viewport > .app-toast-region")[0]).toBe(polite);
    expect(container.querySelectorAll(".app-toast-viewport > .app-toast-region")[1]).toBe(
      assertive,
    );
    expect(polite).toBeEmptyDOMElement();
    expect(assertive).toBeEmptyDOMElement();
  });

  it("can be dismissed and expires automatically", async () => {
    vi.useFakeTimers();
    render(
      <I18nextProvider i18n={i18n}>
        <AppToastProvider>
          <ToastHarness />
        </AppToastProvider>
      </I18nextProvider>,
    );

    act(() => screen.getByRole("button", { name: "Save" }).click());
    expect(document.querySelector(".app-toast")).toHaveTextContent("Saved");
    act(() => screen.getByRole("button", { name: "Close" }).click());
    expect(screen.queryByText("Saved")).not.toBeInTheDocument();

    act(() => screen.getByRole("button", { name: "Save" }).click());
    const toast = document.querySelector(".app-toast") as HTMLElement;
    fireEvent.mouseEnter(toast);
    act(() => vi.advanceTimersByTime(5_000));
    expect(toast).toBeInTheDocument();
    fireEvent.mouseLeave(toast);
    act(() => vi.advanceTimersByTime(5_000));
    expect(screen.queryByText("Saved")).not.toBeInTheDocument();
  });

  it("consumes route feedback after showing it once", async () => {
    const router = createMemoryRouter(
      [
        {
          path: "/",
          element: (
            <AppToastProvider>
              <RouteToastHarness />
            </AppToastProvider>
          ),
        },
      ],
      { initialEntries: [{ pathname: "/", state: { message: "Saved from editor" } }] },
    );
    render(
      <I18nextProvider i18n={i18n}>
        <RouterProvider router={router} />
      </I18nextProvider>,
    );

    expect(await findToast("Saved from editor")).toHaveTextContent("Saved from editor");
    await waitFor(() => expect(screen.getByText("consumed")).toBeVisible());
    expect(router.state.location.state).toBeNull();
  });

  it("gives the dismiss control a name and a target of at least 24px, 44px at or below 800px", () => {
    render(
      <I18nextProvider i18n={i18n}>
        <AppToastProvider>
          <ToastHarness />
        </AppToastProvider>
      </I18nextProvider>,
    );
    act(() => screen.getByRole("button", { name: "Save" }).click());
    const dismiss = screen.getByRole("button", { name: "Close" });
    expect(dismiss.tagName).toBe("BUTTON");
    expect(dismiss).toHaveClass("app-toast__dismiss");

    // jsdom resolves neither var() nor media queries, so the sizes are asserted on the stylesheet source the
    // class draws from; the browser audit (tests/e2e/audit/toast-target-size.spec.ts) measures the rendered box.
    const css = readFileSync(
      resolve(import.meta.dirname, "../src/components/shared-primitives.css"),
      "utf8",
    ).replaceAll("\r\n", "\n");
    const base = /^\.app-toast \.app-toast__dismiss \{([^}]*)\}/m.exec(css)?.[1] ?? "";
    expect(base).toMatch(/min-width:\s*2rem;/);
    expect(base).toMatch(/min-height:\s*2rem;/);
    const rules = [...css.matchAll(/^ *\.app-toast \.app-toast__dismiss \{([^}]*)\}/gm)];
    // The first rule is the desktop size; the second sits inside the max-width 800px block.
    const phone = rules[1]?.[1];
    expect(phone ?? "").toMatch(/min-width:\s*var\(--utf-control-height-touch\);/);
    expect(phone ?? "").toMatch(/min-height:\s*var\(--utf-control-height-touch\);/);
  });

  it("lays the regions out as grids and takes an empty region out of the grid flow", () => {
    const css = readFileSync(
      resolve(import.meta.dirname, "../src/components/shared-primitives.css"),
      "utf8",
    ).replaceAll("\r\n", "\n");
    const region = /^\.app-toast-viewport > \.app-toast-region \{([^}]*)\}/m.exec(css)?.[1] ?? "";
    expect(region).toMatch(/display:\s*grid;/);
    expect(region).toMatch(/gap:\s*var\(--utf-space-[a-z0-9]+\);/);
    const empty =
      /^\.app-toast-viewport > \.app-toast-region:empty \{([^}]*)\}/m.exec(css)?.[1] ?? "";
    expect(empty).toMatch(/position:\s*absolute;/);
  });
});
