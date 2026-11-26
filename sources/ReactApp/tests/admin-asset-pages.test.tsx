import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { Component as ModPackCreatePage } from "@/features/admin/ModPackCreatePage";
import { Component as ModPacksPage } from "@/features/admin/ModPacksPage";
import { Component as ModPackWorkspacePage } from "@/features/admin/ModPackWorkspacePage";
import { Component as TerrainCreatePage } from "@/features/admin/TerrainCreatePage";
import { Component as TerrainDetailPage } from "@/features/admin/TerrainDetailPage";
import { Component as TerrainsPage } from "@/features/admin/TerrainsPage";
import { assetReadinessTones, assetStateTones, modOriginTones } from "@/features/admin/assetTones";
import * as terrains from "@/features/admin/terrains";
import { hostingService } from "@/features/hosting/services";
import { toneOf } from "@/components/statusTones";
import {
  modPackService,
  type FullModPackDefinition,
  type ModPackListSnapshot,
  type ModPackMembership,
  type NormalizedGameCapability,
  type NormalizedModPackWorkspace,
} from "@/features/admin/modPacks";
import { renderWithProviders } from "./helpers/renderWithProviders";

const authState = vi.hoisted(() => ({ user: undefined as FullUser | undefined }));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: authState.user,
    isLoading: false,
    isAuthenticated: Boolean(authState.user),
  }),
}));

const administrator = {
  primaryKey: "10000000-0000-4000-8000-000000000001",
  nickname: "Asset admin",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  claims: [{ identifier: "adm:mp", value: null }],
  roles: [],
} as FullUser;

const outsider = { ...administrator, nickname: "Plain member", claims: [] } as FullUser;

const modPackDefinitions: FullModPackDefinition[] = [
  {
    primaryKey: 11,
    gameIdentifier: "arma3",
    title: "Operations core",
    owner: null,
    isActive: true,
    modPackRevisions: [{ primaryKey: 3, isActive: true, timeStampCreated: "2026-08-14T12:00:00Z" }],
  },
];

const modPackSnapshot: ModPackListSnapshot = { count: 1, definitions: modPackDefinitions };

const capabilities = [
  {
    gameIdentifier: "arma3",
    displayName: "Arma 3",
    supportsModPacks: true,
    modProviderBindings: [],
    modPackFormats: [],
  },
  {
    gameIdentifier: "reforger",
    displayName: "Arma Reforger",
    supportsModPacks: true,
    modProviderBindings: [],
    modPackFormats: [],
  },
] as unknown as NormalizedGameCapability[];

const workspace: NormalizedModPackWorkspace = {
  definitionId: 11,
  gameIdentifier: "arma3",
  title: "Operations core",
  isActive: true,
  revisions: [
    {
      revisionId: 3,
      isActive: true,
      timeStampCreated: "2026-08-14T12:00:00Z",
      updatedByFk: "10000000-0000-4000-8000-000000000001",
      directModCount: 0,
      nestedModPackCount: 0,
    },
  ],
};

const terrainAdministrator = {
  ...administrator,
  nickname: "Terrain admin",
  claims: [{ identifier: "adm:trn", value: null }],
} as FullUser;

const terrain: terrains.Terrain = {
  gameIdentifier: "arma3",
  primaryKey: 5,
  title: "Altis",
  imageAssetFk: "asset-1",
  imagePath: "/images/altis.png",
  isActive: true,
};

const terrainWindow: terrains.TerrainWindow = { terrains: [terrain], totalCount: 1 };

const emptyMembership = { mods: [], dependencies: [] } as unknown as ModPackMembership;

/** Mocks every read the mod pack create and workspace pages perform. */
function mockModPackReads(options: { workspace?: NormalizedModPackWorkspace | null } = {}) {
  vi.spyOn(modPackService, "capabilities").mockResolvedValue(capabilities);
  vi.spyOn(modPackService, "mods").mockResolvedValue([]);
  vi.spyOn(modPackService, "list").mockResolvedValue([]);
  vi.spyOn(modPackService, "members").mockResolvedValue(emptyMembership);
  vi.spyOn(modPackService, "normalizedWorkspace").mockResolvedValue(
    (options.workspace === undefined ? workspace : options.workspace) as never,
  );
}

function renderWorkspace(route = "/command/mod-packs/11") {
  return renderWithProviders(<ModPackWorkspacePage />, {
    route,
    path: "/command/mod-packs/:modPackId/:section?",
  });
}

