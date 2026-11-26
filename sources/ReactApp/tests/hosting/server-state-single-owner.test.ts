import { readdirSync, readFileSync } from "node:fs";
import { join, relative, resolve, sep } from "node:path";

const sourceRoot = resolve(import.meta.dirname, "..", "..", "src");

/** Files that may read a raw deployment status: the owner of the state model and the service normaliser. */
const allowedFiles = new Set(["features/hosting/serverState.ts", "features/hosting/services.ts"]);

/** Directories that hold generated code and are never scanned. */
const skippedDirectories = new Set(["generated"]);

/** A property access to a deployment status, or to a notification status label. */
const forbiddenAccess = /\bdeployment\s*\??\.\s*status\b|\.statusLabel\b/;

/** One forbidden access found by the scanner. */
interface Finding {
  file: string;
  line: number;
  text: string;
}

function collectSourceFiles(directory: string): string[] {
  const files: string[] = [];
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      if (!skippedDirectories.has(entry.name)) files.push(...collectSourceFiles(path));
    } else if (/\.tsx?$/.test(entry.name)) {
      files.push(path);
    }
  }
  return files;
}

/** Finds every line of a source text that accesses a raw deployment status or a notification status label. */
function scan(file: string, source: string): Finding[] {
  return source
    .split(/\r?\n/)
    .flatMap((text, index) =>
      forbiddenAccess.test(text) ? [{ file, line: index + 1, text: text.trim() }] : [],
    );
}

describe("single owner of the server status decision", () => {
  it("finds no raw deployment status or statusLabel access outside serverState.ts and services.ts", () => {
    const findings = collectSourceFiles(sourceRoot).flatMap((path) => {
      const file = relative(sourceRoot, path).split(sep).join("/");
      return allowedFiles.has(file) ? [] : scan(file, readFileSync(path, "utf8"));
    });

    expect(findings.map((finding) => `${finding.file}:${finding.line}: ${finding.text}`)).toEqual(
      [],
    );
  });

  it("flags a bad source so the scanner is known to be live", () => {
    const badSource = [
      "const label = server.deployment.status;",
      "const optional = server.deployment?.status;",
      "const raw = notification.statusLabel;",
      "const fine = derived.state;",
    ].join("\n");

    expect(scan("bad.tsx", badSource).map((finding) => finding.line)).toEqual([1, 2, 3]);
  });

  it("scans the allow-listed owner files as part of the source tree", () => {
    const files = collectSourceFiles(sourceRoot).map((path) =>
      relative(sourceRoot, path).split(sep).join("/"),
    );

    for (const allowed of allowedFiles) expect(files).toContain(allowed);
  });
});
