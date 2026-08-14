import { hostingRouteDefinitions } from "@/features/hosting/routes";

describe("hosting router integration contract", () => {
  it("exposes the command, fleet, create, workspace, profile, and finance routes", () => {
    expect(hostingRouteDefinitions.map((route) => route.manifestPattern)).toEqual([
      "/command",
      "/command/servers",
      "/command/servers/create",
      "/command/servers/{ServerId:guid}",
      "/command/configuration-profiles",
      "/command/hosting-finance",
    ]);
    expect(hostingRouteDefinitions.find((route) => route.path.endsWith(":serverId"))?.parity).toBe(
      "workflow",
    );
  });
});
