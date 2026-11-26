/**
 * The UAT base URL used when `UAT_BASE_URL` is not set: the React dev server of `scripts/Start-Uat.ps1`.
 */
export const defaultUatBaseUrl = "http://localhost:5173/next/";

/** The only host names a UAT run may drive; both name this machine. */
const localHostNames: ReadonlySet<string> = new Set(["localhost", "127.0.0.1"]);

/**
 * Refuses a UAT base URL that does not point at this machine.
 *
 * The seeded UAT journeys create servers, start them and change settings. Aimed at a deployed environment by a wrong
 * `UAT_BASE_URL`, they would mutate real data, so the suite stops before any test runs unless the host is `localhost`
 * or `127.0.0.1`.
 *
 * This module is pure (no Playwright import), so a Vitest test can prove the rule.
 *
 * @param baseUrl The configured UAT base URL.
 * @throws Error when the URL cannot be parsed, or its host is neither `localhost` nor `127.0.0.1`.
 */
export function assertLocalUatTarget(baseUrl: string): void {
  let hostname: string;
  try {
    hostname = new URL(baseUrl).hostname;
  } catch {
    throw new Error(
      `UAT refused: "${baseUrl}" is not a valid URL, so it cannot be shown to be local.`,
    );
  }
  if (!localHostNames.has(hostname)) {
    throw new Error(
      `UAT refused: the base URL host "${hostname}" is not localhost or 127.0.0.1. The seeded journeys mutate ` +
        "data and must only run against the local UAT stack of scripts/Start-Uat.ps1.",
    );
  }
}
