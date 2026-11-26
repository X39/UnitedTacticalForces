import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import type { ReactElement } from "react";
import { beforeEach } from "vitest";
import type { FullUser } from "@/api/types";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { GalleryAssetPicker } from "@/features/admin/GalleryAssetPicker";
import { Component as FieldReportEditorPage } from "@/features/admin/FieldReportEditorPage";
import { Component as FieldReportsPage } from "@/features/admin/FieldReportsPage";
import * as fieldReports from "@/features/admin/fieldReports";
import { Component as MediaGalleryPage } from "@/features/admin/MediaGalleryPage";
import { Component as SiteImagesPage } from "@/features/admin/SiteImagesPage";
import * as siteImages from "@/features/admin/siteImages";

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
  nickname: "Media admin",
  isBanned: false,
  isVerified: true,
  isDeleted: false,
  isVereinMember: false,
  claims: [{ identifier: "adm:wiki", value: null }],
  roles: [],
} as FullUser;

const outsider = { ...administrator, nickname: "Plain member", claims: [] } as FullUser;

const briefing: siteImages.MediaAsset = {
  primaryKey: "a0000000-0000-4000-8000-000000000001",
  originalFileName: "briefing.png",
  contentType: "image/png",
  size: 2048,
  imagePath: "/media/briefing.png",
  timeStampCreated: "2026-01-01T10:00:00Z",
  timeStampUpdated: "2026-01-01T10:00:00Z",
  createdByName: "Alex",
  tags: ["briefing"],
  usages: [],
};

const terrainMap: siteImages.MediaAsset = {
  ...briefing,
  primaryKey: "a0000000-0000-4000-8000-000000000002",
  originalFileName: "terrain.png",
  tags: ["terrain"],
  usages: [
    { domain: "terrain", domainKey: "t-1", displayName: "Altis", path: "/command/terrains/t-1" },
  ],
};

const heroSetting: siteImages.SiteImageSetting = {
  key: "home.hero",
  title: "Home hero",
  description: "The large image on the home page.",
  group: "home",
  sortOrder: 1,
  imageAssetFk: "",
  imagePath: "",
  timeStampCreated: "2026-01-01T10:00:00Z",
  timeStampUpdated: "2026-01-01T10:00:00Z",
  updatedByName: null,
};

const reportId = "b0000000-0000-4000-8000-000000000001";

function makeReport(
  overrides: Partial<fieldReports.CommandFieldReport>,
): fieldReports.CommandFieldReport {
  return {
    primaryKey: reportId,
    slug: "operation-dawn",
    title: "Operation Dawn",
    excerpt: "A short summary.",
    category: "Operations",
    markdown: "# Operation Dawn",
    heroImageAlt: "",
    isFeatured: false,
    sortOrder: 0,
    isPublished: false,
    isDeleted: false,
    timeStampCreated: "2026-01-01T10:00:00Z",
    timeStampUpdated: "2026-01-02T10:00:00Z",
    auditRecordCount: 2,
    ...overrides,
  };
}

const reportRows: fieldReports.CommandFieldReport[] = [
  makeReport({
    primaryKey: "b0000000-0000-4000-8000-000000000011",
    title: "Published report",
    isPublished: true,
    timeStampPublished: "2026-01-03T10:00:00Z",
  }),
  makeReport({
    primaryKey: "b0000000-0000-4000-8000-000000000012",
    title: "Scheduled report",
    isPublished: true,
    timeStampPublished: "2999-01-01T10:00:00Z",
  }),
  makeReport({ primaryKey: "b0000000-0000-4000-8000-000000000013", title: "Draft report" }),
  makeReport({
    primaryKey: "b0000000-0000-4000-8000-000000000014",
    title: "Deleted report",
    isDeleted: true,
  }),
];

const reportsSnapshot: fieldReports.CommandFieldReportsSnapshot = {
  fieldReportCount: 4,
  publishedFieldReportCount: 2,
  draftFieldReportCount: 1,
  deletedFieldReportCount: 1,
  reports: reportRows,
};

