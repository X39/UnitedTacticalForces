import { expect, test } from "@playwright/test";
import {
  checkDialogGeometry,
  checkHeadings,
  checkReducedMotion,
  checkReflow,
  checkStickyCoverage,
  checkTextSpacing,
  checkZoomReflow,
  scanAxe,
  walkKeyboard,
} from "../support/a11y";

/**
 * Self-test of the audit helpers (non-vacuity).
 *
 * Every registry audit relies on `support/a11y.ts`, so a helper that silently accepts everything would make every
 * audit green. Each helper is driven here against a hand-built page that keeps the contract and a page that breaks
 * it, with no app and no API. The pages use no viewport meta tag, so the helpers are driven in the desktop project
 * only.
 */

test.skip(({ isMobile }) => isMobile, "The self-test pages carry no viewport meta tag.");

/** Shared page chrome: dark surface, the gold-bright focus ring, a skip link and a dark dialog surface. */
const base = `
<style>
  body{background:#101318;color:#fff;margin:0;font:16px sans-serif} a{color:#dec982}
  :focus-visible{outline:2px solid #dec982;outline-offset:3px}
  .skip-link{position:fixed;left:1rem;top:-5rem;background:#baa05a;color:#000}
  .skip-link:focus{top:1rem}
  dialog{background:#1a1f27;color:#fff}
</style>
<a class="skip-link" href="#main">Skip</a>`;

test("a page that keeps the contract passes every helper", async ({ page }) => {
  await page.setContent(
    `${base}<main id="main" tabindex="-1"><h1>Title</h1><button>One</button><a href="#x">Two</a><input aria-label="in"></main>`,
  );
  await walkKeyboard(page);
  await checkHeadings(page);
  await checkReflow(page);
  await checkTextSpacing(page);
  await checkReducedMotion(page);
  await checkZoomReflow(page);
  await scanAxe(page, { include: "main" });
});

test("a time input counts as one stop although the browser gives it several", async ({ page }) => {
  await page.setContent(
    `${base}<main id="main" tabindex="-1"><h1>T</h1><button>One</button><label>When <input type="time" value="18:00"></label><button>Two</button><button>Three</button></main>`,
  );
  // Without the segment rule the walk spends its presses on the segments and reports the last button unreachable.
  await walkKeyboard(page);
  await checkStickyCoverage(page);
});

test("axe fails on a violation, fails on an include that matches nothing, and honors a known finding", async ({
  page,
}) => {
  await page.setContent(`${base}<main id="main"><h1>T</h1><img src="data:," /></main>`);
  await expect(scanAxe(page, { include: "main" })).rejects.toThrow(/image-alt/);
  await expect(scanAxe(page, { include: "#missing" })).rejects.toThrow(/matched no element/);
  await scanAxe(page, {
    include: "main",
    knownFindings: [{ id: "XPF-T", rule: "image-alt", target: "img" }],
  });
});

test("a hidden focus ring, a positive tabindex and a missing h1 are caught", async ({ page }) => {
  await page.setContent(
    `${base}<main id="main" tabindex="-1"><button style="outline:none">One</button></main>`,
  );
  await expect(walkKeyboard(page)).rejects.toThrow(/focus ring/);
  await expect(checkHeadings(page)).rejects.toThrow(/exactly one visible h1, found 0/);
  await page.setContent(
    `${base}<main id="main" tabindex="-1"><h1>T</h1><button>One</button><button tabindex="3">Two</button></main>`,
  );
  await expect(walkKeyboard(page)).rejects.toThrow(/positive tabindex/);
});

test("a known keyboard finding is subtracted by rule and selector only", async ({ page }) => {
  await page.setContent(
    `${base}<main id="main" tabindex="-1"><h1>T</h1><button class="b" style="outline:none">One</button></main>`,
  );
  await walkKeyboard(page, {
    knownFindings: [{ id: "XPF-T", rule: "focus-ring", target: "button.b" }],
  });
  await expect(
    walkKeyboard(page, {
      knownFindings: [{ id: "XPF-T", rule: "focus-ring", target: "button.x" }],
    }),
  ).rejects.toThrow(/focus ring/);
});

