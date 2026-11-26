import { screen, within } from "@testing-library/react";
import { Breadcrumbs } from "@/components/Breadcrumbs";
import { NotFoundPage } from "@/components/NotFoundPage";
import { ForbiddenState } from "@/components/StateViews";
import { ActionButton } from "@/components/ActionControl";
import { renderWithProviders } from "./helpers/renderWithProviders";

describe("closing findings of the shared components", () => {
  it("renders the not found title as the one page level heading above the explaining panel", () => {
    renderWithProviders(<NotFoundPage />);

    const headings = screen.getAllByRole("heading", { level: 1 });
    expect(headings).toHaveLength(1);
    expect(headings[0]).toHaveTextContent("Route not found");
    expect(screen.getByRole("link", { name: "Return home" })).toHaveAttribute("href", "/");
    expect(screen.getByRole("status")).toHaveTextContent(
      "The requested page is not part of either interface.",
    );
  });

  it("puts the forbidden action into the action area of the panel", () => {
    renderWithProviders(
      <ForbiddenState title="Sign in" action={<ActionButton>Sign in now</ActionButton>}>
        <p>Members only.</p>
      </ForbiddenState>,
    );

    const panel = screen.getByRole("status");
    const action = within(panel).getByRole("button", { name: "Sign in now" });
    expect(action.parentElement).toHaveClass("state-panel__action");
    expect(within(panel).getByText("Members only.")).toBeInTheDocument();
  });

  it("isolates a user authored breadcrumb label in a bdi element", () => {
    renderWithProviders(
      <Breadcrumbs
        label="Breadcrumb"
        items={[
          { label: "Servers", to: "/command/servers" },
          { label: "Scheduled Operation", to: "/command/servers/1", userContent: true },
          { label: "Overview" },
        ]}
      />,
    );

    const navigation = screen.getByRole("navigation", { name: "Breadcrumb" });
    expect(within(navigation).getByText("Scheduled Operation").tagName).toBe("BDI");
    expect(within(navigation).getByText("Servers").tagName).toBe("A");
    expect(within(navigation).getByText("Overview").tagName).toBe("SPAN");
  });
});