function expectOneHeadingAbove(element: HTMLElement) {
  expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
  expect(
    screen.getByRole("heading", { level: 1 }).compareDocumentPosition(element) &
      Node.DOCUMENT_POSITION_FOLLOWING,
  ).toBeTruthy();
}

describe("mod pack and terrain pages", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = administrator;
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await act(async () => i18n.changeLanguage("en"));
  });

  describe("mod packs list", () => {
    it("renders the navigation label as the only h1 above the forbidden state", () => {
      authState.user = outsider;
      const snapshot = vi.spyOn(modPackService, "snapshot");
      renderWithProviders(<ModPacksPage />);

      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Mod packs");
      expectOneHeadingAbove(screen.getByRole("status"));
      expect(screen.getByRole("status")).toHaveTextContent(
        "Mod pack administrator access required",
      );
      expect(snapshot).not.toHaveBeenCalled();
    });

    it("reads Mod-Pakete in German", async () => {
      vi.spyOn(modPackService, "snapshot").mockResolvedValue(modPackSnapshot);
      await act(async () => i18n.changeLanguage("de"));
      renderWithProviders(<ModPacksPage />);

      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Mod-Pakete");
      expect(await screen.findByText("Operations core")).toBeInTheDocument();
    });

    it("uses the shared search field and filter chips", async () => {
      vi.spyOn(modPackService, "snapshot").mockResolvedValue(modPackSnapshot);
      renderWithProviders(<ModPacksPage />);

      await screen.findByText("Operations core");
      expect(screen.getByRole("searchbox", { name: "Search mod packs" })).toHaveAttribute(
        "type",
        "search",
      );
      const owners = screen.getByRole("group", { name: "All owners" });
      expect(within(owners).getByRole("button", { name: "All owners" })).toHaveAttribute(
        "aria-pressed",
        "true",
      );
      expect(within(owners).getByRole("button", { name: "My mod packs" })).toHaveAttribute(
        "aria-pressed",
        "false",
      );
    });

    it("names how to clear the filter when no mod pack matches", async () => {
      vi.spyOn(modPackService, "snapshot").mockResolvedValue(modPackSnapshot);
      renderWithProviders(<ModPacksPage />);

      await screen.findByText("Operations core");
      await userEvent.click(screen.getByRole("button", { name: /Missing revisions/ }));

      expect(
        screen.getByRole("heading", { level: 2, name: "No mod packs match" }),
      ).toBeInTheDocument();
      expect(
        screen.getByText("Clear the search or filters to see all mod packs."),
      ).toBeInTheDocument();
    });

    it("keeps the loaded rows under a failed refetch and retries for real", async () => {
      const snapshot = vi
        .spyOn(modPackService, "snapshot")
        .mockResolvedValueOnce(modPackSnapshot)
        .mockRejectedValueOnce(new Error("offline"))
        .mockResolvedValue(modPackSnapshot);
      renderWithProviders(<ModPacksPage />);

      await screen.findByText("Operations core");
      await userEvent.click(screen.getAllByRole("button", { name: "Refresh" })[0]!);
      const alert = await screen.findByRole("alert");
      expect(alert).toHaveTextContent("Mod pack data could not be loaded");
      expect(screen.getByText("Operations core")).toBeInTheDocument();

      await userEvent.click(within(alert).getByRole("button", { name: "Try again" }));
      await waitFor(() => expect(snapshot).toHaveBeenCalledTimes(3));
      await waitFor(() => expect(screen.queryByRole("alert")).not.toBeInTheDocument());
    });
  });

  describe("mod pack create", () => {
    it("renders one h1 above the forbidden state", () => {
      authState.user = outsider;
      renderWithProviders(<ModPackCreatePage />);

      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Create mod pack");
      expectOneHeadingAbove(screen.getByRole("status"));
    });

    it("renders the game picker as the shared padded select with a visible label", async () => {
      mockModPackReads();
      renderWithProviders(<ModPackCreatePage />);

      const game = await screen.findByRole("combobox", { name: "Game" });
      expect(game).toHaveClass("form-field__control--select");
      expect(game.parentElement).toHaveClass("form-field__select");
      expect(
        within(game)
          .getAllByRole("option")
          .map((option) => option.textContent),
      ).toEqual(["Arma 3", "Arma Reforger"]);
    });

    it("keeps the title validation and enables the review step once content exists", async () => {
      mockModPackReads();
      renderWithProviders(<ModPackCreatePage />);

      await screen.findByRole("combobox", { name: "Game" });
      expect(screen.getByRole("button", { name: "Review mod pack" })).toBeDisabled();
      expect(screen.getByText("A title is required.")).toBeInTheDocument();
      await userEvent.type(screen.getByRole("textbox", { name: "Title" }), "Core");
      expect(screen.getByText("Add at least one mod or nested mod pack.")).toBeInTheDocument();
    });

    it("names the next step when no game supports mod packs", async () => {
      mockModPackReads();
      vi.spyOn(modPackService, "capabilities").mockResolvedValue([]);
      renderWithProviders(<ModPackCreatePage />);

      expect(
        await screen.findByRole("heading", { level: 2, name: "No game supports mod packs" }),
      ).toBeInTheDocument();
      expect(screen.getByText("Add a game that supports mod packs, then come back.")).toBeVisible();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    });

    it("renders the content editor controls as shared controls with no look-alike buttons", async () => {
      mockModPackReads();
      vi.spyOn(modPackService, "mods").mockResolvedValue([
        {
          primaryKey: "m-1",
          title: "CBA",
          providerIdentifier: "steam-workshop",
          canonicalSourceIdentifier: "450814997",
          usageRole: "Shared",
          versions: [],
        },
      ] as never);
      renderWithProviders(<ModPackCreatePage />);

      expect(await screen.findByRole("searchbox", { name: "Search mod catalog" })).toBeVisible();
      expect(screen.getByRole("searchbox", { name: "Search mod packs" })).toBeVisible();
      expect(screen.getByRole("textbox", { name: "Paste one source per line" })).toBeVisible();
      expect(screen.getByRole("combobox", { name: "Provider" })).toHaveClass(
        "form-field__control--select",
      );
      const catalogItem = await screen.findByRole("button", { name: /CBA/ });
      expect(catalogItem).toHaveClass("button");
      await userEvent.click(catalogItem);
      expect(screen.getByRole("button", { name: "Move mod up" })).toBeDisabled();
      expect(screen.getByRole("button", { name: "Move mod down" })).toBeDisabled();
      expect(screen.getByRole("button", { name: "Remove" })).toHaveClass("button--ghost");
    });
  });

  describe("mod pack workspace", () => {
    it("renders one h1 above the forbidden state and reads nothing", () => {
      authState.user = outsider;
      const read = vi.spyOn(modPackService, "normalizedWorkspace");
      renderWorkspace();

      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Mod pack detail");
      expectOneHeadingAbove(screen.getByRole("status"));
      expect(read).not.toHaveBeenCalled();
    });

    it("renders one h1 above the not-found state with a way back", async () => {
      mockModPackReads({ workspace: null });
      renderWorkspace();

      expect(
        await screen.findByRole("heading", { level: 2, name: "Mod pack not found" }),
      ).toBeInTheDocument();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(
        screen.getByText("It may have been deleted. Go back to the list of mod packs."),
      ).toBeVisible();
      const back = screen.getAllByRole("link", { name: "All mod packs" });
      expect(back.every((link) => link.getAttribute("href") === "/command/mod-packs")).toBe(true);
    });

    it("shows a retryable error above the page when the workspace cannot load", async () => {
      mockModPackReads();
      const read = vi
        .spyOn(modPackService, "normalizedWorkspace")
        .mockRejectedValueOnce(new Error("offline"))
        .mockResolvedValue(workspace);
      renderWorkspace();

      const alert = await screen.findByRole("alert");
      expect(alert).toHaveTextContent("Failed to load mod pack 11: offline");
      expectOneHeadingAbove(alert);
      await userEvent.click(within(alert).getByRole("button", { name: "Try again" }));
      await waitFor(() => expect(read).toHaveBeenCalledTimes(2));
      expect(await screen.findByRole("link", { name: "Revisions" })).toBeInTheDocument();
    });

    it("labels the content and danger tabs from locale keys in both languages", async () => {
      mockModPackReads();
      renderWorkspace();

      const tabs = await screen.findByRole("navigation", { name: "Mod pack workspace navigation" });
      expect(
        within(tabs)
          .getAllByRole("link")
          .map((link) => link.textContent),
      ).toEqual(["Overview", "Content", "Edit", "Revisions", "Danger zone"]);
    });

    it("labels the content and danger tabs in German", async () => {
      mockModPackReads();
      await act(async () => i18n.changeLanguage("de"));
      renderWorkspace();

      const tabs = await screen.findByRole("navigation", {
        name: "Mod-Paket-Arbeitsbereichsnavigation",
      });
      const labels = within(tabs)
        .getAllByRole("link")
        .map((link) => link.textContent);
      expect(labels).toContain("Inhalt");
      expect(labels).toContain("Gefahrenbereich");
      expect(labels).not.toContain("Content");
    });

    it("names the effect of the deactivate dialog dismiss button", async () => {
      mockModPackReads();
      renderWorkspace("/command/mod-packs/11/danger");

      await userEvent.click(await screen.findByRole("button", { name: "Deactivate" }));
      const dialog = await screen.findByRole("dialog", { hidden: true });
      const dismiss = within(dialog).getByRole("button", {
        name: "Keep mod pack active",
        hidden: true,
      });
      expect(dismiss).toBeInTheDocument();
      expect(within(dialog).queryByRole("button", { name: "Cancel", hidden: true })).toBeNull();
    });

    it("renders the empty overview sections with a title and the next step", async () => {
      mockModPackReads();
      renderWorkspace();

      expect(
        await screen.findByRole("heading", { level: 2, name: "No mods in this revision" }),
      ).toBeInTheDocument();
      expect(screen.getByText("Edit the mod pack to add mods.")).toBeVisible();
      expect(
        screen.getByRole("heading", { level: 2, name: "No nested mod packs" }),
      ).toBeInTheDocument();
      expect(
        screen.getByRole("heading", { level: 2, name: "No custom file format" }),
      ).toBeInTheDocument();
    });
  });

  describe("terrains list", () => {
    it("renders the navigation label as the only h1 above the forbidden state", () => {
      authState.user = outsider;
      const read = vi.spyOn(terrains, "loadTerrainWindow");
      renderWithProviders(<TerrainsPage />);

      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Terrains");
      expectOneHeadingAbove(screen.getByRole("status"));
      expect(read).not.toHaveBeenCalled();
    });

    it("reads Karten in German", async () => {
      authState.user = terrainAdministrator;
      vi.spyOn(terrains, "loadTerrainWindow").mockResolvedValue(terrainWindow);
      await act(async () => i18n.changeLanguage("de"));
      renderWithProviders(<TerrainsPage />);

      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Karten");
      expect(await screen.findByText("Altis")).toBeInTheDocument();
    });

    it("uses shared chips and names how to clear the filter when no terrain matches", async () => {
      authState.user = terrainAdministrator;
      vi.spyOn(terrains, "loadTerrainWindow").mockResolvedValue(terrainWindow);
      renderWithProviders(<TerrainsPage />);

      await screen.findByText("Altis");
      const chips = screen.getByRole("group", { name: "Terrain filters" });
      expect(within(chips).getByRole("button", { name: "All" })).toHaveAttribute(
        "aria-pressed",
        "true",
      );
      await userEvent.click(within(chips).getByRole("button", { name: "Missing image" }));

      expect(
        screen.getByRole("heading", { level: 2, name: "No terrains match" }),
      ).toBeInTheDocument();
      expect(screen.getByText("Clear the filter to see all terrains.")).toBeVisible();
    });

    it("keeps the loaded rows under a failed refetch and retries for real", async () => {
      authState.user = terrainAdministrator;
      const read = vi
        .spyOn(terrains, "loadTerrainWindow")
        .mockResolvedValueOnce(terrainWindow)
        .mockRejectedValueOnce(new Error("offline"))
        .mockResolvedValue(terrainWindow);
      renderWithProviders(<TerrainsPage />);

      await screen.findByText("Altis");
      await userEvent.click(screen.getAllByRole("button", { name: "Refresh" })[0]!);
      const alert = await screen.findByRole("alert");
      expect(screen.getByText("Altis")).toBeInTheDocument();

      await userEvent.click(within(alert).getByRole("button", { name: "Try again" }));
      await waitFor(() => expect(read).toHaveBeenCalledTimes(3));
      await waitFor(() => expect(screen.queryByRole("alert")).not.toBeInTheDocument());
    });
  });

  describe("terrain create", () => {
    it("renders one h1 above the forbidden state", () => {
      authState.user = outsider;
      renderWithProviders(<TerrainCreatePage />);

      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Create terrain");
      expectOneHeadingAbove(screen.getByRole("status"));
    });

    it("renders the game picker as the shared select and the steps as shared buttons", async () => {
      authState.user = terrainAdministrator;
      vi.spyOn(hostingService, "capabilities").mockResolvedValue([
        { gameIdentifier: "arma3", displayName: "Arma 3" },
        { gameIdentifier: "reforger", displayName: "Arma Reforger" },
      ] as never);
      renderWithProviders(<TerrainCreatePage />);

      const game = await screen.findByRole("combobox", { name: "Game" });
      expect(game).toHaveClass("form-field__control--select");
      expect(game.parentElement).toHaveClass("form-field__select");
      expect(screen.getByRole("textbox", { name: "Title" })).toHaveClass("form-field__control");
      const steps = screen.getByRole("list", { name: "Create terrain steps" });
      for (const step of within(steps).getAllByRole("button")) {
        expect(step).toHaveClass("button");
      }
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    });

    it("keeps the title validation message", async () => {
      authState.user = terrainAdministrator;
      vi.spyOn(hostingService, "capabilities").mockResolvedValue([
        { gameIdentifier: "arma3", displayName: "Arma 3" },
      ] as never);
      renderWithProviders(<TerrainCreatePage />);

      const title = await screen.findByRole("textbox", { name: "Title" });
      await userEvent.type(title, "x");
      await userEvent.clear(title);
      expect(await screen.findByRole("alert")).toHaveTextContent("Title required");
      expect(title).toHaveAttribute("aria-invalid", "true");
    });
  });

  describe("terrain detail", () => {
    function renderDetail(id = "5") {
      return renderWithProviders(<TerrainDetailPage />, {
        route: `/command/terrains/${id}`,
        path: "/command/terrains/:terrainId",
      });
    }

    it("renders one h1 above the forbidden state and reads nothing", () => {
      authState.user = outsider;
      const read = vi.spyOn(terrains, "loadTerrain");
      renderDetail();

      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Terrain record");
      expectOneHeadingAbove(screen.getByRole("status"));
      expect(read).not.toHaveBeenCalled();
    });

    it("renders one h1 above the not-found state for an invalid id", () => {
      authState.user = terrainAdministrator;
      renderDetail("not-an-id");

      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByRole("heading", { level: 2, name: "Terrain not found" })).toBeVisible();
      expect(screen.getByRole("link", { name: "All terrains" })).toHaveAttribute(
        "href",
        "/command/terrains",
      );
    });

    it("renders one h1 above the not-found state when the terrain does not exist", async () => {
      authState.user = terrainAdministrator;
      vi.spyOn(terrains, "loadTerrain").mockResolvedValue(undefined);
      renderDetail("99");

      expect(
        await screen.findByRole("heading", { level: 2, name: "Terrain not found" }),
      ).toBeVisible();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByText("No terrain was found for ID 99.")).toBeVisible();
    });

    it("renders one h1 above a retryable error", async () => {
      authState.user = terrainAdministrator;
      const read = vi
        .spyOn(terrains, "loadTerrain")
        .mockRejectedValueOnce(new Error("offline"))
        .mockResolvedValue(terrain);
      renderDetail();

      const alert = await screen.findByRole("alert");
      expectOneHeadingAbove(alert);
      await userEvent.click(within(alert).getByRole("button", { name: "Try again" }));
      await waitFor(() => expect(read).toHaveBeenCalledTimes(2));
      expect(await screen.findByRole("heading", { level: 1, name: "Altis" })).toBeVisible();
    });

    it("names keeping the terrain in the deactivate dialog, never Cancel", async () => {
      authState.user = terrainAdministrator;
      vi.spyOn(terrains, "loadTerrain").mockResolvedValue(terrain);
      renderDetail();

      await userEvent.click(await screen.findByRole("button", { name: "Deactivate" }));
      const dismiss = await screen.findByRole("button", {
        name: "Keep terrain active",
        hidden: true,
      });
      const dialog = dismiss.closest("dialog")!;
      expect(within(dialog).queryByRole("button", { name: "Cancel", hidden: true })).toBeNull();
    });

    it("renders the edit form with shared fields and the shared image buttons", async () => {
      authState.user = terrainAdministrator;
      vi.spyOn(terrains, "loadTerrain").mockResolvedValue(terrain);
      renderDetail();

      const title = await screen.findByRole("textbox", { name: "Title" });
      expect(title).toHaveClass("form-field__control");
      expect(screen.getByRole("button", { name: "Change image" })).toHaveClass("button--small");
      expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
      await userEvent.type(title, " II");
      await waitFor(() =>
        expect(screen.getByRole("button", { name: "Save changes" })).toBeEnabled(),
      );
    });
  });

  describe("badge tones", () => {
    it("takes asset badge tones from the closed lexicon", () => {
      expect(toneOf(assetStateTones, "active")).toBe("success");
      expect(toneOf(assetStateTones, "inactive")).toBe("neutral");
      expect(toneOf(assetReadinessTones, "needs-attention")).toBe("warning");
      expect(toneOf(modOriginTones, "new-entry")).toBe("warning");
      expect(toneOf(modOriginTones, "unknown")).toBe("neutral");
    });
  });
});
