import type { Page, Route } from "@playwright/test";
import { mockFleetStream, type FleetStreamMock } from "./fleetStream";
import { seedSettingsPanels } from "./settingsPanels";

/**
 * The shared strict API mock layer of every registry-driven audit, walk and journey (Phase 69, Pattern 1).
 *
 * `installApp` answers the requests every authenticated page sends (the signed-in user, the authentication
 * providers, the site image settings and the quiet cost and notification reads), offers a handler chain for the
 * requests a screen needs, and aborts and records every other `/api/**` request. Each audit asserts the recorded
 * list is empty, so a request that no handler named can never reach a real backend and can never be forgotten.
 *
 * This module imports Playwright types only, so Vitest can import the registry modules that use it.
 */

/** The cultures every audit runs in; the value is what the app stores under `UtfCulture`. */
export type Culture = "en" | "de";

/** The id of the signed-in user the mock serves. Fleet fixtures use the same id as their default creator. */
export const auditUserId = "10000000-0000-4000-8000-000000000001";

/**
 * Answers one intercepted API request when the handler is responsible for it.
 *
 * @param route The intercepted request; fulfil it only when the handler is responsible.
 * @param method The HTTP method of the request.
 * @param path The URL path of the request, without the query string.
 * @param url The full URL of the request, for handlers that read query parameters.
 * @returns True when the handler fulfilled the request; false lets the next handler try.
 */
export type ApiHandler = (route: Route, method: string, path: string, url: URL) => Promise<boolean>;

/** The record of one installed mock; the lists keep growing while the page runs. */
export interface MockSession {
  /** Requests no handler answered and that were aborted. Every audit asserts this is empty. */
  unexpected: string[];
  /** Requests the permissive fallback answered with a 404; they never fail a test. */
  served: string[];
  /** The mocked update stream when the session was installed with `stream`; otherwise undefined. */
  stream?: FleetStreamMock | undefined;
}

/** Options of {@link installApp}. */
export interface InstallAppOptions {
  /** The culture stored in local storage before the first navigation. */
  culture: Culture;
  /** The claim identifiers of the signed-in user, each with a null value. Defaults to `["sv"]`. */
  claims?: readonly string[] | undefined;
  /** Handlers tried in order before the built-in handlers, so a screen can override any default. */
  handlers?: readonly ApiHandler[] | undefined;
  /** When true, unmatched requests get a 404 and are recorded in `served` instead of being aborted. */
  permissive?: boolean | undefined;
  /** When true, the fleet update stream is mocked and quiet. Defaults to false. */
  stream?: boolean | undefined;
  /**
   * The Settings panel anchor ids to open before the first navigation; every panel by default, an empty array for the
   * collapsed first-visit default.
   */
  settingsPanels?: readonly string[] | undefined;
}

/**
 * Builds a handler that answers one method and path with a JSON body.
 *
 * @param method The HTTP method to answer, for example `GET`.
 * @param pattern An exact path, or a regular expression the path must match.
 * @param body The JSON body, or a function that derives it from the request URL.
 * @param status The HTTP status. Defaults to 200.
 * @returns The handler.
 */
export function jsonHandler(
  method: string,
  pattern: RegExp | string,
  body: unknown | ((url: URL) => unknown),
  status?: number | undefined,
): ApiHandler {
  return async (route, requestMethod, path, url) => {
    if (requestMethod !== method) return false;
    if (typeof pattern === "string" ? path !== pattern : !pattern.test(path)) return false;
    const json = typeof body === "function" ? (body as (target: URL) => unknown)(url) : body;
    await route.fulfill({ status: status ?? 200, json });
    return true;
  };
}

/** The accrued-cost read of any server: the quiet state of a server that never had a session that cost. */
const quietAccruedCost = jsonHandler(
  "GET",
  /^\/api\/game-servers\/[0-9a-f-]{36}\/accrued-cost$/i,
  (url: URL) => ({ gameServerId: url.pathname.split("/")[3], session: null }),
);

