import { readFile, mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { format } from "prettier";

const projectRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const webAppRoot = resolve(projectRoot, "..", "WebApp", "Properties");
const outputRoot = resolve(projectRoot, "src", "generated", "locales");

const decodeXml = (value) =>
  value
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
    .replaceAll("&quot;", '"')
    .replaceAll("&apos;", "'")
    .replaceAll("&amp;", "&")
    .replace(/&#(\d+);/g, (_, code) => String.fromCodePoint(Number(code)))
    .replace(/&#x([\da-f]+);/gi, (_, code) => String.fromCodePoint(Number.parseInt(code, 16)));

function parseResx(source) {
  const values = {};
  const dataExpression = /<data\b([^>]*)>([\s\S]*?)<\/data>/g;
  for (const match of source.matchAll(dataExpression)) {
    const nameMatch = match[1]?.match(/\bname="([^"]+)"/);
    const valueMatch = match[2]?.match(/<value\b[^>]*>([\s\S]*?)<\/value>/);
    if (!nameMatch || !valueMatch) continue;
    values[decodeXml(nameMatch[1])] = decodeXml(valueMatch[1]).replaceAll("\r\n", "\n");
  }
  return Object.fromEntries(
    Object.entries(values).sort(([left], [right]) => left.localeCompare(right)),
  );
}

const english = parseResx(await readFile(resolve(webAppRoot, "Language.resx"), "utf8"));
const germanRaw = parseResx(await readFile(resolve(webAppRoot, "Language.de.resx"), "utf8"));
const german = Object.fromEntries(
  Object.keys(english).map((key) => [key, germanRaw[key] ?? english[key]]),
);
const keys = Object.keys(english);
const typeSource = `// Generated from WebApp RESX resources. Do not edit.\nexport const localeKeys = ${JSON.stringify(keys, null, 2)} as const;\nexport type LocaleKey = (typeof localeKeys)[number];\n`;
const formattedTypeSource = await format(typeSource, { parser: "typescript" });

await mkdir(outputRoot, { recursive: true });
await Promise.all([
  writeFile(resolve(outputRoot, "en.json"), `${JSON.stringify(english, null, 2)}\n`),
  writeFile(resolve(outputRoot, "de.json"), `${JSON.stringify(german, null, 2)}\n`),
  writeFile(resolve(outputRoot, "keys.ts"), formattedTypeSource),
]);

console.log(`Exported ${keys.length} localization keys for en/de.`);
