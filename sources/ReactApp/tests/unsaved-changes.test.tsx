import { act, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Link } from "react-router";
import { i18nReady } from "@/app/i18n";
import { useUnsavedChanges } from "@/components/primitives";
import { renderWithProviders } from "./helpers/renderWithProviders";

/** Mounts a form-like view whose only job is to hold the unsaved changes guard. */
function Harness({ dirty }: { dirty: boolean }) {
  useUnsavedChanges(dirty);
  return (
    <div>
      <Link to="#command-content">Skip to content</Link>
      <Link to="/command/servers/1?tab=settings">Other query</Link>
      <Link to="/command/servers/2">Other page</Link>
    </div>
  );
}

function renderHarness(dirty: boolean) {
  return renderWithProviders(<Harness dirty={dirty} />, {
    route: "/command/servers/1",
    path: "/command/servers/:id",
  });
}

describe("useUnsavedChanges", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("does not ask when only the URL fragment changes, like the skip link does", async () => {
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
    const user = userEvent.setup();
    const { router } = renderHarness(true);

    await user.click(screen.getByRole("link", { name: "Skip to content" }));

    expect(confirm).not.toHaveBeenCalled();
    expect(router.state.location.hash).toBe("#command-content");
  });

  it.each(["Other query", "Other page"])(
    "asks before a dirty form leaves for %s and stays when the member declines",
    async (name) => {
      const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
      const user = userEvent.setup();
      const { router } = renderHarness(true);

      await user.click(screen.getByRole("link", { name }));

      await act(async () => {
        await Promise.resolve();
      });
      expect(confirm).toHaveBeenCalledTimes(1);
      expect(router.state.location.pathname).toBe("/command/servers/1");
      expect(router.state.location.search).toBe("");
    },
  );

  it("never asks while the form is clean", async () => {
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
    const user = userEvent.setup();
    const { router } = renderHarness(false);

    await user.click(screen.getByRole("link", { name: "Other page" }));

    expect(confirm).not.toHaveBeenCalled();
    expect(router.state.location.pathname).toBe("/command/servers/2");
  });
});
