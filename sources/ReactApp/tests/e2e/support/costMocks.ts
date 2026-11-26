import type { Page, Route } from "@playwright/test";

/**
 * Answers the two cost reads every workspace page and the profile menu send: the accrued cost of the server and the
 * viewer's cost alert list.
 *
 * The answers are the quiet state: a server that never had a session that cost and no undismissed alert. Specs that
 * test cost behavior mock these requests themselves; every other spec calls this helper before its unexpected-request
 * fallback so the fallback stays meaningful.
 *
 * @param route The intercepted request.
 * @param method The HTTP method of the request.
 * @param path The URL path of the request.
 * @param serverId The server whose accrued cost is mocked.
 * @returns True when the request was one of the two cost reads and was answered.
 */
export async function fulfillCostRoute(
  route: Route,
  method: string,
  path: string,
  serverId: string,
): Promise<boolean> {
  if (method !== "GET") return false;
  if (path === `/api/game-servers/${serverId}/accrued-cost`) {
    await route.fulfill({ json: { gameServerId: serverId, session: null } });
    return true;
  }
  if (path === "/api/cost-alerts") {
    await route.fulfill({ json: [] });
    return true;
  }
  return false;
}

/**
 * Answers both quiet cost reads for a spec that installs a strict `**\/api/**` guard and does not test cost behavior:
 * the profile menu's cost alert list and the accrued cost of any server. Register it after the guard so it takes
 * precedence.
 *
 * @param page The page whose requests are intercepted.
 */
export async function mockQuietCostReads(page: Page): Promise<void> {
  await page.route("**/api/cost-alerts", (route) => route.fulfill({ json: [] }));
  await page.route("**/api/game-servers/*/accrued-cost", (route) => {
    const serverId = new URL(route.request().url()).pathname.split("/")[3];
    return route.fulfill({ json: { gameServerId: serverId, session: null } });
  });
}
