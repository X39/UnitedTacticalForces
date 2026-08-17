import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const styles = readFileSync(resolve(process.cwd(), "src", "styles.css"), "utf8");

describe("gallery asset picker layout", () => {
  it("keeps the dialog inside the viewport and reserves an internal scrollable workspace", () => {
    expect(styles).toMatch(
      /\.gallery-picker-dialog\s*\{[\s\S]*?width: min\(92vw, 960px\);[\s\S]*?height: min\(82dvh, 700px\);[\s\S]*?max-height: calc\(100dvh - 2rem\);[\s\S]*?overflow: hidden;/,
    );
    expect(styles).toMatch(
      /\.gallery-picker-dialog__shell\s*\{[\s\S]*?grid-template-rows: auto auto auto minmax\(0, 1fr\) auto;[\s\S]*?min-height: 0;[\s\S]*?overflow: hidden;/,
    );
    expect(styles).toMatch(
      /\.gallery-picker-dialog__workspace\s*\{[\s\S]*?min-height: 0;[\s\S]*?overflow: hidden;/,
    );
    expect(styles).toMatch(
      /\.gallery-picker-dialog__workspace > section,[\s\S]*?\.gallery-picker-dialog__workspace > aside\s*\{[\s\S]*?min-height: 0;[\s\S]*?overflow: auto;/,
    );
  });

  it("stacks results and selection into bounded rows on narrow viewports", () => {
    expect(styles).toMatch(
      /\.gallery-picker-dialog__workspace\s*\{[\s\S]*?grid-template-columns: 1fr;[\s\S]*?grid-template-rows: minmax\(0, 1fr\) minmax\(10rem, 0\.55fr\);/,
    );
  });
});