function renderRoute(path: string, pattern: string, element: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: pattern, element },
      { path: "/elsewhere", element: <p>Elsewhere</p> },
    ],
    { initialEntries: [path] },
  );
  const view = render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
  return { ...view, router };
}

/** Lists every native button of the container that does not carry the shared `button` class. */
function unsharedButtons(container: HTMLElement): HTMLElement[] {
  return Array.from(container.querySelectorAll<HTMLElement>("button")).filter(
    (button) => !button.classList.contains("button"),
  );
}

describe("media administration", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    authState.user = administrator;
    vi.spyOn(siteImages, "loadMediaAssetPage").mockResolvedValue([briefing, terrainMap]);
    vi.spyOn(siteImages, "loadMediaAssets").mockResolvedValue([briefing, terrainMap]);
    vi.spyOn(siteImages, "loadMediaAsset").mockResolvedValue(null);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.user = undefined;
    await act(async () => i18n.changeLanguage("en"));
  });

  describe("gallery page", () => {
    const renderGallery = () =>
      renderRoute("/command/content/gallery", "/command/content/gallery", <MediaGalleryPage />);

    it("shows the navigation label as its one heading in English and German", async () => {
      renderGallery();
      expect(await screen.findByRole("heading", { level: 1, name: "Gallery" })).toBeVisible();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);

      await act(async () => i18n.changeLanguage("de"));
      expect(await screen.findByRole("heading", { level: 1, name: "Galerie" })).toBeVisible();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
    });

    it("renders one heading above the forbidden state and loads nothing", () => {
      authState.user = outsider;
      renderGallery();

      const heading = screen.getByRole("heading", { level: 1, name: "Gallery" });
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      const forbidden = screen.getByRole("status");
      expect(forbidden).toHaveTextContent("Gallery access required");
      expect(
        heading.compareDocumentPosition(forbidden) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
      expect(siteImages.loadMediaAssetPage).not.toHaveBeenCalled();
    });

    it("renders the loading state under the heading while images load", () => {
      vi.spyOn(siteImages, "loadMediaAssetPage").mockReturnValue(new Promise(() => undefined));
      renderGallery();

      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByRole("status")).toHaveTextContent("Loading gallery images");
    });

    it("renders every button as a shared control with an accessible name", async () => {
      const { container } = renderGallery();
      await screen.findByRole("option", { name: /briefing\.png/ });

      expect(unsharedButtons(container)).toEqual([]);
      for (const button of container.querySelectorAll("button"))
        expect(button).toHaveAccessibleName();
      expect(screen.getByRole("button", { name: "Drop or choose an image" })).toBeVisible();
    });

    it("keeps the image options selectable and shows the detail of the selection", async () => {
      const user = userEvent.setup();
      renderGallery();

      const first = await screen.findByRole("option", { name: /briefing\.png/ });
      expect(first).toHaveAttribute("aria-selected", "true");
      await user.click(screen.getByRole("option", { name: /terrain\.png/ }));

      expect(screen.getByRole("option", { name: /terrain\.png/ })).toHaveAttribute(
        "aria-selected",
        "true",
      );
      expect(screen.getByRole("heading", { level: 2, name: "terrain.png" })).toBeVisible();
      expect(screen.getByRole("link", { name: "Open usage" }).getAttribute("href")).toBe(
        "/command/terrains/t-1",
      );
    });

    it("shows the empty state with the next step when no image matches", async () => {
      vi.spyOn(siteImages, "loadMediaAssetPage").mockResolvedValue([]);
      renderGallery();

      expect(await screen.findByText("No gallery images match")).toBeVisible();
      expect(screen.getByText("Clear the filters to see all images.")).toBeVisible();
    });

    it("offers a real retry when loading fails", async () => {
      const user = userEvent.setup();
      vi.spyOn(siteImages, "loadMediaAssetPage")
        .mockRejectedValueOnce(new Error("offline"))
        .mockResolvedValue([briefing]);
      renderGallery();

      const alert = await screen.findByRole("alert");
      expect(alert).toHaveTextContent("Gallery images could not be loaded: offline");
      await user.click(within(alert).getByRole("button", { name: "Try again" }));

      expect(await screen.findByRole("option", { name: /briefing\.png/ })).toBeVisible();
      expect(siteImages.loadMediaAssetPage).toHaveBeenCalledTimes(2);
    });

    it("filters by usage domain with pressed toggle buttons", async () => {
      const user = userEvent.setup();
      renderGallery();
      await screen.findByRole("option", { name: /briefing\.png/ });

      const unused = screen.getByRole("button", { name: "Unused" });
      expect(unused).toHaveAttribute("aria-pressed", "true");
      await user.click(unused);

      expect(unused).toHaveAttribute("aria-pressed", "false");
      expect(screen.queryByRole("option", { name: /briefing\.png/ })).not.toBeInTheDocument();
      expect(screen.getByRole("option", { name: /terrain\.png/ })).toBeVisible();
    });

    it("guards unsaved tag edits with a leave dialog instead of a native prompt", async () => {
      const user = userEvent.setup();
      const confirm = vi.spyOn(window, "confirm");
      const { router } = renderGallery();
      await screen.findByRole("option", { name: /briefing\.png/ });

      await user.type(screen.getByRole("textbox", { name: "Replace tags" }), ", extra");
      expect(screen.getByRole("button", { name: "Save tags" })).toBeEnabled();
      await act(async () => {
        await router.navigate("/elsewhere");
      });

      const dialog = await screen.findByRole("dialog", { name: "Leave without saving?" });
      expect(router.state.location.pathname).toBe("/command/content/gallery");
      await user.click(within(dialog).getByRole("button", { name: "Keep editing" }));
      await waitFor(() => expect(router.state.location.pathname).toBe("/command/content/gallery"));
      expect(confirm).not.toHaveBeenCalled();
    });
  });

  describe("site images page", () => {
    const renderSiteImages = () =>
      renderRoute(
        "/command/content/images/home",
        "/command/content/images/:group",
        <SiteImagesPage />,
      );

    beforeEach(() => {
      vi.spyOn(siteImages, "loadSiteImageSettings").mockResolvedValue([heroSetting]);
    });

    it("keeps its dynamic title with the public images label as the recorded kicker exception", async () => {
      renderSiteImages();

      const heading = await screen.findByRole("heading", { level: 1, name: "Home images" });
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(heading.closest("header")).toHaveTextContent("Public images");
      expect(await screen.findByText("Public imagery")).toBeVisible();
    });

    it("renders one heading above the forbidden state and loads nothing", () => {
      authState.user = outsider;
      renderSiteImages();

      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Public images");
      expect(screen.getByRole("status")).toHaveTextContent("Access required");
      expect(siteImages.loadSiteImageSettings).not.toHaveBeenCalled();
    });

    it("renders shared controls only and marks the active group as the current page", async () => {
      const { container } = renderSiteImages();
      await screen.findByText("Home hero", { selector: "h3" });

      expect(unsharedButtons(container)).toEqual([]);
      expect(container.querySelectorAll("a:not(.button)").length).toBe(0);
      const groups = screen.getByRole("navigation", { name: "Public image groups" });
      expect(within(groups).getByRole("link", { name: "Home" })).toHaveAttribute(
        "aria-current",
        "page",
      );
      expect(within(groups).getByRole("link", { name: "Media" })).not.toHaveAttribute(
        "aria-current",
      );
    });

    it("assigns a picked image only after Save and sends the chosen asset", async () => {
      const user = userEvent.setup();
      vi.spyOn(siteImages, "updateSiteImageSetting").mockResolvedValue({
        ...heroSetting,
        imageAssetFk: briefing.primaryKey,
        imagePath: briefing.imagePath,
      });
      renderSiteImages();

      await user.click(await screen.findByRole("button", { name: "Select image" }));
      const dialog = await screen.findByRole("dialog", { name: "Select gallery image" });
      await user.click(await within(dialog).findByRole("button", { name: /briefing.png/ }));
      await user.click(within(dialog).getByRole("button", { name: "Apply" }));

      expect(await screen.findByText("Some image assignments have not been saved.")).toBeVisible();
      await user.click(screen.getByRole("button", { name: "Save" }));

      await waitFor(() =>
        expect(siteImages.updateSiteImageSetting).toHaveBeenCalledWith(
          "home.hero",
          briefing.primaryKey,
        ),
      );
    });

    it("holds an unsaved assignment behind a leave dialog that names its effect", async () => {
      const user = userEvent.setup();
      const { router } = renderSiteImages();
      await user.click(await screen.findByRole("button", { name: "Select image" }));
      const picker = await screen.findByRole("dialog", { name: "Select gallery image" });
      await user.click(await within(picker).findByRole("button", { name: /briefing.png/ }));
      await user.click(within(picker).getByRole("button", { name: "Apply" }));
      await screen.findByText("Some image assignments have not been saved.");

      await act(async () => {
        await router.navigate("/elsewhere");
      });

      const dialog = await screen.findByRole("dialog", { name: "Leave without saving?" });
      expect(within(dialog).getByRole("button", { name: "Leave without saving" })).toBeVisible();
      expect(within(dialog).getByRole("button", { name: "Keep editing" })).toBeVisible();
      expect(router.state.location.pathname).toBe("/command/content/images/home");
    });

    it("shows the empty state with its next step when a group has no slots", async () => {
      vi.spyOn(siteImages, "loadSiteImageSettings").mockResolvedValue([]);
      renderSiteImages();

      expect(await screen.findByText("No image slots")).toBeVisible();
      expect(screen.getByText("No public image slots are configured for this area.")).toBeVisible();
    });

    it("offers a real retry when loading fails", async () => {
      const user = userEvent.setup();
      vi.spyOn(siteImages, "loadSiteImageSettings")
        .mockRejectedValueOnce(new Error("offline"))
        .mockResolvedValue([heroSetting]);
      renderSiteImages();

      const alert = await screen.findByRole("alert");
      expect(alert).toHaveTextContent("offline");
      await user.click(within(alert).getByRole("button", { name: "Try again" }));

      expect(await screen.findByText("Home hero", { selector: "h3" })).toBeVisible();
      expect(siteImages.loadSiteImageSettings).toHaveBeenCalledTimes(2);
    });
  });

  describe("field report list", () => {
    const renderList = () =>
      renderRoute("/command/field-reports", "/command/field-reports", <FieldReportsPage />);

    beforeEach(() => {
      vi.spyOn(fieldReports, "loadCommandFieldReports").mockResolvedValue(reportsSnapshot);
    });

    it("shows the navigation label as its one heading in English and German", async () => {
      renderList();
      expect(await screen.findByRole("heading", { level: 1, name: "Field reports" })).toBeVisible();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);

      await act(async () => i18n.changeLanguage("de"));
      expect(await screen.findByRole("heading", { level: 1, name: "Feldberichte" })).toBeVisible();
    });

    it("renders one heading above the forbidden state and loads nothing", () => {
      authState.user = outsider;
      renderList();

      const heading = screen.getByRole("heading", { level: 1, name: "Field reports" });
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      const forbidden = screen.getByRole("status");
      expect(
        heading.compareDocumentPosition(forbidden) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
      expect(fieldReports.loadCommandFieldReports).not.toHaveBeenCalled();
    });

    it("takes the state badge tones from the report lexicon", async () => {
      renderList();
      const table = await screen.findByRole("table");

      const badge = (row: string) =>
        within(within(table).getByRole("row", { name: new RegExp(row) })).getAllByText(
          /^(Published|Scheduled|Draft|Deleted)$/,
        )[0];
      expect(badge("Published report")).toHaveClass("status-badge--success");
      expect(badge("Scheduled report")).toHaveClass("status-badge--info");
      expect(badge("Draft report")).toHaveClass("status-badge--neutral");
      expect(badge("Deleted report")).toHaveClass("status-badge--danger");
    });

    it("renders shared controls only and names every row action by its report", async () => {
      const { container } = renderList();
      await screen.findByRole("table");

      expect(unsharedButtons(container)).toEqual([]);
      expect(container.querySelectorAll("a:not(.button)")).toHaveLength(0);
      expect(screen.getByRole("link", { name: "Edit Published report" })).toHaveAttribute(
        "href",
        "/command/field-reports/b0000000-0000-4000-8000-000000000011",
      );
      expect(screen.getByRole("link", { name: "New report" })).toHaveClass("button--gold");
    });

    it("filters the ledger by the summary tiles with pressed state", async () => {
      const user = userEvent.setup();
      renderList();
      await screen.findByRole("table");

      const summary = screen.getByRole("region", { name: "Field report summary" });
      const tile = within(summary).getByRole("button", { name: /^Drafts/ });
      expect(tile).toHaveAttribute("aria-pressed", "false");
      await user.click(tile);

      expect(tile).toHaveAttribute("aria-pressed", "true");
      const table = screen.getByRole("table");
      expect(within(table).getByText("Draft report")).toBeVisible();
      expect(within(table).queryByText("Published report")).not.toBeInTheDocument();
    });

    it("shows the empty state with its next step and a real retry on failure", async () => {
      const user = userEvent.setup();
      vi.spyOn(fieldReports, "loadCommandFieldReports")
        .mockRejectedValueOnce(new Error("offline"))
        .mockResolvedValue({ ...reportsSnapshot, reports: [] });
      renderList();

      const alert = await screen.findByRole("alert");
      expect(alert).toHaveTextContent("Could not load field reports: offline");
      await user.click(within(alert).getByRole("button", { name: "Try again" }));

      expect(await screen.findByText("No field reports match")).toBeVisible();
      expect(
        screen.getByText("Clear the search or filters, or create a field report."),
      ).toBeVisible();
      expect(fieldReports.loadCommandFieldReports).toHaveBeenCalledTimes(2);
    });
  });

  describe("field report editor", () => {
    const renderCreate = () =>
      renderRoute(
        "/command/field-reports/create",
        "/command/field-reports/create",
        <FieldReportEditorPage />,
      );
    const renderEdit = () =>
      renderRoute(
        `/command/field-reports/${reportId}`,
        "/command/field-reports/:reportId",
        <FieldReportEditorPage />,
      );

    beforeEach(() => {
      vi.spyOn(fieldReports, "loadCommandFieldReport").mockResolvedValue(makeReport({}));
    });

    it("renders one heading, labelled fields and no raw button or unnamed control", async () => {
      const { container } = renderCreate();

      expect(
        await screen.findByRole("heading", { level: 1, name: "Create field report" }),
      ).toBeVisible();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.getByRole("textbox", { name: "Title" })).toBeVisible();
      // The drawn checkbox keeps an invisible native input over its row, so the visible parts are the row and text.
      expect(screen.getByRole("checkbox", { name: "Featured" })).toBeInTheDocument();
      expect(screen.getByText("Featured")).toBeVisible();
      expect(unsharedButtons(container)).toEqual([]);
      expect(container.querySelectorAll("a:not(.button)")).toHaveLength(0);
      for (const button of container.querySelectorAll("button"))
        expect(button).toHaveAccessibleName();
    });

    it("renders one heading above the forbidden state and loads nothing", () => {
      authState.user = outsider;
      renderEdit();

      const heading = screen.getByRole("heading", { level: 1, name: "Field reports" });
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(
        heading.compareDocumentPosition(screen.getByRole("status")) &
          Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy();
      expect(fieldReports.loadCommandFieldReport).not.toHaveBeenCalled();
    });

    it("renders the not-found state under one heading when the report does not exist", async () => {
      vi.spyOn(fieldReports, "loadCommandFieldReport").mockResolvedValue(undefined);
      renderEdit();

      expect(await screen.findByText("Field report was not found.")).toBeVisible();
      expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
      expect(screen.queryByRole("button", { name: "Save" })).not.toBeInTheDocument();
    });

    it("offers a real retry when the report fails to load", async () => {
      const user = userEvent.setup();
      vi.spyOn(fieldReports, "loadCommandFieldReport")
        .mockRejectedValueOnce(new Error("offline"))
        .mockResolvedValue(makeReport({}));
      renderEdit();

      const alert = await screen.findByRole("alert");
      expect(alert).toHaveTextContent("Field report could not be loaded: offline");
      await user.click(within(alert).getByRole("button", { name: "Try again" }));

      expect(await screen.findByRole("textbox", { name: "Title" })).toHaveValue("Operation Dawn");
      expect(fieldReports.loadCommandFieldReport).toHaveBeenCalledTimes(2);
    });

    it("takes the status badge tone from the report lexicon", async () => {
      renderEdit();
      const header = (
        await screen.findByRole("heading", { level: 2, name: "Operation Dawn" })
      ).closest("header")!;

      expect(within(header).getByText("Draft")).toHaveClass("status-badge--neutral");
    });

    it("confirms a deletion in a dialog that names the kept record and focuses the dismiss action", async () => {
      const user = userEvent.setup();
      vi.spyOn(fieldReports, "runFieldReportAction").mockResolvedValue(
        makeReport({ isDeleted: true }),
      );
      renderEdit();
      await screen.findByRole("textbox", { name: "Title" });

      await user.click(screen.getByRole("button", { name: "Delete" }));

      const dialog = await screen.findByRole("dialog", { name: "Delete field report" });
      expect(within(dialog).getByRole("button", { name: "Keep field report" })).toHaveFocus();
      await user.click(within(dialog).getByRole("button", { name: "Delete" }));
      await waitFor(() =>
        expect(fieldReports.runFieldReportAction).toHaveBeenCalledWith(reportId, "delete"),
      );
    });

    it("holds unsaved edits behind a leave dialog instead of a native prompt", async () => {
      const user = userEvent.setup();
      const confirm = vi.spyOn(window, "confirm");
      const { router } = renderEdit();
      await user.type(await screen.findByRole("textbox", { name: "Title" }), " II");

      await act(async () => {
        await router.navigate("/elsewhere");
      });

      const dialog = await screen.findByRole("dialog", { name: "Leave without saving?" });
      expect(within(dialog).getByRole("button", { name: "Keep editing" })).toHaveFocus();
      expect(router.state.location.pathname).toBe(`/command/field-reports/${reportId}`);
      expect(confirm).not.toHaveBeenCalled();
    });
  });

  describe("gallery asset picker", () => {
    function renderPicker(overrides: Partial<Parameters<typeof GalleryAssetPicker>[0]> = {}) {
      const onSelect = vi.fn();
      const onCancel = vi.fn();
      const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <I18nextProvider i18n={i18n}>
          <QueryClientProvider client={queryClient}>
            <GalleryAssetPicker
              open
              selectedAssetId=""
              onSelect={onSelect}
              onCancel={onCancel}
              {...overrides}
            />
          </QueryClientProvider>
        </I18nextProvider>,
      );
      return { onSelect, onCancel, queryClient };
    }

    it("opens as a dialog on the shared shell with an outcome-specific dismiss label", async () => {
      renderPicker();

      const dialog = await screen.findByRole("dialog", { name: "Select gallery image" });
      expect(dialog).toHaveClass("dialog", "dialog--gallery-picker");
      expect(within(dialog).getByRole("button", { name: "Don't choose an image" })).toBeVisible();
      expect(within(dialog).queryByRole("button", { name: "Cancel" })).not.toBeInTheDocument();
      expect(dialog.querySelectorAll("form")).toHaveLength(1);
    });

    it("forwards Escape to onCancel", async () => {
      const { onCancel } = renderPicker();
      const dialog = await screen.findByRole("dialog", { name: "Select gallery image" });

      const cancelEvent = new Event("cancel", { cancelable: true });
      fireEvent(dialog, cancelEvent);

      expect(cancelEvent.defaultPrevented).toBe(true);
      expect(onCancel).toHaveBeenCalledOnce();
    });

    it("keeps Apply unavailable until an image is chosen, then reports the selection", async () => {
      const user = userEvent.setup();
      const { onSelect } = renderPicker();
      const dialog = await screen.findByRole("dialog", { name: "Select gallery image" });

      const apply = within(dialog).getByRole("button", { name: "Apply" });
      expect(apply).toHaveAttribute("aria-disabled", "true");
      await user.click(apply);
      expect(onSelect).not.toHaveBeenCalled();

      await user.click(await within(dialog).findByRole("button", { name: /terrain\.png/ }));
      await user.click(within(dialog).getByRole("button", { name: "Apply" }));

      expect(onSelect).toHaveBeenCalledWith(terrainMap);
    });

    it("keeps the picker usable when the selected image no longer exists", async () => {
      const user = userEvent.setup();
      const consoleError = vi.spyOn(console, "error").mockImplementation(() => undefined);
      const { onSelect, queryClient } = renderPicker({ selectedAssetId: "deleted-asset" });
      const dialog = await screen.findByRole("dialog", { name: "Select gallery image" });
      await within(dialog).findByRole("button", { name: /terrain\.png/ });

      await waitFor(() => {
        const state = queryClient.getQueryState(["media-assets", "detail", "deleted-asset"]);
        expect(state?.status).toBe("success");
        expect(state?.data).toBeNull();
      });
      expect(siteImages.loadMediaAsset).toHaveBeenCalledWith("deleted-asset", expect.anything());
      expect(
        consoleError.mock.calls.filter((call) => String(call[0]).includes("undefined")),
      ).toEqual([]);
      const apply = within(dialog).getByRole("button", { name: "Apply" });
      expect(apply).toHaveAttribute("aria-disabled", "true");

      await user.click(within(dialog).getByRole("button", { name: /terrain\.png/ }));
      await user.click(within(dialog).getByRole("button", { name: "Apply" }));

      expect(onSelect).toHaveBeenCalledWith(terrainMap);
      consoleError.mockRestore();
    });

    it("runs the search on Enter instead of confirming the selection", async () => {
      const user = userEvent.setup();
      const { onSelect } = renderPicker({ selectedAssetId: briefing.primaryKey });
      const dialog = await screen.findByRole("dialog", { name: "Select gallery image" });
      await within(dialog).findByRole("button", { name: /briefing\.png/ });

      await user.type(
        within(dialog).getByRole("searchbox", { name: "Search gallery" }),
        "map{Enter}",
      );

      await waitFor(() =>
        expect(siteImages.loadMediaAssets).toHaveBeenLastCalledWith("map", 0, expect.anything()),
      );
      expect(onSelect).not.toHaveBeenCalled();
    });

    it("shows the empty state with a next step and a retry on a load failure", async () => {
      const user = userEvent.setup();
      vi.spyOn(siteImages, "loadMediaAssets")
        .mockRejectedValueOnce(new Error("offline"))
        .mockResolvedValue([]);
      renderPicker();
      const dialog = await screen.findByRole("dialog", { name: "Select gallery image" });

      await user.click(await within(dialog).findByRole("button", { name: "Try again" }));

      expect(await within(dialog).findByText("No images match")).toBeVisible();
      expect(within(dialog).getByText("Try a different search term.")).toBeVisible();
    });

    it("renders no raw button and keeps its file chooser behind a shared button", async () => {
      renderPicker();
      const dialog = await screen.findByRole("dialog", { name: "Select gallery image" });
      await within(dialog).findByRole("button", { name: /briefing\.png/ });

      expect(unsharedButtons(dialog)).toEqual([]);
      expect(within(dialog).getByRole("button", { name: "Upload image" })).toBeVisible();
    });
  });
});
