import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { join, resolve } from "node:path";

const projectRoot = resolve(import.meta.dirname, "..", "..");

function sourceFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name);
    if (statSync(path).isDirectory()) return sourceFiles(path);
    return /\.(ts|tsx)$/.test(name) ? [path] : [];
  });
}

function readProject(path: string): string {
  return readFileSync(join(projectRoot, path), "utf8");
}

describe("locale ownership", () => {
  it("never imports from the generated locale directory", () => {
    const offenders = [
      ...sourceFiles(join(projectRoot, "src")),
      ...sourceFiles(join(projectRoot, "tests")),
    ]
      .filter((path) => !path.endsWith("ownership.test.ts"))
      .filter((path) => /generated\/locales/.test(readFileSync(path, "utf8")));

    expect(offenders).toEqual([]);
  });

  it("has no RESX exporter and no generated locale files", () => {
    expect(existsSync(join(projectRoot, "scripts", "export-locales.mjs"))).toBe(false);
    expect(existsSync(join(projectRoot, "src", "generated", "locales"))).toBe(false);
  });

  it("has no hosting copy object: features/hosting/copy.ts is deleted and nothing imports it", () => {
    expect(existsSync(join(projectRoot, "src", "features", "hosting", "copy.ts"))).toBe(false);

    const offenders = [
      ...sourceFiles(join(projectRoot, "src")),
      ...sourceFiles(join(projectRoot, "tests")),
    ]
      .filter((path) => !path.endsWith("ownership.test.ts"))
      .filter((path) =>
        /features\/hosting\/copy["']|useHostingCopy|from ["']\.\/copy["']/.test(
          readFileSync(path, "utf8"),
        ),
      );

    expect(offenders).toEqual([]);
  });

  it("keeps the hosting-only state components out of the hosting layer", () => {
    const offenders = sourceFiles(join(projectRoot, "src")).filter((path) =>
      /HostingAccessBoundary|HostingLoading|HostingError/.test(readFileSync(path, "utf8")),
    );

    expect(offenders).toEqual([]);
  });

  it("feeds i18next from the React-owned JSON files", () => {
    const source = readProject("src/app/i18n.ts");

    expect(source).toContain("@/locales/en.json");
    expect(source).toContain("@/locales/de.json");
  });

  it("runs the locale gate in build, build:assets and ci and has no generator script", () => {
    const scripts = (JSON.parse(readProject("package.json")) as { scripts: Record<string, string> })
      .scripts;

    expect(scripts["locales:generate"]).toBeUndefined();
    expect(scripts["locales:check"]).toBe("node scripts/check-locales.mjs");
    for (const name of ["build", "build:assets", "ci"]) {
      expect(scripts[name], name).toContain("locales:check");
    }
  });

  it("runs the unused-key check in ci and in neither build script", () => {
    const scripts = (JSON.parse(readProject("package.json")) as { scripts: Record<string, string> })
      .scripts;

    expect(scripts["locales:unused"]).toBe("node scripts/check-locales.mjs --unused");
    expect(scripts["ci"]).toContain("locales:unused");
    expect(scripts["build"]).not.toContain("locales:unused");
    expect(scripts["build:assets"]).not.toContain("locales:unused");
  });
});
