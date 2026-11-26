import { readdir, readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const localeRoot = resolve(projectRoot, "src", "locales");

/**
 * Reads the command line options.
 *
 * `--english-allow` selects the allow list of the English function word scan. It defaults to
 * `src/locales/english-words-allowed.json`, which starts empty: measured on 2026-11-25, no correct German value
 * contains a listed function word once the WR-02 values are translated. It is where a future product name or title
 * that must keep an English function word gets its key and a reason. A key listed there is exempt from both
 * English-word rules, `[english-words]` and `[english-carryover]`.
 *
 * `--shared-words` selects the list of words that German legitimately shares with English. It defaults to
 * `src/locales/shared-words-allowed.json`; each word maps to one reason and suppresses `[english-carryover]` for that
 * word in every German value.
 */
function readOptions(argv) {
  const options = {
    en: resolve(localeRoot, "en.json"),
    de: resolve(localeRoot, "de.json"),
    allow: resolve(localeRoot, "identical-allowed.json"),
    englishAllow: resolve(localeRoot, "english-words-allowed.json"),
    sharedWords: resolve(localeRoot, "shared-words-allowed.json"),
    src: resolve(projectRoot, "src"),
    unused: false,
  };
  const usage =
    "Usage: check-locales.mjs [--en <path>] [--de <path>] [--allow <path>] [--english-allow <path>] " +
    "[--shared-words <path>] [--unused [--src <path>]]";
  const valueFlags = {
    "--en": "en",
    "--de": "de",
    "--allow": "allow",
    "--english-allow": "englishAllow",
    "--shared-words": "sharedWords",
    "--src": "src",
  };
  for (let index = 0; index < argv.length; index += 1) {
    const flag = argv[index] ?? "";
    if (flag === "--unused") {
      options.unused = true;
      continue;
    }
    const value = argv[index + 1];
    const optionName = valueFlags[flag];
    if (optionName === undefined || value === undefined) {
      throw new Error(usage);
    }
    options[optionName] = resolve(value);
    index += 1;
  }
  return options;
}

async function readFlatObject(path) {
  const parsed = JSON.parse(await readFile(path, "utf8"));
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) {
    throw new Error(`${path} must contain a flat JSON object.`);
  }
  for (const [key, value] of Object.entries(parsed)) {
    if (typeof value !== "string")
      throw new Error(`${path}: the value of "${key}" is not a string.`);
  }
  return parsed;
}

/** Returns the sorted, de-duplicated placeholder names of `{{name}}` and `{n}` / `{n:Nk}` styles. */
function placeholders(value) {
  const names = new Set();
  for (const match of value.matchAll(/\{\{\s*([^{}\s]+)\s*\}\}/g)) names.add(match[1]);
  for (const match of value.matchAll(/(?<!\{)\{(\d+)(?::[^{}]*)?\}(?!\})/g)) names.add(match[1]);
  return [...names].sort().join(",");
}

/**
 * Normalizes a value for the case-insensitive identical comparison: trims it, collapses whitespace runs to one space
 * and lower-cases it.
 */
function comparable(value) {
  return value.trim().replace(/\s+/g, " ").toLowerCase();
}

/**
 * English function words that must not appear in a German value. They were chosen because none of them is a German
 * word. The words in {@link germanWordsLikeEnglish} are deliberately excluded.
 */
const englishFunctionWords = [
  "the",
  "and",
  "with",
  "this",
  "that",
  "these",
  "those",
  "has",
  "have",
  "for",
  "of",
  "is",
  "are",
  "your",
  "you",
  "from",
  "to",
  "or",
  "how",
  "what",
  "which",
  "when",
  "can",
  "new",
];

/** Whole-word, case-insensitive match of any listed word; a Unicode letter, digit or underscore on a side blocks it. */
const englishFunctionWordPattern = new RegExp(
  `(?<![\\p{L}\\p{N}_])(?:${englishFunctionWords.join("|")})(?![\\p{L}\\p{N}_])`,
  "giu",
);

/**
 * Words that are ordinary German words or collide with German text, so a German value may repeat them from its
 * English source: an, in, am, um, so, was, will, die and man are ordinary German words, and not, do, all, it and on
 * collide with German text (Not, Do for Donnerstag, All, IT and Add-on). They are skipped by `[english-carryover]`.
 */
const germanWordsLikeEnglish = [
  "an",
  "in",
  "am",
  "um",
  "so",
  "was",
  "will",
  "die",
  "man",
  "not",
  "do",
  "all",
  "it",
  "on",
];

/**
 * Removes the parts of a value that are not natural-language words: `{{name}}`, `{n}` and `{n:format}` placeholders,
 * http or https URLs and identifiers that contain an underscore.
 *
 * @param value The locale value.
 * @returns The value with those parts replaced by a space.
 */
