import { readFileSync } from "node:fs";
import { dirname, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";
import { localReactReturnPath, localReturnUrl } from "@/auth/auth";
import { routeManifest } from "@/app/routes";
import {
  activityEntryAnchorPattern,
  configurationProfileTabIds,
  filesAreaIds,
  financeOwnerKindIds,
  financeStatusIds,
  isDeclaredQueryKey,
  serverActionIds,
  serverAutomationAnchor,
  serverSettingsPanelAnchors,
  slotSetupIds,
  upcomingEventsModeIds,
  urlParameters,
  type ParameterSpec,
} from "@/app/urlParameters";
import { lookupActionVocabulary } from "@/features/hosting/actionVocabulary";
import { filesLocationSearch, readFilesLocation } from "@/features/hosting/filesLocation";
import {
  fleetFilters,
  parseFleetSearchParams,
  writeFleetSearchParams,
} from "@/features/hosting/fleetModel";
import {
  activityEntryAnchor,
  sectionSearch,
  workspaceSectionFromSearch,
  workspaceTabIds,
} from "@/features/hosting/workspaceSections";

const appRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");

/**
 * The files that read or write the URL state of a route, and the route ids they serve.
 *
 * Maintained by hand: when a new file reads or writes a query parameter of the address bar it must be listed here
 * with its route, and the key must be declared in `urlParameters.ts`. A file that is in neither this table nor
 * {@link apiQueryBuilderFiles} fails the scan, so URL state cannot appear unnoticed.
 */
const urlStateFiles: Readonly<Record<string, readonly string[]>> = {
  "src/components/server/CostConfirmationProvider.tsx": ["server"],
  "src/features/admin/EventCreatePage.tsx": ["command-event-edit"],
  "src/features/admin/EventEditPage.tsx": ["command-event-edit"],
  "src/features/hosting/ConfigurationProfileWorkspacePage.tsx": ["configuration-profile"],
  "src/features/hosting/CostAlertsPage.tsx": ["server-cost-alerts"],
  "src/features/hosting/HostingFinancePage.tsx": ["command-hosting-finance"],
  "src/features/hosting/ServerWorkspacePage.tsx": ["server"],
  "src/features/hosting/filesLocation.ts": ["server"],
  "src/features/hosting/fleetModel.ts": ["servers"],
  "src/features/hosting/workspaceSections.ts": ["server"],
  "src/pages/AuthenticationPage.tsx": ["authentication-action"],
  "src/pages/UpcomingEventsPage.tsx": ["events-upcoming"],
};

/**
 * The files whose `URLSearchParams` calls build API or WebSocket request URLs. Those query strings are not URL
 * state of the app and are excluded from the contract (F9).
 */
const apiQueryBuilderFiles: ReadonlySet<string> = new Set([
  "src/app/updateStream.ts",
  "src/auth/auth.tsx",
  "src/features/admin/authority.ts",
  "src/features/admin/eventAuthoring.ts",
  "src/features/admin/fieldReports.ts",
  "src/features/admin/members.ts",
  "src/features/admin/roles.ts",
  "src/features/admin/siteImages.ts",
  "src/features/admin/terrains.ts",
  "src/features/admin/vereinKassenbuch.ts",
  "src/features/admin/vereinMembershipRequests.ts",
  "src/features/events-member/services.ts",
  "src/features/hosting/financeService.ts",
  "src/features/hosting/services.ts",
]);

/**
 * The files that carry `location.search` through to a navigation or a return URL without reading a key.
 * Keys in the carried query string belong to the route that owns them.
 */
const passThroughFiles: ReadonlySet<string> = new Set([
  "src/app/routes.ts",
  "src/auth/auth.tsx",
  "src/components/AppToasts.tsx",
]);

/**
 * Functions whose key argument is a parameter. The scan reads their call sites instead of the dynamic key, which
 * must be a string literal there.
 */
const keyedHelperFunctions: Readonly<Record<string, readonly string[]>> = {
  "src/features/hosting/HostingFinancePage.tsx": ["setFilter"],
};

type SiteKind = "read" | "write";

interface UrlSite {
  file: string;
  line: number;
  kind: SiteKind;
  /** The literal key, or null when the key could not be resolved to a literal. */
  key: string | null;
  /** True for a dynamic key inside a function listed in {@link keyedHelperFunctions}. */
  delegated: boolean;
  /** The syntax that produced the site, for failure messages. */
  via: string;
}

interface ScanResult {
  sites: UrlSite[];
  /** Files with a `location.search` read that is neither URL state nor a declared pass-through. */
  searchReaders: string[];
}

const readMethods = new Set(["get", "getAll", "has"]);
const writeMethods = new Set(["set", "append", "delete"]);
const queryInLiteral = /[?&]([A-Za-z][A-Za-z0-9]*)=/g;

function normalize(file: string): string {
  return relative(appRoot, file).replace(/\\/g, "/");
}

function isUrlSearchParams(type: ts.Type): boolean {
  return type.symbol?.name === "URLSearchParams";
}

function enclosingFunctionName(node: ts.Node): string | null {
  for (let current: ts.Node | undefined = node.parent; current; current = current.parent) {
    if (ts.isFunctionDeclaration(current) && current.name) return current.name.text;
    if (
      (ts.isArrowFunction(current) || ts.isFunctionExpression(current)) &&
      ts.isVariableDeclaration(current.parent) &&
      ts.isIdentifier(current.parent.name)
    )
      return current.parent.name.text;
  }
  return null;
}

/**
 * Finds every reader and writer of URL query state in the given source files of a program.
 *
 * Recognized: `get`, `getAll`, `has`, `set`, `append` and `delete` on a value typed `URLSearchParams`; calls of the
 * `setSearchParams` function of `useSearchParams` with an object literal; the keys of `?key=` and `&key=` in the
 * string and template literals of the files listed in {@link urlStateFiles}; and `location.search` reads.
 */
function scanProgram(program: ts.Program, files: readonly string[]): ScanResult {
  const checker = program.getTypeChecker();
  const sites: UrlSite[] = [];
  const searchReaders: string[] = [];

  for (const file of files) {
    const source = program.getSourceFile(file);
    if (!source) throw new Error(`The scan could not load ${file}.`);
    const name = normalize(file);
    const helpers = keyedHelperFunctions[name] ?? [];
    const lineOf = (node: ts.Node) =>
      source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1;

    const addLiteralQueryKeys = (node: ts.Node, text: string) => {
      if (!(name in urlStateFiles)) return;
      for (const match of text.matchAll(queryInLiteral))
        sites.push({
          file: name,
          line: lineOf(node),
          kind: "write",
          key: match[1]!,
          delegated: false,
          via: "literal",
        });
    };

    const visit = (node: ts.Node) => {
      if (ts.isCallExpression(node)) {
        const callee = node.expression;
        if (ts.isPropertyAccessExpression(callee)) {
          const method = callee.name.text;
          const isRead = readMethods.has(method);
          if (
            (isRead || writeMethods.has(method)) &&
            isUrlSearchParams(checker.getTypeAtLocation(callee.expression))
          ) {
            const argument = node.arguments[0];
            const argumentType = argument ? checker.getTypeAtLocation(argument) : undefined;
            const key = argumentType?.isStringLiteral() ? argumentType.value : null;
            const functionName = enclosingFunctionName(node);
            sites.push({
              file: name,
              line: lineOf(node),
              kind: isRead ? "read" : "write",
              key,
              delegated: key === null && functionName !== null && helpers.includes(functionName),
              via: `.${method}()`,
            });
          }
        }
        if (ts.isIdentifier(callee) && helpers.includes(callee.text)) {
          const argument = node.arguments[0];
          const argumentType = argument ? checker.getTypeAtLocation(argument) : undefined;
          sites.push({
            file: name,
            line: lineOf(node),
            kind: "write",
            key: argumentType?.isStringLiteral() ? argumentType.value : null,
            delegated: false,
            via: `${callee.text}()`,
          });
        }
        if (checker.getTypeAtLocation(callee).aliasSymbol?.name === "SetURLSearchParams") {
          const argument = node.arguments[0];
          if (argument && ts.isObjectLiteralExpression(argument)) {
            for (const property of argument.properties) {
              const key =
                ts.isShorthandPropertyAssignment(property) || ts.isPropertyAssignment(property)
                  ? ts.isIdentifier(property.name) || ts.isStringLiteral(property.name)
                    ? property.name.text
                    : null
                  : null;
              sites.push({
                file: name,
                line: lineOf(property),
                kind: "write",
                key,
                delegated: false,
                via: "setSearchParams({})",
              });
            }
          }
        }
      }
      if (ts.isPropertyAccessExpression(node) && node.name.text === "search") {
        const receiver = node.expression.getText(source);
        if (
          (receiver === "window.location" || receiver === "location") &&
          !(name in urlStateFiles) &&
          !passThroughFiles.has(name)
        )
          searchReaders.push(`${name}:${lineOf(node)}`);
      }
      if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node))
        addLiteralQueryKeys(node, node.text);
      else if (ts.isTemplateHead(node) || ts.isTemplateMiddle(node) || ts.isTemplateTail(node))
        addLiteralQueryKeys(node, node.text);
      ts.forEachChild(node, visit);
    };
    visit(source);
  }
  return { sites, searchReaders };
}