test("overflow, clipped text and animation are caught", async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 568 });
  await page.setContent(
    `${base}<style>.w{width:500px;height:20px;background:#333}
    .c{display:inline-block;width:40px;overflow:hidden;white-space:nowrap}
    .a{animation:spin 2s infinite} @keyframes spin{to{transform:rotate(1turn)}}</style>
    <main id="main"><h1>T</h1><div class="w">wide</div><button class="c">Long button label here</button><div class="a">x</div></main>`,
  );
  await expect(checkReflow(page)).rejects.toThrow(/div\.w/);
  await expect(checkTextSpacing(page)).rejects.toThrow(/button\.c/);
  await expect(checkReducedMotion(page)).rejects.toThrow(/div\.a/);
});

test("a modal dialog walk traps focus and Escape returns focus to the invoker", async ({
  page,
}) => {
  await page.setContent(
    `${base}<main id="main" tabindex="-1"><h1>T</h1><button id="invoker">Open</button></main>
    <dialog id="d" aria-label="d"><h2>Dialog</h2><button data-dialog-dismiss>A</button><button>B</button></dialog>`,
  );
  await page.locator("#invoker").focus();
  await page.evaluate(() => (document.getElementById("d") as HTMLDialogElement).showModal());
  await walkKeyboard(page, { returnFocusTo: "#invoker" });
  expect(await page.evaluate(() => document.activeElement?.id)).toBe("invoker");
});

test("a modal dialog that loses focus on Escape is caught", async ({ page }) => {
  await page.setContent(
    `${base}<main id="main" tabindex="-1"><h1>T</h1><button id="invoker">Open</button></main>
    <dialog id="d" aria-label="d"><h2>Dialog</h2><button>A</button></dialog>`,
  );
  await page.evaluate(() => (document.getElementById("d") as HTMLDialogElement).showModal());
  await page.evaluate(() => (document.activeElement as HTMLElement).blur());
  await expect(walkKeyboard(page)).rejects.toThrow(/focus was lost|return/);
});

test("a sticky bar that covers the focused element is caught", async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 568 });
  await page.setContent(
    `${base}<style>.bar{position:fixed;left:0;right:0;bottom:0;height:150px;background:#223}</style>
    <main id="main" tabindex="-1"><h1>T</h1><div style="height:900px"></div><button>Late</button><div style="height:50px"></div></main>
    <div class="bar">bar</div>`,
  );
  await expect(checkStickyCoverage(page)).rejects.toThrow(/covered/);
});

test("dialog geometry catches a misaligned title and actions that are not right-aligned", async ({
  page,
}) => {
  await page.setViewportSize({ width: 800, height: 700 });
  await page.setContent(
    `${base}<main id="main"><h1>T</h1></main>
    <dialog open class="dialog" style="width:500px"><form style="padding:20px"><span class="kicker">K</span><h2 style="margin-left:12px">Title</h2><p>Body</p>
    <div class="dialog__actions" style="display:flex;justify-content:flex-start"><button>Cancel</button><button>OK</button></div></form></dialog>`,
  );
  const dialog = page.locator("dialog");
  await expect(checkDialogGeometry(page, dialog)).rejects.toThrow(/title.*left edge/s);
  await expect(checkDialogGeometry(page, dialog)).rejects.toThrow(/not right-aligned/);
});

test("dialog geometry accepts a reversed stack and rejects side-by-side actions at a phone width", async ({
  page,
}) => {
  await page.setViewportSize({ width: 400, height: 700 });
  const dialog = (direction: string, buttonCss: string) =>
    `${base}<main id="main"><h1>T</h1></main>
    <dialog open class="dialog" style="width:360px"><form style="padding:20px"><span class="kicker">K</span><h2>Title</h2><p>Body</p>
    <div class="dialog__actions" style="display:flex;flex-direction:${direction}"><button style="${buttonCss}">Cancel</button><button style="${buttonCss}">OK</button></div></form></dialog>`;
  const stacked = "width:100%;min-height:44px";
  await page.setContent(dialog("column-reverse", stacked));
  await checkDialogGeometry(page, page.locator("dialog"));
  await page.setContent(dialog("row", "min-height:44px"));
  await expect(checkDialogGeometry(page, page.locator("dialog"))).rejects.toThrow(
    /not stacked below|full container width/,
  );
});
