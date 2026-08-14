import manifestJson from "../../../Common/Ui/routes.json";

export type AppShell = "community" | "command";
export interface RouteManifestEntry {
  id: string;
  canonical: string;
  aliases: string[];
  certified: boolean;
  shell: AppShell;
}

export interface RouteManifest {
  version: number;
  reactBasePath: string;
  routes: RouteManifestEntry[];
}

export const routeManifest = manifestJson as RouteManifest;

function segmentPattern(segment: string): string {
  const parameter = /^\{([^}:]+)(?::([^}]+))?\}$/.exec(segment);
  if (!parameter) return segment.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return parameter[2] === "guid"
    ? "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
    : parameter[2] === "long"
      ? "-?[0-9]+"
      : "[^/]+";
}

export function routePatternToRegExp(pattern: string): RegExp {
  if (pattern === "/") return /^\/$/;
  return new RegExp(
    `^/${pattern.split("/").filter(Boolean).map(segmentPattern).join("/")}/?$`,
    "i",
  );
}

export function matchRoute(pathname: string): RouteManifestEntry | undefined {
  const normalized = pathname.length > 1 ? pathname.replace(/\/+$/, "") : pathname;
  return routeManifest.routes.find((route) =>
    [route.canonical, ...route.aliases].some((pattern) =>
      routePatternToRegExp(pattern).test(normalized),
    ),
  );
}

export function countRoutePatterns(): number {
  return routeManifest.routes.reduce((count, route) => count + 1 + route.aliases.length, 0);
}

export function currentClassicUrl(): string {
  const base = routeManifest.reactBasePath;
  const classicPath = window.location.pathname.startsWith(`${base}/`)
    ? window.location.pathname.slice(base.length)
    : window.location.pathname === base
      ? "/"
      : window.location.pathname;
  return `${classicPath || "/"}${window.location.search}${window.location.hash}`;
}

export function currentReactUrl(): string {
  const isReactPath =
    window.location.pathname === routeManifest.reactBasePath ||
    window.location.pathname.startsWith(`${routeManifest.reactBasePath}/`);
  const path = isReactPath
    ? window.location.pathname
    : `${routeManifest.reactBasePath}${window.location.pathname === "/" ? "/" : window.location.pathname}`;
  return `${path}${window.location.search}${window.location.hash}`;
}

export function setUiVariant(variant: "classic" | "react"): void {
  const secure = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `utf_ui_variant=${variant}; Path=/; SameSite=Lax; Max-Age=15552000${secure}`;
}

export function switchToClassic(): void {
  setUiVariant("classic");
  window.location.assign(currentClassicUrl());
}