function loadCompilerOptions(): ts.CompilerOptions {
  const configPath = resolve(appRoot, "tsconfig.app.json");
  const config = ts.readConfigFile(configPath, ts.sys.readFile);
  return ts.parseJsonConfigFileContent(config.config, ts.sys, appRoot).options;
}

function listCandidateFiles(): string[] {
  const configPath = resolve(appRoot, "tsconfig.app.json");
  const config = ts.readConfigFile(configPath, ts.sys.readFile);
  const parsed = ts.parseJsonConfigFileContent(config.config, ts.sys, appRoot);
  const sourceRoot = resolve(appRoot, "src");
  return parsed.fileNames.filter((file) => {
    if (!/\.tsx?$/.test(file) || !resolve(file).startsWith(sourceRoot)) return false;
    if (normalize(file).startsWith("src/generated/")) return false;
    return (
      normalize(file) in urlStateFiles ||
      /[sS]earchParams|location\.search/.test(readFileSync(file, "utf8"))
    );
  });
}

/** Scans in-memory sources typed against the real lib, so a sample can be checked like a real file. */
function scanVirtualSources(samples: Readonly<Record<string, string>>): ScanResult {
  const options = loadCompilerOptions();
  const host = ts.createCompilerHost(options);
  const original = {
    getSourceFile: host.getSourceFile.bind(host),
    fileExists: host.fileExists.bind(host),
    readFile: host.readFile.bind(host),
  };
  const virtualName = (file: string) => normalize(file);
  host.fileExists = (file) => virtualName(file) in samples || original.fileExists(file);
  host.readFile = (file) => samples[virtualName(file)] ?? original.readFile(file);
  host.getSourceFile = (file, languageVersion, onError, shouldCreate) => {
    const text = samples[virtualName(file)];
    return text === undefined
      ? original.getSourceFile(file, languageVersion, onError, shouldCreate)
      : ts.createSourceFile(file, text, languageVersion, true);
  };
  const files = Object.keys(samples).map((file) => resolve(appRoot, file));
  const program = ts.createProgram(files, { ...options, types: [] }, host);
  return scanProgram(program, files);
}