function wordText(value) {
  return value
    .replace(/\{\{[^{}]*\}\}/g, " ")
    .replace(/\{\d+(?::[^{}]*)?\}/g, " ")
    .replace(/https?:\/\/\S+/gi, " ")
    .replace(/[\p{L}\p{N}]*_[\p{L}\p{N}_]*/gu, " ");
}

/**
 * Splits a value into lower-case words. A word is a maximal run that starts with a Unicode letter followed by Unicode
 * letters, digits, apostrophes (straight and typographic) or hyphens, so a hyphenated compound is one word.
 *
 * @param value The locale value.
 * @returns The lower-case words of {@link wordText} in order of appearance.
 */
function wordsOf(value) {
  return (wordText(value).match(/\p{L}[\p{L}\p{N}'’-]*/gu) ?? []).map((word) => word.toLowerCase());
}

/**
 * Finds the English function words of a German value. Placeholders (`{{name}}`, `{n}` and `{n:format}`) and http or
 * https URLs are removed first, so a word inside them does not count.
 *
 * @param value The German value.
 * @returns The distinct lower-case words found, sorted and joined by a comma and a space; empty when none is found.
 */
function englishWordsIn(value) {
  const text = value
    .replace(/\{\{[^{}]*\}\}/g, " ")
    .replace(/\{\d+(?::[^{}]*)?\}/g, " ")
    .replace(/https?:\/\/\S+/gi, " ");
  const found = new Set(
    (text.match(englishFunctionWordPattern) ?? []).map((word) => word.toLowerCase()),
  );
  return [...found].sort().join(", ");
}

/**
 * Finds the words a German value keeps from its own English value. The rule targets German values that keep English
 * content words of their source, the class the 2026-11-25 verification found ("Mission image ausstehend"). Words of
 * fewer than three characters, English function words, {@link germanWordsLikeEnglish} and the documented shared
 * words are not carry-over.
 *
 * @param german The German value.
 * @param english The English value of the same key.
 * @param sharedWords The documented shared words by lower-case word.
 * @param used A set that receives every shared word that suppressed a hit, so unused entries can be reported.
 * @returns The distinct carried-over words, sorted.
 */
function carriedOverWords(german, english, sharedWords, used) {
  const englishWords = new Set(wordsOf(english));
  const found = new Set();
  for (const word of wordsOf(german)) {
    if (word.length < 3 || !englishWords.has(word)) continue;
    if (englishFunctionWords.includes(word) || germanWordsLikeEnglish.includes(word)) continue;
    if (Object.hasOwn(sharedWords, word)) {
      used.add(word);
      continue;
    }
    found.add(word);
  }
  return [...found].sort();
}

/** Collects every `.ts` and `.tsx` file below a directory. */
async function listSourceFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(
    entries.map(async (entry) => {
      const path = resolve(directory, entry.name);
      if (entry.isDirectory()) return entry.name === "node_modules" ? [] : listSourceFiles(path);
      return /\.(ts|tsx)$/.test(entry.name) ? [path] : [];
    }),
  );
  return nested.flat();
}

const escapePattern = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const keyLike = /^[A-Za-z][A-Za-z0-9]*_/;
/** An identifier-shaped string literal; the quotes are looked at, not consumed, so nesting cannot misalign them. */
const quotedIdentifier = /(?<=["'`])[A-Za-z][A-Za-z0-9_]*(?=["'`])/g;
/** A run of key characters with at least one substitution: Server_Action_${id}_Label. */
const templateKey = /[A-Za-z0-9_]*(?:\$\{[^}]*\}[A-Za-z0-9_]*)+/g;
/** A quoted key prefix followed by a plus: "Server_Activity_Audit_" + type. */
const concatenatedPrefix = /["'`]([A-Za-z][A-Za-z0-9]*_[A-Za-z0-9_]*_)["'`]\s*\+/g;

/**
 * Reads the string literals of the source files. Returns the identifier-shaped literal values, plus a pattern for
 * every key built at run time from a template literal or a concatenation.
 */
async function collectReferences(sourceRoot) {
  const literals = new Set();
  const patterns = [];
  for (const file of await listSourceFiles(sourceRoot)) {
    const text = await readFile(file, "utf8");
    for (const match of text.matchAll(quotedIdentifier)) literals.add(match[0]);
    for (const match of text.matchAll(templateKey)) {
      const parts = match[0].split(/\$\{[^}]*\}/);
      if (parts.some((part) => keyLike.test(part))) {
        patterns.push(new RegExp(`^${parts.map(escapePattern).join("[A-Za-z0-9_]+")}$`));
      }
    }
    for (const match of text.matchAll(concatenatedPrefix)) {
      patterns.push(new RegExp(`^${escapePattern(match[1] ?? "")}[A-Za-z0-9_]+$`));
    }
  }
  return { literals, patterns };
}

const pluralSuffix = /_(zero|one|two|few|many|other)$/;

/** A key counts as referenced by its literal, by its plural base literal, or by a built-key pattern. */
function isReferenced(key, references) {
  const base = key.replace(pluralSuffix, "");
  if (references.literals.has(key) || references.literals.has(base)) return true;
  return references.patterns.some((pattern) => pattern.test(key) || pattern.test(base));
}

const options = readOptions(process.argv.slice(2));
const english = await readFlatObject(options.en);
const german = await readFlatObject(options.de);
const allowList = await readFlatObject(options.allow);
const englishAllowList = await readFlatObject(options.englishAllow);
const sharedWordList = await readFlatObject(options.sharedWords);
const usedSharedWords = new Set();

const failures = [];
const warnings = [];
const fail = (category, key, detail) =>
  failures.push(`[${category}] ${key}${detail ? ` - ${detail}` : ""}`);

for (const [label, source] of [
  ["en", english],
  ["de", german],
]) {
  for (const key of Object.keys(source)) {
    if (/[.:]/.test(key)) fail("bad-key", key, `${label} key contains a dot or a colon`);
  }
}

for (const key of Object.keys(english)) {
  if (!(key in german)) {
    fail("missing-german", key, "no German key");
    continue;
  }
  const value = german[key] ?? "";
  if (value.trim() === "") {
    fail("empty-german", key, "German value is empty");
    continue;
  }
  if (placeholders(english[key] ?? "") !== placeholders(value)) {
    fail("placeholder", key, "English and German placeholders differ");
  }
  // identical-allowed.json lists values that are deliberately equal to English, byte for byte or apart from letter
  // case. A byte-identical value is reported as [identical] only; [identical-case] covers the remaining case-only
  // matches, for example "Total Count" for "Total count".
  let identicalValue = false;
  if (!(key in allowList)) {
    if (value === english[key]) {
      identicalValue = true;
      fail("identical", key, "German equals English and is not allow-listed");
    } else if (comparable(value) === comparable(english[key] ?? "")) {
      identicalValue = true;
      fail("identical-case", key, "German equals English apart from letter case");
    }
  }
  // English words hiding inside a German value; a key in either allow list is exempt.
  if (!(key in allowList) && !(key in englishAllowList)) {
    const words = englishWordsIn(value);
    if (words !== "")
      fail("english-words", key, `German value contains English function words: ${words}`);
    if (!identicalValue) {
      const carried = carriedOverWords(value, english[key] ?? "", sharedWordList, usedSharedWords);
      if (carried.length > 0)
        fail("english-carryover", key, `German value repeats English words: ${carried.join(", ")}`);
    }
  }
}

for (const key of Object.keys(german)) {
  if (!(key in english)) fail("orphan-german", key, "no English key");
}

for (const [key, reason] of Object.entries(allowList)) {
  if (reason.trim() === "") fail("allow-reason", key, "allow-list entry has no reason");
  if (!(key in english)) {
    warnings.push(`[allow-stale] ${key} - allow-list key no longer exists`);
  } else if (comparable(german[key] ?? "") !== comparable(english[key] ?? "")) {
    warnings.push(`[allow-stale] ${key} - allow-list key no longer has identical values`);
  }
}

for (const [key, reason] of Object.entries(englishAllowList)) {
  if (reason.trim() === "")
    fail("allow-reason", key, "english-words allow-list entry has no reason");
  if (!(key in german)) {
    warnings.push(`[allow-stale] ${key} - english-words allow-list key no longer exists`);
  } else if (
    englishWordsIn(german[key] ?? "") === "" &&
    carriedOverWords(german[key] ?? "", english[key] ?? "", {}, new Set()).length === 0
  ) {
    warnings.push(
      `[allow-stale] ${key} - english-words allow-list key no longer triggers [english-words] or [english-carryover]`,
    );
  }
}

for (const [word, reason] of Object.entries(sharedWordList)) {
  if (reason.trim() === "")
    fail("allow-reason", word, "shared-word allow-list entry has no reason");
  if (!usedSharedWords.has(word)) {
    warnings.push(`[allow-stale] ${word} - shared word is no longer repeated by any German value`);
  }
}

if (options.unused) {
  const references = await collectReferences(options.src);
  for (const key of Object.keys(english)) {
    if (isReferenced(key, references)) continue;
    // Server_ and Hosting_ keys belong to the hosting UI, so an unreferenced one is a retired key that must be
    // deleted. Other families only warn because their owners are not gated yet.
    if (/^(Server|Hosting)_/.test(key)) fail("unused", key, "no reference in src");
    else warnings.push(`[unused-warn] ${key} - no reference in src`);
  }
}

for (const line of [...failures, ...warnings]) console.log(line);
console.log(
  `Checked ${Object.keys(english).length} English and ${Object.keys(german).length} German keys: ` +
    `${failures.length} failure(s), ${warnings.length} warning(s).`,
);
if (failures.length > 0) process.exitCode = 1;
