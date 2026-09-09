import { beforeEach, describe, expect, it, vi } from "vitest";

const request = vi.fn();

vi.mock("@/api/client", () => ({
  getApiClient: () => ({ request, download: vi.fn() }),
}));

import { modPackService } from "@/features/admin/modPacks";

describe("normalized mod-pack contracts", () => {
  beforeEach(() => request.mockReset());

  it("loads the normalized workspace endpoint", async () => {
    request.mockResolvedValue({
      definitionId: 12,
      gameIdentifier: "arma3",
      title: "Operations",
      isActive: true,
      revisions: [],
    });

    await expect(modPackService.normalizedWorkspace(12)).resolves.toMatchObject({
      definitionId: 12,
    });
    expect(request).toHaveBeenCalledWith("/game-content/mod-packs/12", {
      signal: undefined,
      expectedStatuses: [404],
    });
  });

  it("builds a format link for any historical revision", () => {
    expect(modPackService.formatDownloadPath(41, "arma3-launcher-html")).toBe(
      "/game-content/mod-packs/revisions/41/formats/arma3-launcher-html",
    );
    expect(modPackService.formatDownloadPath(7, "custom format")).toBe(
      "/game-content/mod-packs/revisions/7/formats/custom%20format",
    );
  });
});
