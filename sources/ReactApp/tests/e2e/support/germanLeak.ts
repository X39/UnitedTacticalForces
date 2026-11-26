import type { Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import { join } from "node:path";

/**
 * The German leak detector of Phase 69 (D-13, L10N-03).
 *
 * A page rendered in German must not show English UI text. The detector is deterministic and explainable: every
 * finding names the text and the rule that matched, so a failing walk shows what to translate.
 *
 * Two rules decide:
 *
 * 1. The text contains an English locale value (case-insensitive, on word boundaries) whose German value differs,
 *    that is not allow-listed in `identical-allowed.json` and that does not itself occur inside German text. Only
 *    values of at least two words or six characters count, so `Name` or `Status` never match (research finding F5).
 * 2. The text holds two or more English function words from {@link functionWords}. `an` and `will` are not on the
 *    list because they are ordinary German words.
 *
 * Collection skips every subtree that is not German UI text: an element or ancestor with a `lang` other than `de`
 * (API prose rendered by `ApiText`), `bdi`, `code`, `kbd`, `time` and `[data-user-content]`, plus hidden elements.
 *
 * This module imports Playwright types only and reads the locale files with `node:fs`, so Vitest can import the pure
 * functions for the non-vacuity unit test (`tests/audit/german-leak.test.ts`).
 */

/** English function words; two of them in one text are a leak. `an` and `will` are German words and stay out. */
export const functionWords: readonly string[] = [
  "the",
  "and",
  "to",
  "of",
  "is",
  "your",
  "you",
  "for",
  "with",
  "this",
  "that",
  "are",
  "not",
  "can",
  "or",
  "from",
  "has",
];

/** The smallest number of characters of a candidate that has a single word. */
const minimumCandidateLength = 6;

/** The smallest number of words of a candidate that is shorter than {@link minimumCandidateLength}. */
const minimumCandidateWords = 2;

/** The number of function words in one text that makes it a leak. */
const functionWordThreshold = 2;

/** Matches an interpolation placeholder of the locale files, for example `{{count}}` or `{0}`. */
const placeholderPattern = /\{\{[^}]*\}\}|\{\d+\}/g;

/** One text that looks like English on a German page, with the rule that matched. */
export interface GermanLeak {
  /** The visible text, with whitespace collapsed. */
  text: string;
  /** The rule that matched, naming the English locale value or the function words. */
  reason: string;
}

/**
 * Collapses runs of whitespace to one space and trims the ends.
 *
 * @param value The raw text.
 * @returns The normalized text.
 */
function normalize(value: string): string {
  return value.replace(/\s+/g, " ").trim();
}

/**
 * Tells whether a normalized candidate is long enough to be matched against page text.
 *
 * @param value The lowercase candidate.
 * @returns True for at least two words or at least six characters.
 */
function isSubstantial(value: string): boolean {
  return value.split(" ").length >= minimumCandidateWords || value.length >= minimumCandidateLength;
}

/**
 * Computes the English leak candidates: the lowercase English locale values that must never appear on a German page.
 *
 * A value is a candidate when the German value of its key differs (a missing German value differs), its key is not
 * allow-listed, it has at least two words or six characters, and it does not occur inside any German value. A value
 * with placeholders contributes its literal segments, because the rendered text no longer holds the placeholder.
 *
 * @param en The English locale values by key.
 * @param de The German locale values by key.
 * @param allowed The keys listed in `identical-allowed.json`.
 * @returns The distinct lowercase candidates.
 */
export function leakCandidates(
  en: Record<string, string>,
  de: Record<string, string>,
  allowed: ReadonlySet<string>,
): string[] {
  const germanText = normalize(Object.values(de).join("\n")).toLowerCase();
  const candidates = new Set<string>();
  for (const [key, value] of Object.entries(en)) {
    if (de[key] === value || allowed.has(key)) continue;
    for (const segment of value.split(placeholderPattern)) {
      const candidate = normalize(segment).toLowerCase();
      if (candidate === "" || !isSubstantial(candidate)) continue;
      if (germanText.includes(candidate)) continue;
      candidates.add(candidate);
    }
  }
  return [...candidates];
}

/**
 * Tells whether a character is a letter in any script.
 *
 * @param character One character, or the empty string at a text boundary.
 * @returns True for a letter.
 */
function isLetter(character: string): boolean {
  return character !== "" && /\p{L}/u.test(character);
}

/**
 * Tells whether a phrase occurs in a text on word boundaries, so `active` does not match inside `interactive`.
 *
 * A phrase glued to a prefix with a colon, as `operate` in the permission claim id `sv:operate`, is the tail of an
 * identifier that the API sends and that no locale file can translate, so it is not an occurrence.
 *
 * @param haystack The lowercase text.
 * @param needle The lowercase phrase.
 * @returns True when the phrase occurs with no letter directly before or after it and is not the tail of a
 * colon-joined identifier.
 */
function containsPhrase(haystack: string, needle: string): boolean {
  let index = haystack.indexOf(needle);
  while (index !== -1) {
    const before = index === 0 ? "" : (haystack[index - 1] ?? "");
    const after = haystack[index + needle.length] ?? "";
    const identifierTail = before === ":" && isLetter(index < 2 ? "" : (haystack[index - 2] ?? ""));
    if (!isLetter(before) && !isLetter(after) && !identifierTail) return true;
    index = haystack.indexOf(needle, index + 1);
  }
  return false;
}

