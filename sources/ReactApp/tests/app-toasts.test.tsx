import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider, useAppRouteToast, useAppToast } from "@/components/AppToasts";

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

describe("application toasts", () => {
  beforeAll(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("stacks feedback without creating a blocking dialog", async () => {
    render(
      <I18nextProvider i18n={i18n}>
        <AppToastProvider>
          <ToastHarness />
        </AppToastProvider>
      </I18nextProvider>,
    );
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Save" }));
    await user.click(screen.getByRole("button", { name: "Fail" }));

    expect(screen.getByRole("status")).toHaveTextContent("Saved");
    expect(screen.getByRole("alert")).toHaveTextContent("Could not save");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Close" })).toHaveLength(2);
    expect(screen.getByRole("status").parentElement).not.toHaveAttribute("aria-live");
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
    expect(screen.getByRole("status")).toHaveTextContent("Saved");
    act(() => screen.getByRole("button", { name: "Close" }).click());
    expect(screen.queryByText("Saved")).not.toBeInTheDocument();

    act(() => screen.getByRole("button", { name: "Save" }).click());
    const toast = screen.getByRole("status");
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

    expect(await screen.findByRole("status")).toHaveTextContent("Saved from editor");
    await waitFor(() => expect(screen.getByText("consumed")).toBeVisible());
    expect(router.state.location.state).toBeNull();
  });
});
