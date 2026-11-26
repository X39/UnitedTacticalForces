import { render, screen } from "@testing-library/react";
import { PageStack } from "@/components/PageStack";

describe("PageStack", () => {
  it("renders a div with the page-stack class around its children", () => {
    const { container } = render(
      <PageStack>
        <p>First block</p>
        <p>Second block</p>
      </PageStack>,
    );

    const stack = container.firstElementChild as HTMLElement;
    expect(stack.tagName).toBe("DIV");
    expect(stack).toHaveClass("page-stack");
    expect(stack.className).toBe("page-stack");
    expect(stack).toContainElement(screen.getByText("First block"));
    expect(stack).toContainElement(screen.getByText("Second block"));
  });

  it("joins an extra class name after the page-stack class", () => {
    const { container } = render(
      <PageStack className="members-page">
        <p>Body</p>
      </PageStack>,
    );

    expect((container.firstElementChild as HTMLElement).className).toBe("page-stack members-page");
  });

  it("ignores an undefined class name", () => {
    const { container } = render(
      <PageStack className={undefined}>
        <p>Body</p>
      </PageStack>,
    );

    expect((container.firstElementChild as HTMLElement).className).toBe("page-stack");
  });
});