/**
 * Finds the texts that look like English.
 *
 * @param texts The visible texts of a page; duplicates and surrounding whitespace do not matter.
 * @param candidates The lowercase English candidates from {@link leakCandidates}.
 * @returns One finding per distinct leaking text, in order of first appearance.
 */
export function findLeaks(texts: readonly string[], candidates: readonly string[]): GermanLeak[] {
  const functionWordSet = new Set(functionWords);
  const seen = new Set<string>();
  const leaks: GermanLeak[] = [];
  for (const raw of texts) {
    const text = normalize(raw);
    if (text === "" || seen.has(text)) continue;
    seen.add(text);
    const lower = text.toLowerCase();
    const candidate = candidates.find((value) => containsPhrase(lower, value));
    if (candidate !== undefined) {
      leaks.push({ text, reason: `contains the English locale value "${candidate}"` });
      continue;
    }
    const words = lower.match(/[\p{L}']+/gu) ?? [];
    const hits = words.filter((word) => functionWordSet.has(word));
    if (hits.length >= functionWordThreshold) {
      leaks.push({ text, reason: `contains the English function words ${hits.join(", ")}` });
    }
  }
  return leaks;
}

/**
 * Reads a locale file of `src/locales`.
 *
 * @param name The file name, for example `en.json`.
 * @returns The parsed JSON object.
 */
function readLocaleFile(name: string): Record<string, string> {
  const path = join(import.meta.dirname, "..", "..", "..", "src", "locales", name);
  return JSON.parse(readFileSync(path, "utf8")) as Record<string, string>;
}

/** The candidates computed from the real locale files, built on first use. */
let cachedCandidates: string[] | undefined;

/**
 * Returns the leak candidates of the real locale files.
 *
 * @returns The lowercase candidates, computed once per worker.
 */
export function realLeakCandidates(): string[] {
  cachedCandidates ??= leakCandidates(
    readLocaleFile("en.json"),
    readLocaleFile("de.json"),
    new Set(Object.keys(readLocaleFile("identical-allowed.json"))),
  );
  return cachedCandidates;
}

/**
 * Collects the visible UI texts of a page: text nodes plus `aria-label`, `title`, `placeholder`, `alt` and option
 * `label` attributes, outside the excluded subtrees and hidden elements.
 *
 * @param page The page to read.
 * @returns The texts, one per node or attribute, not deduplicated.
 */
export async function collectVisibleTexts(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const excludedSelector = "bdi, code, kbd, time, [data-user-content], script, style, noscript";
    const attributes = ["aria-label", "title", "placeholder", "alt"];

    /** Tells whether an element is outside German UI text because of its tag, marker or language. */
    const isExcluded = (element: Element): boolean => {
      if (element.closest(excludedSelector) !== null) return true;
      for (let node: Element | null = element; node !== null; node = node.parentElement) {
        const language = node.getAttribute("lang");
        if (language !== null && language !== "") {
          return !language.toLowerCase().startsWith("de");
        }
      }
      return false;
    };

    /** Tells whether an element renders. */
    const isRendered = (element: Element): boolean =>
      element.checkVisibility({ visibilityProperty: true, opacityProperty: false });

    const texts: string[] = [];
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      const parent = node.parentElement;
      const value = node.nodeValue?.trim() ?? "";
      if (parent === null || value === "" || isExcluded(parent) || !isRendered(parent)) continue;
      texts.push(value);
    }
    for (const element of document.body.querySelectorAll("*")) {
      if (isExcluded(element) || !isRendered(element)) continue;
      for (const name of attributes) {
        const value = element.getAttribute(name)?.trim() ?? "";
        if (value !== "") texts.push(value);
      }
      if (element instanceof HTMLOptionElement) {
        const label = element.getAttribute("label")?.trim() ?? "";
        if (label !== "") texts.push(label);
      }
    }
    return texts;
  });
}

/**
 * Fails when the page shows English text on a German page.
 *
 * The check also fails when the page has no visible text at all, because a walk over an empty page would pass
 * vacuously.
 *
 * @param page The page, already rendered in German.
 * @param knownFindings Exact texts (whitespace-insensitive, case-insensitive) that a plan recorded as leaks it may
 *   not fix itself; they are subtracted and returned.
 * @returns The known texts that were present and subtracted, so the caller can annotate them.
 * @throws Error when the page has no text or a leak is left after subtracting the known findings.
 */
export async function assertNoGermanLeak(
  page: Page,
  knownFindings: readonly string[] | undefined = undefined,
): Promise<string[]> {
  const texts = await collectVisibleTexts(page);
  if (texts.length === 0) {
    throw new Error("german leak check: the page has no visible text, so the check proves nothing");
  }
  const known = new Set((knownFindings ?? []).map((value) => normalize(value).toLowerCase()));
  const leaks = findLeaks(texts, realLeakCandidates());
  const subtracted = leaks.filter((leak) => known.has(leak.text.toLowerCase()));
  const remaining = leaks.filter((leak) => !known.has(leak.text.toLowerCase()));
  if (remaining.length > 0) {
    const lines = remaining.map((leak) => `  - "${leak.text}": ${leak.reason}`);
    throw new Error(
      `german leak check: ${remaining.length} English text(s) on a German page\n${lines.join("\n")}`,
    );
  }
  return subtracted.map((leak) => leak.text);
}
