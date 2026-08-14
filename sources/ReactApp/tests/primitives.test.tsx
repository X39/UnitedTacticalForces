import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { ConfirmDialog, Markdown, PageHeader } from "@/components/primitives";

describe("shared UI primitives", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  it("drops raw HTML and unsafe Markdown link schemes", () => {
    const { container } = render(
      <Markdown>
        {'<script>alert("x")</script>\n\n[unsafe](javascript:alert(1)) [safe](https://example.com)'}
      </Markdown>,
    );
    expect(container.querySelector("script")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "safe" })).toHaveAttribute(
      "href",
      "https://example.com",
    );
    expect(screen.queryByRole("link", { name: "unsafe" })).not.toBeInTheDocument();
  });

  it("keeps safe relative Markdown links", () => {
    render(<Markdown>{"[map](briefing/map)"}</Markdown>);
    expect(screen.getByRole("link", { name: "map" })).toHaveAttribute("href", "briefing/map");
  });

  it("uses the requested heading level when a page already owns the h1", () => {
    render(<PageHeader title="Community resources" headingLevel={2} />);

    expect(screen.getByRole("heading", { level: 2, name: "Community resources" })).toBeVisible();
    expect(screen.queryByRole("heading", { level: 1 })).not.toBeInTheDocument();
  });

  it("renders stable heading anchors, footnotes, definitions, and alerts", () => {
    const { container } = render(
      <Markdown>
        {
          "## Mission Brief\n\nTerm\n: Definition\n\nIntel[^1]\n\n[^1]: Source\n\n> [!WARNING]\n> Hold position."
        }
      </Markdown>,
    );
    expect(screen.getByRole("heading", { name: "Mission Brief" })).toHaveAttribute(
      "id",
      "mission-brief",
    );
    expect(container.querySelector("dl")).toHaveTextContent("TermDefinition");
    expect(container.querySelector("[data-footnotes]")).toHaveTextContent("Source");
    expect(container.querySelector("blockquote")).toHaveTextContent("WARNING Hold position.");
  });

  it("renders the advanced Markdig constructs used by published content", () => {
    const { container } = render(
      <Markdown>
        {
          '*[UTF]: United Tactical Forces\n\nUTF uses H~2~O, 2^10^, ++inserted text++, ==marked text==, and ""a citation"".\n\n^^^\nFigure body\n^^^ Figure caption\n\n^^ Footer line\n^^ Continued footer\n\n+---------+---------+\n| Header  | Header  |\n+=========+=========+\n| Cell    | Value   |\n+---------+---------+'
        }
      </Markdown>,
    );

    expect(container.querySelector("abbr")).toHaveAttribute("title", "United Tactical Forces");
    expect(container.querySelector("sub")).toHaveTextContent("2");
    expect(container.querySelector("sup")).toHaveTextContent("10");
    expect(container.querySelector("ins")).toHaveTextContent("inserted text");
    expect(container.querySelector("mark")).toHaveTextContent("marked text");
    expect(container.querySelector("cite")).toHaveTextContent("a citation");
    expect(container.querySelector("figure")).toHaveTextContent("Figure bodyFigure caption");
    expect(container.querySelector("figure figcaption")).toHaveTextContent("Figure caption");
    expect(container.querySelector("footer")).toHaveTextContent("Footer line Continued footer");
    expect(container.querySelector("table thead")).toHaveTextContent("HeaderHeader");
    expect(container.querySelector("table tbody")).toHaveTextContent("CellValue");
  });

  it("requires an exact challenge before confirming", async () => {
    const confirm = vi.fn();
    render(
      <I18nextProvider i18n={i18n}>
        <ConfirmDialog
          open
          title="Terminate"
          description="This cannot be undone."
          confirmLabel="Confirm"
          cancelLabel="Cancel"
          challenge="TERMINATE"
          onConfirm={confirm}
          onCancel={vi.fn()}
        />
      </I18nextProvider>,
    );
    const user = userEvent.setup();
    const input = screen.getByRole("textbox");
    await user.type(input, "terminate");
    await user.click(screen.getByRole("button", { name: "Confirm" }));
    expect(confirm).not.toHaveBeenCalled();
    await user.clear(input);
    await user.type(input, "TERMINATE");
    await user.click(screen.getByRole("button", { name: "Confirm" }));
    expect(confirm).toHaveBeenCalledOnce();
  });
});
