import { readdirSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
  countRoutePatterns,
  currentClassicUrl,
  currentReactUrl,
  matchRoute,
  routeManifest,
  routePatternToRegExp,
} from "@/app/routes";
import { shouldFallBackToClassic } from "@/app/RouteCertificationGate";
import { implementedReactRoutePatterns } from "@/app/App";

function collectBlazorRoutes(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) return collectBlazorRoutes(path);
    if (!entry.name.endsWith(".razor")) return [];
    return [...readFileSync(path, "utf8").matchAll(/^@page\s+"([^"]+)"/gm)].map(
      (match) => match[1]!,
    );
  });
}

describe("route manifest", () => {
  it("describes every existing Blazor route and marks React-only fallbacks", () => {
    expect(countRoutePatterns()).toBe(81);
    const manifestRoutes = routeManifest.routes
      .filter((route) => !route.classicFallback)
      .flatMap((route) => [route.canonical, ...route.aliases]);
    const blazorRoutes = collectBlazorRoutes(resolve(process.cwd(), "..", "WebApp", "Pages"));
    expect(new Set(manifestRoutes)).toEqual(new Set(blazorRoutes));
    expect(manifestRoutes).toHaveLength(new Set(manifestRoutes).size);
    expect(matchRoute("/command/hosting-finance/settings")?.classicFallback).toBe(
      "/command/hosting-finance",
    );
    expect(
      matchRoute("/command/hosting-finance/accounts/83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f")
        ?.classicFallback,
    ).toBe("/command/hosting-finance");
  });

  it("matches constrained identifiers and aliases", () => {
    expect(matchRoute("/command/events/01a0102f-3f75-7c4a-bd99-ed6273e83565")?.certified).toBe(
      true,
    );
    expect(matchRoute("/command/servers/83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f")?.id).toBe("server");
    expect(matchRoute("/media/after-action-report")?.id).toBe("field-report");
    expect(matchRoute("/wiki/83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f")?.certified).toBe(true);
    expect(matchRoute("/events/83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f")?.certified).toBe(true);
    expect(matchRoute("/events/all")?.id).toBe("command-events");
    expect(matchRoute("/events/all")?.certified).toBe(true);
    expect(matchRoute("/events/create")?.id).toBe("command-event-create");
    expect(matchRoute("/events/create")?.certified).toBe(true);
    expect(matchRoute("/command/events/83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f")?.certified).toBe(
      true,
    );
    expect(matchRoute("/command/claims")?.certified).toBe(true);
    expect(matchRoute("/command/members")?.certified).toBe(true);
    expect(matchRoute("/command/members/83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f")?.certified).toBe(
      true,
    );
    expect(matchRoute("/command/content/gallery")?.certified).toBe(true);
    expect(matchRoute("/command/content/images")?.certified).toBe(true);
    expect(matchRoute("/command/content/images/gdpr")?.certified).toBe(true);
    expect(matchRoute("/command/field-reports")?.certified).toBe(true);
    expect(matchRoute("/command/wiki-media")?.id).toBe("command-field-reports");
    expect(
      matchRoute("/command/field-reports/83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f")?.certified,
    ).toBe(true);
    expect(matchRoute("/command/content/pages/gdpr/edit")?.certified).toBe(true);
    expect(matchRoute("/command/verein/settings")?.certified).toBe(true);
    expect(matchRoute("/command/settings/verein")?.id).toBe("command-verein-settings");
    expect(matchRoute("/command/verein/kassenbuch")?.certified).toBe(true);
    expect(matchRoute("/command/verein/kassenbuch/create")?.certified).toBe(true);
    expect(matchRoute("/command/verein/kassenbuch/9223372036854775807/edit")?.certified).toBe(true);
    expect(matchRoute("/command/mod-packs")?.certified).toBe(true);
    expect(matchRoute("/data/mod-packs/all")?.id).toBe("mod-packs");
    expect(matchRoute("/data/mod-packs/create")?.id).toBe("mod-pack-create");
    expect(matchRoute("/data/mod-packs/42")?.id).toBe("mod-pack");
    expect(matchRoute("/command/mod-packs/42/downloads")?.certified).toBe(true);
    expect(matchRoute("/command/terrains")?.certified).toBe(true);
    expect(matchRoute("/data/terrains/all")?.id).toBe("terrains");
    expect(matchRoute("/data/terrains/create")?.id).toBe("terrain-create");
    expect(matchRoute("/data/terrains/9223372036854775807")?.id).toBe("terrain");
    expect(routePatternToRegExp("/command/roles/{RoleId:long}").test("/command/roles/nope")).toBe(
      false,
    );
    expect(
      routePatternToRegExp("/verein/beschluesse/{DocumentId:long}").test(
        "/verein/beschluesse/9223372036854775807",
      ),
    ).toBe(true);
  });

  it("certifies only completed command-center routes", () => {
    expect(matchRoute("/command/servers")?.certified).toBe(true);
    expect(matchRoute("/command/hosting-finance")?.certified).toBe(true);
    expect(matchRoute("/command/configuration-profiles")?.certified).toBe(true);
    expect(matchRoute("/command/configuration-profiles/create")?.certified).toBe(true);
    expect(
      matchRoute("/command/configuration-profiles/83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f")?.certified,
    ).toBe(true);
    expect(matchRoute("/command/servers/create")?.certified).toBe(true);
    expect(matchRoute("/command/servers/cost-alerts")?.id).toBe("server-cost-alerts");
    expect(matchRoute("/command/servers/cost-alerts")?.certified).toBe(true);
    expect(matchRoute("/command/servers/cost-alerts")?.classicFallback).toBe("/command/servers");
    expect(matchRoute("/command/servers/83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f")?.certified).toBe(
      true,
    );
    expect(matchRoute("/field-reports")?.certified).toBe(true);
  });

  it("implements every certified manifest pattern without treating implementation as certification", () => {
    const certifiedPatterns = routeManifest.routes
      .filter((route) => route.certified)
      .flatMap((route) => [route.canonical, ...route.aliases]);
    const implementedPatterns = new Set(implementedReactRoutePatterns);
    for (const pattern of certifiedPatterns) expect(implementedPatterns).toContain(pattern);
    expect(implementedReactRoutePatterns).toHaveLength(new Set(implementedReactRoutePatterns).size);
  });

  it("falls back only for known routes whose certification has been reopened", () => {
    expect(shouldFallBackToClassic("/gdpr")).toBe(true);
    expect(shouldFallBackToClassic("/field-reports/after-action")).toBe(true);
    expect(shouldFallBackToClassic("/join")).toBe(false);
    expect(shouldFallBackToClassic("/not-a-real-route")).toBe(false);
  });

  it("preserves the path, query, and hash while switching variants", () => {
    window.history.replaceState({}, "", "/next/command/servers?status=active#jobs");
    expect(currentClassicUrl()).toBe("/command/servers?status=active#jobs");

    window.history.replaceState({}, "", "/field-reports?category=training#latest");
    expect(currentReactUrl()).toBe("/next/field-reports?category=training#latest");

    window.history.replaceState({}, "", "/next-generation?view=all#latest");
    expect(currentReactUrl()).toBe("/next/next-generation?view=all#latest");
  });
});
