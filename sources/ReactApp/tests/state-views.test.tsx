import { act, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n, { i18nReady } from "@/app/i18n";
import { ActionButton } from "@/components/ActionControl";
import {
  AccessBoundary,
  EmptyState,
  ErrorState,
  ForbiddenState,
  LoadingState,
} from "@/components/StateViews";
import { renderWithProviders } from "./helpers/renderWithProviders";

describe("shared state views", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(async () => {
    await act(async () => i18n.changeLanguage("en"));
  });

  describe("LoadingState", () => {
    it("announces the carried loading text as a status", () => {
      renderWithProviders(<LoadingState />);

      const status = screen.getByRole("status");
      expect(status).toHaveTextContent("Loading...");
      expect(status).toHaveClass("state-panel--loading");
      expect(screen.queryByRole("heading")).not.toBeInTheDocument();
      expect(screen.queryByRole("button")).not.toBeInTheDocument();
    });

    it("accepts a specific label and moves no focus", () => {
      const before = document.activeElement;
      renderWithProviders(<LoadingState label="Loading the servers..." />);

      expect(screen.getByRole("status")).toHaveTextContent("Loading the servers...");
      expect(document.activeElement).toBe(before);
    });

    it("reads Lade... in German", async () => {
      await act(async () => i18n.changeLanguage("de"));
      renderWithProviders(<LoadingState />);

      expect(screen.getByRole("status")).toHaveTextContent("Lade...");
    });
  });

  describe("EmptyState", () => {
    it("renders the title as an h2 and the body naming the next step", () => {
      renderWithProviders(
        <EmptyState title="No roles yet">Create the first role to give members access.</EmptyState>,
      );

      expect(screen.getByRole("heading", { level: 2, name: "No roles yet" })).toBeInTheDocument();
      expect(screen.getByRole("status")).toHaveTextContent(
        "Create the first role to give members access.",
      );
    });

    it("renders the single optional action", () => {
      renderWithProviders(
        <EmptyState title="No roles yet" action={<ActionButton>Create role</ActionButton>}>
          Create the first role to give members access.
        </EmptyState>,
      );

      expect(screen.getByRole("button", { name: "Create role" })).toBeInTheDocument();
    });

    it("requires a title and a body at the type level", () => {
      // @ts-expect-error A call without title and children is rejected by the compiler.
      const missing = <EmptyState />;
      // @ts-expect-error A call without a title is rejected by the compiler.
      const noTitle = <EmptyState>Body</EmptyState>;
      expect(missing).toBeDefined();
      expect(noTitle).toBeDefined();
    });
  });

  describe("ErrorState", () => {
    it("shows the generic copy as an alert and retries once per click", async () => {
      const onRetry = vi.fn();
      renderWithProviders(<ErrorState onRetry={onRetry} />);

      const alert = screen.getByRole("alert");
      expect(alert).toHaveTextContent("This couldn't be loaded");
      expect(alert).toHaveTextContent("Check your connection and try again.");
      await userEvent.click(screen.getByRole("button", { name: "Try again" }));
      expect(onRetry).toHaveBeenCalledTimes(1);
    });

    it("lets a page override title, body and retry label", () => {
      renderWithProviders(
        <ErrorState
          onRetry={() => undefined}
          title="The servers could not be loaded"
          retryLabel="Reload"
        >
          The server list is out of date.
        </ErrorState>,
      );

      const alert = screen.getByRole("alert");
      expect(alert).toHaveTextContent("The servers could not be loaded");
      expect(alert).toHaveTextContent("The server list is out of date.");
      expect(alert).not.toHaveTextContent("This couldn't be loaded");
      expect(screen.getByRole("button", { name: "Reload" })).toBeInTheDocument();
    });

    it("uses the ghost variant for the retry button", () => {
      renderWithProviders(<ErrorState onRetry={() => undefined} />);

      expect(screen.getByRole("button", { name: "Try again" })).toHaveClass("button--ghost");
    });

    it("reads the German generic copy", async () => {
      await act(async () => i18n.changeLanguage("de"));
      renderWithProviders(<ErrorState onRetry={() => undefined} />);

      const alert = screen.getByRole("alert");
      expect(alert).toHaveTextContent("Das konnte nicht geladen werden");
      expect(screen.getByRole("button", { name: "Erneut versuchen" })).toBeInTheDocument();
    });
  });

  describe("ForbiddenState and AccessBoundary", () => {
    it("shows the generic forbidden copy with its overrides", () => {
      const { unmount } = renderWithProviders(<ForbiddenState />);
      const generic = screen.getByRole("status");
      expect(generic).toHaveTextContent("You don't have access to this page");
      expect(generic).toHaveTextContent("Ask an administrator to give you access.");
      unmount();

      renderWithProviders(<ForbiddenState title="No server access">Ask the owner.</ForbiddenState>);
      expect(screen.getByRole("status")).toHaveTextContent("No server access");
      expect(screen.getByRole("status")).toHaveTextContent("Ask the owner.");
    });

    it("renders the children when allowed and never the forbidden panel", () => {
      renderWithProviders(
        <AccessBoundary allowed>
          <p>Protected content</p>
        </AccessBoundary>,
      );

      expect(screen.getByText("Protected content")).toBeInTheDocument();
      expect(screen.queryByText("You don't have access to this page")).not.toBeInTheDocument();
    });

    it("renders the forbidden panel and no children when not allowed", () => {
      renderWithProviders(
        <AccessBoundary allowed={false}>
          <p>Protected content</p>
        </AccessBoundary>,
      );

      expect(screen.queryByText("Protected content")).not.toBeInTheDocument();
      expect(screen.getByRole("status")).toHaveTextContent("You don't have access to this page");
    });

    it("passes optional title and body overrides to the forbidden panel", () => {
      renderWithProviders(
        <AccessBoundary allowed={false} title="No hosting access" body="Ask for the hosting claim.">
          <p>Protected content</p>
        </AccessBoundary>,
      );

      const status = screen.getByRole("status");
      expect(status).toHaveTextContent("No hosting access");
      expect(status).toHaveTextContent("Ask for the hosting claim.");
    });

    it("is presentation only: the children stay under the caller's control", () => {
      // The boundary hides content, it never fetches or authorizes. The API stays the authority (T-69-03).
      const fetchSpy = vi.spyOn(globalThis, "fetch");
      renderWithProviders(
        <AccessBoundary allowed={false}>
          <p>Protected content</p>
        </AccessBoundary>,
      );

      expect(fetchSpy).not.toHaveBeenCalled();
      fetchSpy.mockRestore();
    });

    it("reads the German forbidden copy", async () => {
      await act(async () => i18n.changeLanguage("de"));
      renderWithProviders(<ForbiddenState />);

      expect(screen.getByRole("status")).toHaveTextContent(
        "Du hast keinen Zugriff auf diese Seite",
      );
    });
  });
});
