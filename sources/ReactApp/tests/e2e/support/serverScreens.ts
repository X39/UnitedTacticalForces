import { adjacentScreens } from "./screens/adjacentScreens";
import { fleetScreens } from "./screens/fleetScreens";
import type { ScreenEntry } from "./screens/registryTypes";
import { workspaceScreens } from "./screens/workspaceScreens";

/**
 * The screen registry every audit iterates: the concatenation of the fleet, workspace and adjacent slices.
 *
 * The Vitest guard `tests/hosting/screen-registry.test.ts` fails when a required id has neither an entry in a slice
 * nor a pending declaration there, so a screen cannot ship without an audit.
 */
export const serverScreens: readonly ScreenEntry[] = [
  ...fleetScreens,
  ...workspaceScreens,
  ...adjacentScreens,
];
