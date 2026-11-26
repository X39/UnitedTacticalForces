import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const styles = readFileSync(
  resolve(process.cwd(), "src", "features", "admin", "mediaAdmin.css"),
  "utf8",
);

describe("gallery asset picker layout", () => {
  it("sizes the shared dialog shell to the viewport and reserves an internal scrollable workspace", () => {
    expect(styles).toMatch(
      /\.dialog\.dialog--gallery-picker\s*\{[\s\S]*?width: min\(60rem, 100vw - var\(--utf-dialog-gutter\)\);[\s\S]*?height: min\(82dvh, 44rem\);/,
    );
    expect(styles).toMatch(
      /\.dialog\.dialog--gallery-picker \.dialog__body\s*\{[\s\S]*?overflow: hidden;/,
    );
    expect(styles).toMatch(
      /\.gallery-picker__workspace\s*\{[\s\S]*?flex: 1 1 auto;[\s\S]*?min-height: 0;/,
    );
    expect(styles).toMatch(
      /\.gallery-picker__results,\s*\.gallery-picker__selection\s*\{[\s\S]*?min-height: 0;[\s\S]*?overflow: auto;/,
    );
  });

  it("stacks results and selection into bounded rows on narrow viewports", () => {
    expect(styles).toMatch(
      /@media \(max-width: 800px\)\s*\{[\s\S]*?\.gallery-picker__workspace\s*\{[\s\S]*?grid-template-columns: minmax\(0, 1fr\);[\s\S]*?grid-template-rows: minmax\(0, 1fr\) minmax\(10rem, 0\.55fr\);/,
    );
  });

  it("doubles the shell selector so the variant wins over the shared dialog rules", () => {
    expect(styles).not.toMatch(/^\.dialog--gallery-picker/m);
  });
});