/**
 * Compares scanned sites with the contract.
 *
 * @returns One message per undeclared key, unresolved key and unclassified file.
 */
function findViolations(
  sites: readonly UrlSite[],
  searchReaders: readonly string[],
  stateFiles: Readonly<Record<string, readonly string[]>>,
  builderFiles: ReadonlySet<string>,
): string[] {
  const violations: string[] = [];
  for (const site of sites) {
    if (builderFiles.has(site.file)) continue;
    const routeIds = stateFiles[site.file];
    if (!routeIds) {
      violations.push(
        `${site.file}:${site.line} ${site.via} is in no file table (URL state or API builder)`,
      );
      continue;
    }
    if (site.delegated) continue;
    if (site.key === null) {
      violations.push(
        `${site.file}:${site.line} ${site.via} has a key that is not a string literal`,
      );
      continue;
    }
    const key = site.key;
    if (!routeIds.some((routeId) => isDeclaredQueryKey(routeId, key)))
      violations.push(
        `${site.file}:${site.line} ${site.via} uses undeclared key "${key}" for ${routeIds.join(", ")}`,
      );
  }
  for (const reader of searchReaders)
    violations.push(`${reader} reads location.search outside the file tables`);
  return violations;
}

function declaredValues(spec: ParameterSpec | undefined): readonly string[] {
  return Array.isArray(spec) ? spec : [];
}

