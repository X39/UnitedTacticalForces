import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { PublicHero } from "@/components/PublicHero";
import { ConfirmDialog, Markdown, MarkdownExcerpt, PageHeader } from "@/components/primitives";

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

  it("renders Markdown excerpts without exposing source syntax or nested interactions", () => {
    const { container } = render(
      <MarkdownExcerpt maximumLines={4}>
        {
          "## Situation\n\n**Secure** the [rally point](/events/briefing).\n\n- Hold :radio: watch\n- Report contact"
        }
      </MarkdownExcerpt>,
    );
    const excerpt = container.querySelector<HTMLElement>(".markdown--excerpt");

    expect(excerpt).not.toBeNull();
    expect(excerpt?.style.getPropertyValue("--markdown-excerpt-max-height")).toBe("6.2em");
    expect(excerpt?.querySelector(".markdown-excerpt__heading")).toHaveTextContent("Situation");
    expect(excerpt?.querySelector("strong:not(.markdown-excerpt__heading)")).toHaveTextContent(
      "Secure",
    );
    expect(excerpt?.querySelector("ul")).toHaveTextContent("Hold 📻 watch Report contact");
    expect(screen.queryByRole("link", { name: "rally point" })).not.toBeInTheDocument();
    expect(screen.getByText("rally point")).toHaveClass("markdown-excerpt__link");
    expect(excerpt).not.toHaveTextContent("##");
    expect(excerpt).not.toHaveTextContent("**");
  });

  it("provides fallback copy and keeps PublicHero descriptions block-safe", () => {
    const { container } = render(
      <PublicHero
        title="Operation Northstar"
        description={
          <MarkdownExcerpt fallback="No briefing is available yet.">{"   "}</MarkdownExcerpt>
        }
      />,
    );
    const description = container.querySelector<HTMLElement>(".public-hero__description");

    expect(description?.tagName).toBe("DIV");
    expect(description?.querySelector(":scope > .markdown--excerpt")).toHaveTextContent(
      "No briefing is available yet.",
    );
    expect(description?.querySelector(":scope > p")).not.toBeInTheDocument();
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
