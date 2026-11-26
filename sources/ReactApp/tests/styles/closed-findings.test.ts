import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";

const sourceRoot = resolve(import.meta.dirname, "..", "..", "src");

function css(path: string): string {
  return readFileSync(join(sourceRoot, path), "utf8").replace(/\r\n/g, "\n");
}

/** Returns the declaration block of the first rule whose selector line equals `selector`. */
function ruleBody(text: string, selector: string): string {
  const start = text.indexOf(`${selector} {`);
  if (start < 0) throw new Error(`rule not found: ${selector}`);
  return text.slice(start, text.indexOf("}", start));
}

describe("stylesheet findings closed by the Phase 69 closure plan", () => {
  it("gives the attendance mark an explicit tone instead of a currentColor background", () => {
    const body = ruleBody(
      css("features/events-member/eventsMember.css"),
      ".em-attendance-choice__mark",
    );

    expect(body).not.toMatch(/background:\s*currentColor/i);
    expect(body).toMatch(/background:\s*var\(--em-choice-tone/);
  });

  it("raises the phone dialog action rule above the legacy button width", () => {
    const shared = css("components/shared-primitives.css");

    expect(shared).toMatch(/\.dialog \.dialog__actions > \* \{\s*width: 100%;/);
    expect(css("components/server/workspace-shell.css")).not.toContain("dialog--record");
  });

  it("lets the shared page header own the command center heading size and layout", () => {
    const commandCenter = css("components/commandCenter.css");

    expect(commandCenter).not.toMatch(/\.command-shell \.page-header h1/);
    expect(commandCenter).not.toMatch(
      /@media \(max-width: 780px\)[\s\S]*?\.command-shell \.page-header \{/,
    );
  });

  it("draws the focus ring of a command table row from the shared token", () => {
    const styles = css("styles.css");
    const body = ruleBody(styles, ".command-table tbody tr:focus-visible");

    expect(body).toContain("outline: var(--utf-focus-ring)");
  });
});
