import { createHash } from "node:crypto";
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { join, resolve } from "node:path";

const projectRoot = resolve(import.meta.dirname, "..", "..");
const fontDirectory = join(projectRoot, "tests", "e2e", "fonts");

interface ManifestFont {
  file: string;
  family: string;
  weight: number;
  sha256: string;
  sourceUrl: string;
  license: string;
}

interface ManifestLicense {
  file: string;
  family: string;
  license: string;
  sha256: string;
  sourceUrl: string;
}

interface Manifest {
  fonts: ManifestFont[];
  licenses: ManifestLicense[];
}

function sha256(path: string): string {
  return createHash("sha256").update(readFileSync(path)).digest("hex");
}

function readManifest(): Manifest {
  return JSON.parse(readFileSync(join(fontDirectory, "MANIFEST.json"), "utf8")) as Manifest;
}

function sourceFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name);
    if (statSync(path).isDirectory()) return sourceFiles(path);
    return /\.(ts|tsx)$/.test(name) ? [path] : [];
  });
}

describe("vendored test fonts", () => {
  it("lists every woff2 file in the manifest and no file that is missing", () => {
    const manifest = readManifest();
    const onDisk = readdirSync(fontDirectory)
      .filter((name) => name.endsWith(".woff2"))
      .sort();
    const listed = manifest.fonts.map((font) => font.file).sort();

    expect(listed).toEqual(onDisk);
    expect(new Set(listed).size).toBe(listed.length);
  });

  it("keeps only the manifest, the listed fonts and the listed licenses in the directory", () => {
    const manifest = readManifest();
    const expected = [
      ".gitattributes",
      "MANIFEST.json",
      ...manifest.fonts.map((font) => font.file),
      ...manifest.licenses.map((license) => license.file),
    ].sort();

    expect(readdirSync(fontDirectory).sort()).toEqual(expected);
  });

  it("matches the SHA-256 recorded for every font file", () => {
    for (const font of readManifest().fonts) {
      expect(sha256(join(fontDirectory, font.file)), font.file).toBe(font.sha256);
    }
  });

  it("matches the SHA-256 recorded for every license file", () => {
    for (const license of readManifest().licenses) {
      expect(sha256(join(fontDirectory, license.file)), license.file).toBe(license.sha256);
    }
  });

  it("names a license for every entry and ships a non-empty license file for every family", () => {
    const manifest = readManifest();
    const families = new Set(manifest.fonts.map((font) => font.family));

    for (const font of manifest.fonts) {
      expect(font.license.trim(), font.file).not.toBe("");
      expect(font.sourceUrl, font.file).toMatch(/^https:\/\//);
    }
    for (const family of families) {
      const license = manifest.licenses.find((entry) => entry.family === family);
      expect(license, `license of ${family}`).toBeDefined();
      const path = join(fontDirectory, license?.file ?? "missing");
      expect(existsSync(path), `license file of ${family}`).toBe(true);
      expect(statSync(path).size, `license file of ${family}`).toBeGreaterThan(0);
      expect(license?.license.trim(), `license name of ${family}`).not.toBe("");
    }
  });

  it("is never imported by production source (bundle guard)", () => {
    const importOfTests = /(?:from\s+|import\s*\(\s*|import\s+|require\(\s*)["'][^"']*tests\//;
    const offenders = sourceFiles(join(projectRoot, "src")).filter((path) =>
      importOfTests.test(readFileSync(path, "utf8")),
    );

    expect(offenders).toEqual([]);
  });
});