/**
 * The answer of `GET /api/game-servers/{id}/cost-owner/options` for any server: no owner to choose. The Cost owner
 * picker requests owners on its first focus, which a keyboard walk over the open Settings page reaches, so every
 * audit gets a quiet answer.
 */
const quietCostOwnerOptions = jsonHandler(
  "GET",
  /^\/api\/game-servers\/[0-9a-f-]{36}\/cost-owner\/options$/i,
  [],
);

/** The reads every authenticated page sends and that no screen under audit cares about. */
const baselineHandlers: readonly ApiHandler[] = [
  jsonHandler("GET", "/api/users/authentication-providers", { steam: true, discord: true }),
  jsonHandler("GET", "/api/site-image-settings", []),
  jsonHandler("GET", "/api/game-servers/notifications", []),
  jsonHandler("GET", "/api/cost-alerts", []),
  quietAccruedCost,
  quietCostOwnerOptions,
];

/**
 * Builds the answer of `GET /api/users/me`.
 *
 * @param claims The claim identifiers of the user.
 * @returns The user body.
 */
function currentUser(claims: readonly string[]) {
  return {
    primaryKey: auditUserId,
    nickname: "Operator",
    isBanned: false,
    isVerified: true,
    isDeleted: false,
    isVereinMember: false,
    claims: claims.map((identifier) => ({ identifier, value: null })),
    roles: [],
  };
}

/**
 * Installs the strict mock layer on a page; call it before the first navigation.
 *
 * Handler order: the handlers passed in `options.handlers`, then the signed-in user, then the baseline reads. The
 * first handler that returns true answers the request. Every other `/api/**` request is aborted and recorded in
 * `unexpected`, or answered with a 404 and recorded in `served` when `permissive` is set. A handler that throws is
 * recorded in `unexpected` too, because a thrown route handler would otherwise leave the request hanging.
 *
 * @param page The page to mock.
 * @param options The culture, claims, handlers and fallback behavior.
 * @returns The record of requests that were not answered by a handler.
 */
export async function installApp(page: Page, options: InstallAppOptions): Promise<MockSession> {
  const session: MockSession = { unexpected: [], served: [] };
  const claims = options.claims ?? ["sv"];
  const handlers: readonly ApiHandler[] = [
    ...(options.handlers ?? []),
    jsonHandler("GET", "/api/users/me", currentUser(claims)),
    ...baselineHandlers,
  ];

  await page.addInitScript((value) => localStorage.setItem("UtfCulture", value), options.culture);
  await seedSettingsPanels(page, options.settingsPanels);
  await page.route("**/appsettings.json", (route) =>
    route.fulfill({
      json: {
        Api: { BaseUrl: "/api/" },
        Urls: { TeamSpeak3: "", Discord: "" },
        Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
      },
    }),
  );
  await page.route("**/Insignia-48px.png", (route) =>
    route.fulfill({ status: 200, contentType: "image/png", body: "" }),
  );
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const method = request.method();
    const url = new URL(request.url());
    const label = `${method} ${url.pathname}`;
    try {
      for (const handler of handlers) {
        if (await handler(route, method, url.pathname, url)) return;
      }
      if (options.permissive) {
        session.served.push(label);
        await route.fulfill({ status: 404, json: { title: "Not found" } });
        return;
      }
      session.unexpected.push(label);
      await route.abort();
    } catch (error) {
      // A page that navigated away cancels its requests; fulfilling them afterwards fails harmlessly.
      const message = error instanceof Error ? error.message : String(error);
      if (/closed|cancel|abort|already handled/i.test(message)) return;
      session.unexpected.push(`${label} (handler error: ${message})`);
      await route.abort().catch(() => undefined);
    }
  });
  if (options.stream) session.stream = await mockFleetStream(page);
  return session;
}
