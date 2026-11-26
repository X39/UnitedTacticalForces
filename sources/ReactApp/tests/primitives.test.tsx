import { fireEvent, render, screen, within } from "@testing-library/react";
import type { ComponentProps } from "react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import i18n, { i18nReady } from "@/app/i18n";
import { PublicHero } from "@/components/PublicHero";
import en from "@/locales/en.json";
import {
  ConfirmDialog,
  Markdown,
  MarkdownExcerpt,
  PageHeader,
  StatePanel,
  StatusBadge,
  type StateKind,
} from "@/components/primitives";
import { ErrorState } from "@/components/StateViews";
import { parseCustomProperties, readProjectFile, resolveToken } from "./helpers/cssTokens";
import { renderWithProviders } from "./helpers/renderWithProviders";

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

describe("ConfirmDialog additive capabilities", () => {
  beforeAll(async () => {
    await i18nReady;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function dialogElement(props: Partial<ComponentProps<typeof ConfirmDialog>> = {}) {
    return (
      <I18nextProvider i18n={i18n}>
        <ConfirmDialog
          open
          title="Start server"
          description="This starts billing."
          confirmLabel="Start"
          cancelLabel="Keep off"
          onConfirm={() => undefined}
          onCancel={() => undefined}
          {...props}
        />
      </I18nextProvider>
    );
  }

  function renderDialog(props: Partial<ComponentProps<typeof ConfirmDialog>> = {}) {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    const view = render(dialogElement({ onConfirm, onCancel, ...props }));
    // The dialog renders into the document body, so its content is searched from the body.
    const dialog = document.querySelector("dialog");
    if (!dialog) throw new Error("dialog missing");
    return { ...view, container: document.body, dialog, onConfirm, onCancel };
  }

  it("keeps the danger confirm variant and the default kicker for existing callers", () => {
    const { container } = renderDialog();

    expect(screen.getByRole("button", { name: "Start" })).toHaveClass("button--danger");
    expect(container.querySelector(".kicker")).toHaveTextContent(en.React_Confirm_Kicker);
    expect(container.querySelector("dialog")).toHaveClass("dialog");
  });

  it("renders the requested confirm variant", () => {
    renderDialog({ confirmVariant: "gold" });

    expect(screen.getByRole("button", { name: "Start" })).toHaveClass("button--gold");
  });

  it("can place initial focus on the dismiss button", () => {
    renderDialog({ initialFocus: "dismiss" });

    expect(screen.getByRole("button", { name: "Keep off" })).toHaveFocus();
  });

  it("blocks confirmation until the exact case-sensitive challenge is typed", async () => {
    const user = userEvent.setup();
    const { onConfirm } = renderDialog({
      challenge: "Alpha",
      challengeHelper: "Type the name exactly.",
    });
    const confirm = screen.getByRole("button", { name: "Start" });
    const input = screen.getByRole("textbox");

    expect(confirm).toHaveAttribute("aria-disabled", "true");
    expect(confirm).not.toHaveAttribute("disabled");
    expect(input).toHaveAccessibleDescription("Type the name exactly.");

    await user.type(input, "alpha{Enter}");
    expect(confirm).toHaveAttribute("aria-disabled", "true");
    expect(onConfirm).not.toHaveBeenCalled();

    await user.clear(input);
    await user.type(input, "Alpha");
    expect(confirm).not.toHaveAttribute("aria-disabled");
    await user.click(confirm);
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("blocks confirmation while confirmDisabled is set", async () => {
    const user = userEvent.setup();
    const { onConfirm } = renderDialog({ confirmDisabled: true });

    await user.click(screen.getByRole("button", { name: "Start" }));
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("ignores Escape, dismissal and confirmation while busy and re-opens on a native close", async () => {
    const user = userEvent.setup();
    const showModal = vi.spyOn(HTMLDialogElement.prototype, "showModal");
    const { dialog, onConfirm, onCancel } = renderDialog({ busy: true });
    const dismiss = screen.getByRole("button", { name: "Keep off" });
    const confirm = screen.getByRole("button", { name: "Start" });

    expect(dialog).toHaveAttribute("aria-busy", "true");
    expect(dismiss).toBeDisabled();
    expect(confirm).toHaveAttribute("aria-disabled", "true");

    const cancelEvent = new Event("cancel", { cancelable: true });
    fireEvent(dialog, cancelEvent);
    expect(cancelEvent.defaultPrevented).toBe(true);
    expect(onCancel).not.toHaveBeenCalled();
    await user.click(confirm);
    expect(onConfirm).not.toHaveBeenCalled();

    dialog.close();
    showModal.mockClear();
    fireEvent(dialog, new Event("close"));
    expect(showModal).toHaveBeenCalledOnce();
  });

  it("forwards Escape to onCancel when idle and does not re-open after the open prop clears", () => {
    const showModal = vi.spyOn(HTMLDialogElement.prototype, "showModal");
    const onCancel = vi.fn();
    const { dialog, rerender } = renderDialog({ onCancel });

    const cancelEvent = new Event("cancel", { cancelable: true });
    fireEvent(dialog, cancelEvent);
    expect(cancelEvent.defaultPrevented).toBe(true);
    expect(onCancel).toHaveBeenCalledOnce();

    rerender(dialogElement({ open: false, onCancel }));
    showModal.mockClear();
    fireEvent(dialog, new Event("close"));
    expect(showModal).not.toHaveBeenCalled();
  });

  it("shows an alert error, labels the dialog by its title and describes it with its body", () => {
    renderDialog({ error: "The request failed.", children: <p>Costs 0.10 per hour.</p> });

    expect(screen.getByRole("alert")).toHaveTextContent("The request failed.");
    const dialog = screen.getByRole("dialog", { name: "Start server" });
    expect(dialog).toHaveAccessibleDescription("This starts billing. Costs 0.10 per hour.");
    expect(within(dialog).getByText("Costs 0.10 per hour.")).toBeVisible();
  });

  it("accepts a kicker, element labels and an extra class", () => {
    const { container } = renderDialog({
      kicker: "Cost check",
      title: <span>Start the server</span>,
      confirmLabel: <em>Start now</em>,
      className: "dialog--cost",
    });

    expect(container.querySelector(".kicker")).toHaveTextContent("Cost check");
    expect(screen.getByRole("heading", { name: "Start the server" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Start now" })).toBeVisible();
    expect(container.querySelector("dialog")).toHaveClass("dialog", "dialog--cost");
  });

  it("confirms from inside a page form without submitting that form", async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const onOuterSubmit = vi.fn((event: { preventDefault(): void }) => event.preventDefault());
    render(<form onSubmit={onOuterSubmit}>{dialogElement({ onConfirm })}</form>);

    // A dialog nested in a page form would nest its own form, whose submit event never arrives.
    expect(document.querySelector("dialog")?.parentElement).toBe(document.body);
    await user.click(screen.getByRole("button", { name: "Start" }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onOuterSubmit).not.toHaveBeenCalled();
  });

  it("renders exactly two action buttons without an extra action", () => {
    const { dialog } = renderDialog();

    expect(
      within(dialog)
        .getAllByRole("button")
        .map((button) => button.textContent),
    ).toEqual(["Keep off", "Start"]);
  });

  it("renders the extra action after the confirm button and calls its handler", async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    const { dialog, onConfirm } = renderDialog({
      extraAction: { label: "Review changes", variant: "gold", onClick },
    });

    const buttons = within(dialog).getAllByRole("button");
    expect(buttons.map((button) => button.textContent)).toEqual([
      "Keep off",
      "Start",
      "Review changes",
    ]);
    expect(buttons[2]).toHaveClass("button--gold");
    expect(buttons[2]).toHaveAttribute("type", "button");

    await user.click(buttons[2] as HTMLElement);
    expect(onClick).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("disables the extra action while busy", () => {
    renderDialog({
      busy: true,
      extraAction: { label: "Review changes", variant: "gold", onClick: () => undefined },
    });

    expect(screen.getByRole("button", { name: "Review changes" })).toBeDisabled();
  });

  it("renders a named focusable region body only when bodyLabel is set", () => {
    const { dialog, rerender } = renderDialog({
      bodyLabel: "Staged changes",
      children: <p>Mod pack changes.</p>,
    });

    const region = within(dialog).getByRole("region", { name: "Staged changes" });
    expect(region).toHaveAttribute("tabindex", "0");
    expect(region).toHaveClass("dialog__body");

    rerender(dialogElement({ children: <p>Mod pack changes.</p> }));
    expect(within(dialog).queryByRole("region")).not.toBeInTheDocument();
    const plain = dialog.querySelector(".dialog__body");
    expect(plain).not.toHaveAttribute("role");
    expect(plain).not.toHaveAttribute("tabindex");
  });

  it("keeps a confirm with an unavailable reason focusable, described and inert", async () => {
    const user = userEvent.setup();
    const { onConfirm } = renderDialog({ confirmUnavailableReason: "Still comparing the packs." });

    const confirm = screen.getByRole("button", { name: "Start" });
    expect(confirm).toHaveAttribute("aria-disabled", "true");
    expect(confirm).not.toBeDisabled();
    expect(confirm).toHaveAccessibleDescription("Still comparing the packs.");
    expect(screen.getByText("Still comparing the packs.")).toBeVisible();

    confirm.focus();
    expect(confirm).toHaveFocus();
    await user.click(confirm);
    await user.keyboard("{Enter}");
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it("confirms normally when no unavailable reason is given", async () => {
    const user = userEvent.setup();
    const { onConfirm } = renderDialog({ confirmUnavailableReason: undefined });

    await user.click(screen.getByRole("button", { name: "Start" }));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("renders every part inside one content column in the shell order", () => {
    const { dialog } = renderDialog({
      kicker: "Cost check",
      challenge: "Alpha",
      challengeHelper: "Type the name exactly.",
      error: "The request failed.",
      children: <p>Costs 0.10 per hour.</p>,
    });

    const form = dialog.querySelector("form") as HTMLFormElement;
    expect(
      Array.from(form.children).map((child) =>
        child.matches(".kicker")
          ? "kicker"
          : child.matches("h2")
            ? "title"
            : child.matches("p:not(.dialog__error)")
              ? "description"
              : child.matches(".dialog__body")
                ? "body"
                : child.matches(".form-field")
                  ? "field"
                  : child.matches(".dialog__error")
                    ? "error"
                    : child.matches(".dialog__actions")
                      ? "actions"
                      : "unknown",
      ),
    ).toEqual(["kicker", "title", "description", "body", "field", "error", "actions"]);
  });

  it("renders the challenge as a TextField whose hint describes the input", () => {
    const { dialog } = renderDialog({
      challenge: "Alpha",
      challengeHelper: "Type the name exactly.",
    });

    const field = dialog.querySelector(".form-field") as HTMLElement;
    const input = within(field).getByRole("textbox");
    expect(input).toHaveClass("form-field__control");
    expect(input).toHaveAccessibleName(
      `${en.React_Confirm_TypePrefix} Alpha ${en.React_Confirm_TypeSuffix}`,
    );
    expect(input).toHaveAccessibleDescription("Type the name exactly.");
    expect(field.querySelector(".form-field__hint")).toHaveTextContent("Type the name exactly.");
  });

  it("renders the challenge without a hint when no helper is given", () => {
    const { dialog } = renderDialog({ challenge: "Alpha" });

    expect(dialog.querySelector(".form-field__hint")).not.toBeInTheDocument();
    expect(screen.getByRole("textbox")).not.toHaveAttribute("aria-describedby");
  });

  it("keeps the dialog open, announces the error and leaves focus on the dismiss control", () => {
    const { dialog, onCancel } = renderDialog({
      initialFocus: "dismiss",
      error: "The request failed.",
    });

    expect(dialog.open).toBe(true);
    expect(within(dialog).getByRole("alert")).toHaveTextContent("The request failed.");
    expect(onCancel).not.toHaveBeenCalled();
  });

  it("shows a spinner with the confirm label and blocks dismissal while pending", () => {
    const { dialog } = renderDialog({ busy: true, initialFocus: "dismiss" });

    const confirm = within(dialog).getByRole("button", { name: "Start" });
    expect(confirm.querySelector("[aria-hidden='true']")).not.toBeNull();
    expect(confirm).toHaveTextContent("Start");
    expect(within(dialog).getByRole("button", { name: "Keep off" })).toBeDisabled();
    expect(dialog).toHaveAttribute("aria-busy", "true");
  });

  it("clears the typed challenge when the dialog is opened again", async () => {
    const user = userEvent.setup();
    const { rerender } = render(dialogElement({ challenge: "TERMINATE" }));
    await user.type(screen.getByRole("textbox"), "TERM");

    rerender(dialogElement({ open: false, challenge: "TERMINATE" }));
    rerender(dialogElement({ open: true, challenge: "TERMINATE" }));

    expect(screen.getByRole("textbox")).toHaveValue("");
  });

  describe("blockFileDrops", () => {
    const fileDrag = { dataTransfer: { files: [], items: [], types: ["Files"] } };

    it("cancels a file drag and drop on the dialog and on its content only when opted in", () => {
      const { dialog } = renderDialog({ blockFileDrops: true });
      const description = within(dialog).getByText("This starts billing.");

      expect(fireEvent.dragOver(description, fileDrag)).toBe(false);
      expect(fireEvent.drop(description, fileDrag)).toBe(false);
      expect(fireEvent.drop(dialog, fileDrag)).toBe(false);
    });

    it("leaves a drag without files alone and keeps the default for dialogs that do not opt in", () => {
      const optedIn = renderDialog({ blockFileDrops: true });
      const text = { dataTransfer: { files: [], items: [], types: ["text/plain"] } };
      expect(fireEvent.dragOver(optedIn.dialog, text)).toBe(true);
      optedIn.unmount();

      const { dialog } = renderDialog();
      expect(fireEvent.drop(dialog, fileDrag)).toBe(true);
    });

    it("does not override the drop effect of a zone that already took the event", () => {
      const { dialog } = renderDialog({
        blockFileDrops: true,
        children: (
          <div
            data-testid="zone"
            onDragOver={(event) => {
              event.preventDefault();
              event.dataTransfer.dropEffect = "copy";
            }}
          />
        ),
      });
      const dataTransfer = { files: [], items: [], types: ["Files"], dropEffect: "" };

      fireEvent.dragOver(within(dialog).getByTestId("zone"), { dataTransfer });

      expect(dataTransfer.dropEffect).toBe("copy");
    });
  });

  describe("closeOnBackdrop, confirmPlacement and dismissVariant", () => {
    /** Fires a pointer event with coordinates; jsdom reports an all-zero rect, so 500/500 lies outside it. */
    function pointer(type: "pointerdown" | "click", target: Element, clientX = 500, clientY = 500) {
      fireEvent(
        target,
        new MouseEvent(type, { bubbles: true, cancelable: true, clientX, clientY }),
      );
    }

    it("calls onCancel once for a pointerdown and click on the backdrop", () => {
      const { dialog, onCancel } = renderDialog({ closeOnBackdrop: true });

      pointer("pointerdown", dialog);
      pointer("click", dialog);

      expect(onCancel).toHaveBeenCalledOnce();
    });

    it("ignores a click on a descendant", () => {
      const { dialog, onCancel } = renderDialog({ closeOnBackdrop: true });
      const description = within(dialog).getByText("This starts billing.");

      pointer("pointerdown", description);
      pointer("click", description);

      expect(onCancel).not.toHaveBeenCalled();
    });

    it("ignores a drag that starts inside the dialog and ends on the backdrop", () => {
      const { dialog, onCancel } = renderDialog({ closeOnBackdrop: true });
      const description = within(dialog).getByText("This starts billing.");

      pointer("pointerdown", description);
      pointer("click", dialog);

      expect(onCancel).not.toHaveBeenCalled();
    });

    it("ignores a click on the dialog box itself inside its rect", () => {
      const { dialog, onCancel } = renderDialog({ closeOnBackdrop: true });

      pointer("pointerdown", dialog, 0, 0);
      pointer("click", dialog, 0, 0);

      expect(onCancel).not.toHaveBeenCalled();
    });

    it("ignores a backdrop click while busy", () => {
      const { dialog, onCancel } = renderDialog({ closeOnBackdrop: true, busy: true });

      pointer("pointerdown", dialog);
      pointer("click", dialog);

      expect(onCancel).not.toHaveBeenCalled();
    });

    it("does nothing on a backdrop click without closeOnBackdrop", () => {
      const { dialog, onCancel } = renderDialog();

      pointer("pointerdown", dialog);
      pointer("click", dialog);

      expect(onCancel).not.toHaveBeenCalled();
    });

    it("does not reuse a recorded pointerdown for a later click", () => {
      const { dialog, onCancel } = renderDialog({ closeOnBackdrop: true });

      pointer("pointerdown", dialog);
      pointer("click", dialog);
      pointer("click", dialog);

      expect(onCancel).toHaveBeenCalledOnce();
    });

    it("renders confirm, dismiss, extra action for confirmPlacement start", () => {
      const { dialog } = renderDialog({
        confirmPlacement: "start",
        extraAction: { label: "Review changes", variant: "gold", onClick: () => undefined },
      });

      expect(
        within(dialog)
          .getAllByRole("button")
          .map((button) => button.textContent),
      ).toEqual(["Start", "Keep off", "Review changes"]);
    });

    it("keeps dismiss, confirm, extra action by default", () => {
      const { dialog } = renderDialog({
        extraAction: { label: "Review changes", variant: "gold", onClick: () => undefined },
      });

      expect(
        within(dialog)
          .getAllByRole("button")
          .map((button) => button.textContent),
      ).toEqual(["Keep off", "Start", "Review changes"]);
    });

    it("styles the dismiss button with dismissVariant and keeps ghost by default", () => {
      const { rerender } = renderDialog();
      expect(screen.getByRole("button", { name: "Keep off" })).toHaveClass("button--ghost");

      rerender(dialogElement({ dismissVariant: "primary" }));
      expect(screen.getByRole("button", { name: "Keep off" })).toHaveClass("button--primary");
      expect(screen.getByRole("button", { name: "Keep off" })).not.toHaveClass("button--ghost");
    });

    it.each(["end", "start"] as const)(
      "focuses the dismiss button for initialFocus dismiss with placement %s",
      (confirmPlacement) => {
        renderDialog({ initialFocus: "dismiss", confirmPlacement });

        expect(screen.getByRole("button", { name: "Keep off" })).toHaveFocus();
      },
    );
  });
});

describe("PageHeader display typography (CON-01)", () => {
  const tokens = parseCustomProperties(readProjectFile("src/styles/tokens.css"));
  const sharedCss = readProjectFile("src/components/shared-primitives.css");

  /** Replaces every var() reference with the literal value declared in tokens.css. */
  function resolveVariables(value: string): string {
    return value.replace(/var\(\s*(--[\w-]+)\s*\)/g, (_whole, name: string) =>
      resolveToken(name, [tokens]),
    );
  }

  it("sets the h1 to the Display token (1.75rem, 700, 1.2) without uppercase or tracking", () => {
    render(<PageHeader title="Members" />);

    const style = getComputedStyle(screen.getByRole("heading", { level: 1, name: "Members" }));
    expect(resolveVariables(style.fontSize)).toBe("1.75rem");
    expect(resolveVariables(style.fontWeight)).toBe("700");
    expect(resolveVariables(style.lineHeight)).toBe("1.2");
    expect(style.textTransform).toBe("none");
    expect(style.letterSpacing).not.toMatch(/^-/);
  });

  it("wraps long titles with overflow-wrap anywhere and never clamps lines or viewport type", () => {
    const heading = /\.page-header\s+:is\(h1[^{]*\{([^}]*)\}/.exec(sharedCss)?.[1] ?? "";

    expect(heading).toMatch(/overflow-wrap:\s*anywhere/);
    expect(heading).not.toMatch(/line-clamp|clamp\(|\dvw|text-overflow/);
  });

  it("keeps the kicker, title, description, meta and actions in DOM order", () => {
    const { container } = render(
      <PageHeader
        kicker="Command"
        title="Members"
        description="Everyone in the unit."
        meta={<span>Meta chip</span>}
        actions={<button type="button">Invite</button>}
      />,
    );

    const order = [".kicker", "h1", "p", ".page-header__meta", ".page-header__actions"].map(
      (selector) => container.querySelector(selector) as Node,
    );
    for (let index = 1; index < order.length; index++) {
      expect(
        (order[index - 1] as Node).compareDocumentPosition(order[index] as Node) &
          Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
    }
  });

  it("defines the description measure token", () => {
    expect(tokens.get("--utf-measure")).toBe("45rem");
    expect(sharedCss).toContain("var(--utf-measure)");
  });
});

describe("StatusBadge and StatePanel on state tokens (D-05, CON-02)", () => {
  const sharedCss = readProjectFile("src/components/shared-primitives.css");

  /** Returns the declaration block of the first top-level rule whose selector is exactly `selector`. */
  function ruleBody(selector: string): string {
    const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    return new RegExp(`(?:^|\\n)${escaped}\\s*\\{([^}]*)\\}`).exec(sharedCss)?.[1] ?? "";
  }

  it.each(["info", "success", "warning", "danger"] as const)(
    "colours the %s badge from the matching state tokens",
    (tone) => {
      const body = ruleBody(`.status-badge--${tone}`);

      expect(body).toContain(`var(--utf-state-${tone}-fg)`);
      expect(body).toContain(`var(--utf-state-${tone}-bg)`);
      expect(body).toContain(`var(--utf-state-${tone}-border)`);
    },
  );

  it("defaults the badge to the neutral tokens and lets it wrap without an ellipsis", () => {
    const body = ruleBody(".status-badge");

    expect(body).toContain("var(--utf-state-neutral-fg)");
    expect(body).toContain("var(--utf-state-neutral-bg)");
    expect(body).toContain("var(--utf-state-neutral-border)");
    expect(body).toMatch(/white-space:\s*normal/);
    expect(body).not.toContain("ellipsis");
  });

  it.each(["neutral", "success", "warning", "danger", "info"] as const)(
    "renders the %s tone class and keeps the content",
    (tone) => {
      render(<StatusBadge tone={tone}>Label</StatusBadge>);

      expect(screen.getByText("Label")).toHaveClass("status-badge", `status-badge--${tone}`);
    },
  );

  it("raises the panel surface with a divider border and a token stripe", () => {
    const body = ruleBody(".state-panel");

    expect(body).toContain("var(--utf-surface-raised)");
    expect(body).toMatch(/border:\s*1px solid var\(--utf-border-divider\)/);
    expect(body).toMatch(/border-inline-start:\s*var\(--utf-space-xs\) solid/);
    expect(body).toMatch(/padding:\s*var\(--utf-space-lg\)/);
  });

  it("sets the panel title to the Heading token and the tone from the state tokens", () => {
    const title = ruleBody(".state-panel h2");

    expect(title).toContain("var(--utf-text-heading)");
    expect(title).toContain("var(--utf-weight-bold)");
    expect(title).toMatch(/text-transform:\s*none/);
    for (const [kind, tone] of [
      ["error", "danger"],
      ["warning", "warning"],
      ["success", "success"],
      ["info", "info"],
    ]) {
      expect(ruleBody(`.state-panel--${kind}`)).toContain(`var(--utf-state-${tone}-fg)`);
    }
  });

  it("never uses the legacy quiet colour in the shared stylesheet", () => {
    expect(sharedCss).not.toContain("--quiet");
  });

  it("keeps the error panel an alert and every other panel a status with a hidden icon", () => {
    const { container, rerender } = render(<StatePanel kind="error">Failed</StatePanel>);
    expect(screen.getByRole("alert")).toHaveClass("state-panel--error");
    expect(container.querySelector(".state-panel__icon")).toHaveAttribute("aria-hidden", "true");

    rerender(<StatePanel kind="forbidden">No access</StatePanel>);
    expect(screen.getByRole("status")).toHaveClass("state-panel--forbidden");
    expect(container.querySelector(".state-panel__icon")).toHaveAttribute("aria-hidden", "true");
  });
});

describe("dialog shell geometry (D-10)", () => {
  const sharedCss = readProjectFile("src/components/shared-primitives.css");

  /** Returns the declaration block of the first rule with exactly this selector, or an empty string. */
  function ruleBody(selector: string): string {
    const escaped = selector.replace(/[.*+?^${}()|[\]\\:]/g, "\\$&");
    return new RegExp(`(?:^|\\n)${escaped}\\s*\\{([^}]*)\\}`).exec(sharedCss)?.[1] ?? "";
  }

  /** Returns the joined text of every media query whose prelude is `(prelude)`. */
  function mediaBody(prelude: string): string {
    const blocks: string[] = [];
    for (
      let start = sharedCss.indexOf(`@media (${prelude})`);
      start >= 0;
      start = sharedCss.indexOf(`@media (${prelude})`, start + 1)
    ) {
      let depth = 0;
      for (let index = sharedCss.indexOf("{", start); index < sharedCss.length; index++) {
        if (sharedCss[index] === "{") depth += 1;
        if (sharedCss[index] === "}") depth -= 1;
        if (depth === 0) {
          blocks.push(sharedCss.slice(start, index + 1));
          break;
        }
      }
    }
    return blocks.join("\n");
  }

  it("sizes the dialog by the width and gutter tokens and caps its height", () => {
    expect(ruleBody(":where(.dialog)")).toContain(
      "width: min(var(--utf-dialog-width), 100vw - var(--utf-dialog-gutter))",
    );
    const shell = ruleBody(".dialog");
    expect(shell).toContain("max-height: calc(100dvh - var(--utf-dialog-gutter))");
    expect(shell).toContain("overflow-y: auto");
    expect(ruleBody(".dialog .dialog__body")).toMatch(/overflow-y:\s*auto/);
  });

  it("puts every child on one left edge with the lg padding, md at 520px and below", () => {
    const form = ruleBody(".dialog form");

    expect(form).toContain("flex-direction: column");
    expect(form).toContain("padding: var(--utf-space-lg)");
    expect(mediaBody("max-width: 520px")).toMatch(
      /\.dialog form\s*\{[^}]*padding:\s*var\(--utf-space-md\)/,
    );
  });

  it("right-aligns the actions above 520px and stacks them full width below", () => {
    expect(ruleBody(".dialog__actions")).toContain("justify-content: flex-end");
    const compact = mediaBody("max-width: 520px");
    expect(compact).toMatch(/\.dialog__actions\s*\{[^}]*flex-direction:\s*column-reverse/);
    expect(compact).toMatch(/\.dialog__actions > \*\s*\{[^}]*width:\s*100%/);
    expect(mediaBody("max-width: 800px")).toContain("var(--utf-control-height-touch)");
  });

  it("lays facts out in two columns above 520px and stacks them below", () => {
    expect(ruleBody(".dialog__fact")).toMatch(
      /grid-template-columns:\s*minmax\(0, 1fr\) minmax\(0, 2fr\)/,
    );
    expect(mediaBody("max-width: 520px")).toMatch(
      /\.dialog__fact\s*\{[^}]*grid-template-columns:\s*minmax\(0, 1fr\)/,
    );
  });
});

describe("PageHeader additions", () => {
  it("renders the meta slot after the heading and makes the heading programmatically focusable", () => {
    const { container } = render(
      <PageHeader title="Servers" description="Fleet overview." meta={<span>Meta chip</span>} />,
    );

    const heading = screen.getByRole("heading", { level: 1, name: "Servers" });
    expect(heading).toHaveAttribute("tabindex", "-1");
    const meta = container.querySelector(".page-header__meta");
    expect(meta).toHaveTextContent("Meta chip");
    expect(
      heading.compareDocumentPosition(meta as Node) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    heading.focus();
    expect(heading).toHaveFocus();
  });
});

describe("StatePanel icon", () => {
  const kinds: StateKind[] = [
    "loading",
    "empty",
    "error",
    "forbidden",
    "success",
    "warning",
    "info",
  ];

  it("renders a supplied icon inside the hidden icon wrapper instead of the default icon", () => {
    const { container } = render(
      <StatePanel kind="info" title="Live updates paused" icon={<span data-testid="custom-icon" />}>
        Body
      </StatePanel>,
    );

    const wrapper = container.querySelector(".state-panel__icon");
    expect(wrapper).toHaveAttribute("aria-hidden", "true");
    expect(within(wrapper as HTMLElement).getByTestId("custom-icon")).toBeInTheDocument();
    expect(within(wrapper as HTMLElement).queryByTestId("fluent-icon")).not.toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveClass("state-panel", "state-panel--info");
  });

  it("replaces the loading spinner when an icon is supplied", () => {
    const { container } = render(
      <StatePanel kind="loading" icon={<span data-testid="custom-icon" />}>
        Loading
      </StatePanel>,
    );

    const wrapper = container.querySelector(".state-panel__icon") as HTMLElement;
    expect(within(wrapper).getByTestId("custom-icon")).toBeInTheDocument();
    expect(wrapper.querySelector("[role='status']")).toBeNull();
  });

  it.each(kinds)("keeps the default %s rendering without an icon", (kind) => {
    const { container } = render(
      <StatePanel kind={kind} title="Title">
        Body
      </StatePanel>,
    );

    const panel = container.querySelector("section.state-panel") as HTMLElement;
    expect(panel).toHaveClass(`state-panel--${kind}`);
    expect(panel).toHaveAttribute("role", kind === "error" ? "alert" : "status");
    if (kind === "error") {
      expect(panel).not.toHaveAttribute("aria-live");
    } else {
      expect(panel).toHaveAttribute("aria-live", "polite");
    }
    expect(within(panel).getByRole("heading", { level: 2, name: "Title" })).toBeVisible();
    const wrapper = panel.querySelector(".state-panel__icon") as HTMLElement;
    if (kind === "loading") {
      expect(wrapper.querySelector("[role='status']")).not.toBeNull();
      expect(within(wrapper).queryByTestId("fluent-icon")).not.toBeInTheDocument();
    } else {
      expect(within(wrapper).getAllByTestId("fluent-icon")).toHaveLength(1);
    }
  });

  it("keeps the error panel assertive", async () => {
    await i18nReady;
    render(<StatePanel kind="error">Body</StatePanel>);
    const alert = screen.getByRole("alert");
    expect(alert).not.toHaveAttribute("aria-live");
    expect(screen.queryByRole("status")).not.toBeInTheDocument();

    renderWithProviders(<ErrorState onRetry={() => undefined} />);
    const alerts = screen.getAllByRole("alert");
    expect(alerts).toHaveLength(2);
    expect(alerts[1]).toHaveClass("state-panel--error");
    expect(alerts[1]).not.toHaveAttribute("aria-live");
  });
});