/** Collects the string literal texts of one source file. */
function literalsOf(file: string): Set<string> {
  const source = ts.createSourceFile(
    file,
    readFileSync(resolve(appRoot, file), "utf8"),
    ts.ScriptTarget.Latest,
    true,
  );
  const literals = new Set<string>();
  const visit = (node: ts.Node) => {
    if (
      ts.isStringLiteral(node) ||
      ts.isNoSubstitutionTemplateLiteral(node) ||
      ts.isTemplateHead(node) ||
      ts.isTemplateMiddle(node) ||
      ts.isTemplateTail(node)
    )
      literals.add(node.text);
    ts.forEachChild(node, visit);
  };
  visit(source);
  return literals;
}

describe("URL parameter certification", () => {
  let scan: ScanResult;

  beforeAll(() => {
    const files = listCandidateFiles();
    const program = ts.createProgram(files, loadCompilerOptions());
    scan = scanProgram(program, files);
  }, 120_000);

  it("finds a non-vacuous set of reader and writer sites", () => {
    const stateSites = scan.sites.filter((site) => !apiQueryBuilderFiles.has(site.file));
    expect(stateSites.filter((site) => site.kind === "read").length).toBeGreaterThanOrEqual(10);
    expect(stateSites.length).toBeGreaterThanOrEqual(20);
    for (const file of Object.keys(urlStateFiles))
      expect(
        scan.sites.some((site) => site.file === file),
        `${file} is in the URL state table but the scan found no site in it`,
      ).toBe(true);
    for (const file of apiQueryBuilderFiles)
      expect(
        scan.sites.some((site) => site.file === file),
        `${file} is in the API builder table but has no URLSearchParams call`,
      ).toBe(true);
  });

  it("declares every key that a reader or writer uses for the route of its file", () => {
    expect(
      findViolations(scan.sites, scan.searchReaders, urlStateFiles, apiQueryBuilderFiles),
    ).toEqual([]);
  });

  it("maps files only to routes of the manifest and contract entries only to manifest routes", () => {
    const routeIds = new Set(routeManifest.routes.map((route) => route.id));
    for (const [file, ids] of Object.entries(urlStateFiles))
      for (const id of ids)
        expect(routeIds.has(id), `${file} maps to unknown route ${id}`).toBe(true);
    for (const id of Object.keys(urlParameters))
      expect(routeIds.has(id), `urlParameters declares unknown route ${id}`).toBe(true);
  });

  it("reports an undeclared key, a dynamic key and an unclassified file in an inline sample", () => {
    const sample = scanVirtualSources({
      "src/virtual/declared.ts": [
        "declare const params: URLSearchParams;",
        'export const tab = params.get("tab");',
        'export const bogus = params.get("bogus");',
        "export function dynamic(name: string) { return params.get(name); }",
      ].join("\n"),
      "src/virtual/unlisted.ts": [
        "declare const params: URLSearchParams;",
        'export const owner = params.has("owner");',
      ].join("\n"),
    });
    const violations = findViolations(
      sample.sites,
      sample.searchReaders,
      { "src/virtual/declared.ts": ["server"] },
      new Set(),
    );
    expect(violations).toHaveLength(3);
    expect(violations.join("\n")).toContain('undeclared key "bogus"');
    expect(violations.join("\n")).toContain("not a string literal");
    expect(violations.join("\n")).toContain("src/virtual/unlisted.ts");
    expect(violations.join("\n")).not.toContain('"tab"');
  });

  it("fails a declared key that no code reads", () => {
    const readKeys = (routeId: string) =>
      new Set(
        scan.sites
          .filter(
            (site) =>
              site.kind === "read" &&
              site.key !== null &&
              urlStateFiles[site.file]?.includes(routeId),
          )
          .map((site) => site.key),
      );
    const stale = Object.entries(urlParameters).flatMap(([routeId, contract]) => {
      const keys = readKeys(routeId);
      return Object.keys(contract.query)
        .filter((key) => !keys.has(key))
        .map((key) => `${routeId}:${key}`);
    });
    expect(stale, "declared keys that no code reads").toEqual([]);
  });

  describe("enum values", () => {
    it("reaches every workspace tab from its writer and its reader", () => {
      expect(urlParameters.server!.query.tab).toBe(workspaceTabIds);
      for (const id of workspaceTabIds) {
        expect(workspaceSectionFromSearch(id)).toBe(id);
        expect(sectionSearch(id)).toBe(id === "overview" ? "" : `?tab=${id}`);
      }
    });

    it("keeps the action ids inside the action vocabulary", () => {
      for (const id of serverActionIds)
        expect(lookupActionVocabulary(id), `no vocabulary entry for ${id}`).toBeDefined();
      expect(urlParameters.server!.query.action).toBe(serverActionIds);
    });

    it("reaches every fleet filter from its writer and its reader", () => {
      expect(urlParameters.servers!.query.filter).toBe(fleetFilters);
      for (const filter of fleetFilters) {
        const written = writeFleetSearchParams(new URLSearchParams(), { filter });
        expect(parseFleetSearchParams(written).filter).toBe(filter);
      }
    });

    it("reaches both files areas from the files location helpers", () => {
      for (const area of filesAreaIds) {
        const context = area === "live" ? "live" : "persistent";
        const search = filesLocationSearch(new URLSearchParams(), { path: "", context });
        expect(readFilesLocation(new URLSearchParams(search)).location.context).toBe(context);
      }
      expect(declaredValues(urlParameters.server!.query.area)).toEqual(filesAreaIds);
    });

    it.each([
      ["src/features/hosting/ConfigurationProfileWorkspacePage.tsx", configurationProfileTabIds],
      [
        "src/features/hosting/HostingFinancePage.tsx",
        [...financeOwnerKindIds, ...financeStatusIds],
      ],
      ["src/pages/UpcomingEventsPage.tsx", upcomingEventsModeIds],
      ["src/features/admin/EventEditPage.tsx", slotSetupIds],
      ["src/features/admin/EventCreatePage.tsx", slotSetupIds.map((id) => `?slotSetup=${id}`)],
    ] as const)("finds every declared value as a literal in %s", (file, values) => {
      const literals = literalsOf(file);
      for (const value of values) {
        const found = [...literals].some((literal) => literal.includes(value));
        expect(found, `${value} is declared but never appears in ${file}`).toBe(true);
      }
    });

    it("declares enum values only for keys that exist", () => {
      expect(declaredValues(urlParameters["configuration-profile"]!.query.tab)).toEqual(
        configurationProfileTabIds,
      );
      expect(declaredValues(urlParameters["command-hosting-finance"]!.query.kind)).toEqual(
        financeOwnerKindIds,
      );
      expect(declaredValues(urlParameters["command-hosting-finance"]!.query.status)).toEqual(
        financeStatusIds,
      );
    });
  });

  describe("hash anchors", () => {
    it("declares every Settings panel anchor and the activity anchor pattern for the server route", () => {
      const hash = urlParameters.server!.hash;
      expect(hash).toContain(serverAutomationAnchor);
      for (const anchor of serverSettingsPanelAnchors) expect(hash).toContain(anchor);
      expect(hash).toContain(activityEntryAnchorPattern);
      for (const type of ["job", "log", "audit"] as const)
        expect(activityEntryAnchor(type, "7f1c")).toMatch(activityEntryAnchorPattern);
    });

    it("writes the automation anchor in the code", () => {
      expect([...literalsOf("src/features/hosting/AutomationPanel.tsx")]).toContain(
        serverAutomationAnchor,
      );
      expect(
        [...literalsOf("src/components/server/AutoStopNotice.tsx")].some((literal) =>
          literal.includes(`#${serverAutomationAnchor}`),
        ),
      ).toBe(true);
    });

    it("declares each Settings panel anchor as the id of its panel in the code", () => {
      const panelFiles: Record<string, string> = {
        "server-automation": "src/features/hosting/AutomationPanel.tsx",
        "server-mods": "src/components/server/ModPackPanel.tsx",
        "server-configuration": "src/features/hosting/ServerConfigurationPanel.tsx",
        "server-credentials": "src/features/hosting/ServerSecretsPanel.tsx",
        "server-access": "src/features/hosting/ServerAccessPanel.tsx",
        "server-cost-owner": "src/features/hosting/ServerCostOwnerPanel.tsx",
        "server-record": "src/features/hosting/sections/SettingsSection.tsx",
      };
      expect(Object.keys(panelFiles).sort()).toEqual([...serverSettingsPanelAnchors].sort());
      for (const [anchor, file] of Object.entries(panelFiles))
        expect([...literalsOf(file)], `${anchor} is not a literal in ${file}`).toContain(anchor);
    });

    it("declares no hash anchor for routes that never write one", () => {
      const withHash = Object.entries(urlParameters)
        .filter(([, contract]) => contract.hash.length > 0)
        .map(([routeId]) => routeId);
      expect(withHash).toEqual(["server"]);
    });
  });

  describe("returnUrl", () => {
    it("is declared as a local React path", () => {
      expect(urlParameters["authentication-action"]!.query.returnUrl).toBe("local-path");
    });

    it("keeps a local React path and rejects absolute and protocol-relative URLs", () => {
      const base = routeManifest.reactBasePath;
      expect(localReactReturnPath(`${base}/command`, `${base}/`)).toBe(`${base}/command`);
      expect(localReactReturnPath("/command", `${base}/`).startsWith("/")).toBe(true);
      expect(localReactReturnPath("https://evil.example", `${base}/`)).toBe(`${base}/`);
      expect(localReactReturnPath("//evil.example", `${base}/`)).toBe(`${base}/`);
      expect(localReactReturnPath("javascript:alert(1)", `${base}/`)).toBe(`${base}/`);

      for (const hostile of ["https://evil.example", "//evil.example", "https:\\\\evil.example"]) {
        const local = localReturnUrl(undefined, hostile);
        expect(local.startsWith("/")).toBe(true);
        expect(local.startsWith("//")).toBe(false);
        expect(local).not.toContain("evil.example");
        const absolute = localReturnUrl(new URL("https://api.example/users/login/steam"), hostile);
        expect(new URL(absolute).origin).toBe(window.location.origin);
      }
      expect(localReturnUrl(undefined, "/command").startsWith("/")).toBe(true);
    });
  });
});
