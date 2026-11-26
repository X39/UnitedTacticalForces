import { render, screen, within } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { Breadcrumbs, type BreadcrumbItem } from "@/components/Breadcrumbs";

function renderTrail(items: readonly BreadcrumbItem[]) {
  const router = createMemoryRouter(
    [{ path: "*", element: <Breadcrumbs label="Breadcrumb" items={items} /> }],
    { initialEntries: ["/"] },
  );
  return render(<RouterProvider router={router} />);
}

const trail: BreadcrumbItem[] = [
  { label: "Servers", to: "/command/servers" },
  { label: "UTF One", to: "/command/servers/server-1" },
  { label: "Activity" },
];

describe("Breadcrumbs", () => {
  it("renders a named navigation with an ordered list of the items in order", () => {
    renderTrail(trail);

    const navigation = screen.getByRole("navigation", { name: "Breadcrumb" });
    const list = within(navigation).getByRole("list");
    expect(list.tagName).toBe("OL");
    expect(within(list).getAllByRole("listitem")).toHaveLength(3);
    expect(
      within(list)
        .getAllByRole("listitem")
        .map((item) => item.textContent),
    ).toEqual(["Servers", "UTF One", "Activity"]);
  });

  it("renders links for items with a target and the last item as current plain text", () => {
    renderTrail(trail);

    expect(screen.getByRole("link", { name: "Servers" })).toHaveAttribute(
      "href",
      "/command/servers",
    );
    expect(screen.getByRole("link", { name: "UTF One" })).toHaveAttribute(
      "href",
      "/command/servers/server-1",
    );
    const current = screen.getByText("Activity");
    expect(current.tagName).toBe("SPAN");
    expect(current).toHaveAttribute("aria-current", "page");
    expect(screen.queryByRole("link", { name: "Activity" })).not.toBeInTheDocument();
    expect(screen.getAllByRole("link")).toHaveLength(2);
  });

  it("renders the last item as plain text even when it carries a target", () => {
    renderTrail([
      { label: "Servers", to: "/command/servers" },
      { label: "UTF One", to: "/x" },
    ]);

    expect(screen.queryByRole("link", { name: "UTF One" })).not.toBeInTheDocument();
    expect(screen.getByText("UTF One")).toHaveAttribute("aria-current", "page");
  });

  it("renders separators between items only, hidden and outside the item text", () => {
    renderTrail(trail);

    const separators = screen.getAllByTestId("fluent-icon");
    expect(separators).toHaveLength(2);
    separators.forEach((separator) => expect(separator).toHaveAttribute("aria-hidden", "true"));
    const items = screen.getAllByRole("listitem");
    expect(within(items[0]!).getAllByTestId("fluent-icon")).toHaveLength(1);
    expect(within(items[1]!).getAllByTestId("fluent-icon")).toHaveLength(1);
    expect(within(items[2]!).queryByTestId("fluent-icon")).not.toBeInTheDocument();
    expect(items.map((item) => item.textContent)).toEqual(["Servers", "UTF One", "Activity"]);
  });

  it("renders a 256 character label in full", () => {
    const longName = "S".repeat(256);
    renderTrail([{ label: "Servers", to: "/command/servers" }, { label: longName }]);

    expect(screen.getByText(longName)).toBeVisible();
    expect(screen.getByText(longName).textContent).toHaveLength(256);
  });
});
