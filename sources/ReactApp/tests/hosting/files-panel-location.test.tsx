import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Link } from "react-router";
import { beforeEach } from "vitest";
import { ApiError } from "@/api/client";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerFilesPanel } from "@/features/hosting/ServerFilesPanel";
import { hostingService } from "@/features/hosting/services";
import type { GameServer, HostingFile, PreservationSelection } from "@/features/hosting/types";
import { renderWithProviders } from "../helpers/renderWithProviders";

const serverId = "10000000-0000-4000-8000-0000000000aa";
const server = { primaryKey: serverId, canModify: true } as unknown as GameServer;
const workspace = `/command/servers/${serverId}`;

function entry(path: string, isDirectory: boolean): HostingFile {
  return {
    path,
    name: path.split("/").at(-1)!,
    isDirectory,
    size: isDirectory ? null : 12,
    modifiedAt: "2026-08-13T09:00:00Z",
    context: "Persistent",
    classification: "Unknown",
    canModify: true,
    origin: "Persistent",
    modificationMode: "RawFile",
    isGeneratedTarget: false,
    overridesGenerated: false,
    configurationStatus: "NotApplicable",
  } as HostingFile;
}

/** Listings by folder path: the top level holds profiles, profiles holds child and server.cfg. */
const listings: Record<string, HostingFile[]> = {
  "": [entry("profiles", true), entry("readme.txt", false)],
  profiles: [entry("profiles/child", true), entry("profiles/server.cfg", false)],
  "profiles/child": [entry("profiles/child/deep.txt", false)],
};

function mockFiles() {
  return vi
    .spyOn(hostingService, "files")
    .mockImplementation(async (_server, _context, path = "") => listings[path] ?? []);
}

function render(search: string, preservation: PreservationSelection[] = []) {
  vi.spyOn(hostingService, "preservationSelections").mockResolvedValue(preservation);
  vi.spyOn(hostingService, "fileDownloadUrl").mockReturnValue("http://localhost/download");
  return renderWithProviders(
    <>
      <Link to={`${workspace}?tab=settings`}>Go to settings</Link>
      <ServerFilesPanel server={server} />
    </>,
    { route: `${workspace}${search}` },
  );
}

const notFound = () => new ApiError("Not found upstream", 404, "not-found");

function locationOf(router: { state: { location: { pathname: string; search: string } } }) {
  return `${router.state.location.pathname}${router.state.location.search}`;
}

function breadcrumbs(): HTMLElement {
  return screen.getByRole("navigation", { name: "Folder path" });
}

describe("ServerFilesPanel location", () => {
  beforeEach(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });

  it("reads the folder from ?path= and lists it first", async () => {
    const files = mockFiles();
    render("?tab=files&path=profiles");

    expect(await screen.findByText("server.cfg")).toBeVisible();
    expect(files.mock.calls[0]?.[1]).toBe("persistent");
    expect(files.mock.calls[0]?.[2]).toBe("profiles");
    const bar = within(breadcrumbs());
    expect(bar.getByRole("link", { name: "Persistent" })).toHaveAttribute(
      "href",
      `${workspace}?tab=files`,
    );
    const current = bar.getByText("profiles");
    expect(current).toHaveAttribute("aria-current", "location");
    expect(bar.queryByRole("link", { name: "profiles" })).not.toBeInTheDocument();
  });

  it("pushes one history entry per folder link and restores the folder on Back", async () => {
    const user = userEvent.setup();
    const files = mockFiles();
    const { router } = render("?tab=files&path=profiles");

    const table = await screen.findByRole("table");
    const childLink = within(table).getByRole("link", { name: "child" });
    expect(childLink).toHaveAttribute("href", `${workspace}?tab=files&path=profiles%2Fchild`);
    await user.click(childLink);

    expect(locationOf(router)).toBe(`${workspace}?tab=files&path=profiles%2Fchild`);
    expect(await screen.findByText("deep.txt")).toBeVisible();
    expect(files).toHaveBeenCalledWith(serverId, "persistent", "profiles/child", expect.anything());

    await router.navigate(-1);
    expect(locationOf(router)).toBe(`${workspace}?tab=files&path=profiles`);
    expect(await screen.findByText("server.cfg")).toBeVisible();
    expect(screen.queryByText("deep.txt")).not.toBeInTheDocument();
  });

  it("navigates through the breadcrumb and keeps the tree on the same navigation", async () => {
    const user = userEvent.setup();
    mockFiles();
    const { router } = render("?tab=files&path=profiles%2Fchild");

    expect(await screen.findByText("deep.txt")).toBeVisible();
    await user.click(within(breadcrumbs()).getByRole("link", { name: "profiles" }));
    expect(locationOf(router)).toBe(`${workspace}?tab=files&path=profiles`);
    await user.click(within(breadcrumbs()).getByRole("link", { name: "Persistent" }));
    expect(locationOf(router)).toBe(`${workspace}?tab=files`);

    await user.click(await screen.findByRole("treeitem", { name: /profiles/ }));
    expect(locationOf(router)).toBe(`${workspace}?tab=files&path=profiles`);
  });

  it("adds no history entry when the current folder is activated again", async () => {
    const user = userEvent.setup();
    mockFiles();
    const { router } = render("?tab=files&path=profiles");

    const item = await screen.findByRole("treeitem", { name: /profiles/ });
    const before = router.state.location.key;
    await user.click(item);
    expect(router.state.location.key).toBe(before);
    expect(router.state.historyAction).toBe("POP");
  });

  it("switches the context with a push and resets the folder", async () => {
    const user = userEvent.setup();
    mockFiles();
    const { router } = render("?tab=files&path=profiles");

    await screen.findByText("server.cfg");
    await user.click(screen.getByRole("radio", { name: "Live worker files" }));
    expect(locationOf(router)).toBe(`${workspace}?tab=files&area=live`);
    expect(router.state.historyAction).toBe("PUSH");
    await user.click(screen.getByRole("radio", { name: "Persistent expected files" }));
    expect(locationOf(router)).toBe(`${workspace}?tab=files`);
  });

  it("opens a preserved shortcut in the persistent context", async () => {
    const user = userEvent.setup();
    mockFiles();
    const { router } = render("?tab=files&area=live", [
      {
        primaryKey: "p1",
        path: "profiles",
        scope: "RecursiveFolder",
        mode: "CreateOrUpdate",
        seededByGame: false,
      } as unknown as PreservationSelection,
    ]);

    await user.click(await screen.findByRole("button", { name: "profiles" }));
    expect(locationOf(router)).toBe(`${workspace}?tab=files&path=profiles`);
  });

  it("resets search text and expands the ancestors of the new folder", async () => {
    const user = userEvent.setup();
    mockFiles();
    render("?tab=files&path=profiles");

    const search = await screen.findByRole("searchbox", { name: "Search this folder" });
    await user.type(search, "chi");
    expect(search).toHaveValue("chi");
    const table = await screen.findByRole("table");
    expect(within(table).queryByText("server.cfg")).not.toBeInTheDocument();
    await user.click(within(table).getByRole("link", { name: "child" }));
    expect(screen.getByRole("searchbox", { name: "Search this folder" })).toHaveValue("");
    await waitFor(() =>
      expect(screen.getByRole("treeitem", { name: /profiles/ })).toHaveAttribute(
        "aria-expanded",
        "true",
      ),
    );
  });
});

describe("ServerFilesPanel fallback, focus and guard", () => {
  beforeEach(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });

  const invalidNotice = "That folder link is not valid. Showing the top level instead.";
  const missingNotice = "That folder was not found. Showing the top level instead.";

  it("shows the top level and replaces the URL for an invalid ?path=", async () => {
    const files = mockFiles();
    const { router } = render("?tab=files&path=a%2F..%2Fb");

    expect(await screen.findByText(invalidNotice)).toBeVisible();
    await waitFor(() => expect(locationOf(router)).toBe(`${workspace}?tab=files`));
    expect(router.state.historyAction).toBe("REPLACE");
    expect(await screen.findByText("readme.txt")).toBeVisible();
    expect(files.mock.calls.every((call) => call[2] === "")).toBe(true);
  });

  it("does not trap Back after an invalid link was replaced", async () => {
    mockFiles();
    const { router } = render("?tab=files&path=profiles");
    await screen.findByText("server.cfg");

    await act(async () => {
      await router.navigate(`${workspace}?tab=files&path=a%2F..%2Fb`);
    });
    expect(await screen.findByText(invalidNotice)).toBeVisible();
    await waitFor(() => expect(locationOf(router)).toBe(`${workspace}?tab=files`));

    await act(async () => {
      await router.navigate(-1);
    });
    expect(locationOf(router)).toBe(`${workspace}?tab=files&path=profiles`);
    expect(screen.queryByText(invalidNotice)).not.toBeInTheDocument();
  });

  it("falls back to the top level once for a missing folder and keeps Back usable", async () => {
    const files = vi
      .spyOn(hostingService, "files")
      .mockImplementation(async (_server, _context, path = "") => {
        if (path === "missing") throw notFound();
        return listings[path] ?? [];
      });
    const { router } = render("?tab=files&path=profiles");
    await screen.findByText("server.cfg");

    await act(async () => {
      await router.navigate(`${workspace}?tab=files&path=missing`);
    });
    expect(await screen.findByText(missingNotice)).toBeVisible();
    await waitFor(() => expect(locationOf(router)).toBe(`${workspace}?tab=files`));
    expect(await screen.findByText("readme.txt")).toBeVisible();
    expect(files.mock.calls.filter((call) => call[2] === "missing")).toHaveLength(1);

    await act(async () => {
      await router.navigate(-1);
    });
    expect(locationOf(router)).toBe(`${workspace}?tab=files&path=profiles`);
  });

  it("keeps the live area when a live folder is missing", async () => {
    vi.spyOn(hostingService, "files").mockImplementation(async (_server, _context, path = "") => {
      if (path === "x") throw notFound();
      return [];
    });
    const { router } = render("?tab=files&area=live&path=x");

    expect(await screen.findByText(missingNotice)).toBeVisible();
    await waitFor(() => expect(locationOf(router)).toBe(`${workspace}?tab=files&area=live`));
  });

  it("keeps the error panel and the URL for a top-level not-found, with no loop", async () => {
    const files = vi.spyOn(hostingService, "files").mockRejectedValue(notFound());
    const { router } = render("?tab=files&area=live");

    expect(await screen.findByRole("alert")).toHaveTextContent("Not found upstream");
    expect(locationOf(router)).toBe(`${workspace}?tab=files&area=live`);
    expect(router.state.historyAction).toBe("POP");
    expect(screen.queryByText(missingNotice)).not.toBeInTheDocument();
    expect(files.mock.calls.every((call) => call[2] === "")).toBe(true);
  });

  it("keeps the error panel and the URL for a non-404 error on a folder", async () => {
    vi.spyOn(hostingService, "files").mockImplementation(async (_server, _context, path = "") => {
      if (path === "profiles") throw new ApiError("Server broke", 500, "server");
      return listings[path] ?? [];
    });
    const { router } = render("?tab=files&path=profiles");

    expect(await screen.findByRole("alert")).toHaveTextContent("Server broke");
    expect(locationOf(router)).toBe(`${workspace}?tab=files&path=profiles`);
    expect(router.state.historyAction).toBe("POP");
    expect(screen.queryByText(missingNotice)).not.toBeInTheDocument();
  });

  it("removes the notice when it is dismissed", async () => {
    const user = userEvent.setup();
    mockFiles();
    render("?tab=files&path=..");

    expect(await screen.findByText(invalidNotice)).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Dismiss notice" }));
    expect(screen.queryByText(invalidNotice)).not.toBeInTheDocument();
  });

  it("moves focus to the folder heading after a link and announces the folder", async () => {
    const user = userEvent.setup();
    mockFiles();
    const { router } = render("?tab=files");

    expect(await screen.findByText("Showing Top level: 2 items.")).toBeInTheDocument();
    const topHeading = screen.getByRole("heading", {
      level: 3,
      name: "Persistent expected files",
    });
    expect(topHeading).not.toHaveFocus();
    expect(document.activeElement).not.toBe(topHeading);

    await user.click(
      within(await screen.findByRole("table")).getByRole("link", { name: "profiles" }),
    );
    const heading = await screen.findByRole("heading", { level: 3, name: "profiles" });
    await waitFor(() => expect(heading).toHaveFocus());
    expect(heading).toHaveAttribute("tabindex", "-1");
    expect(await screen.findByText("Showing profiles: 2 items.")).toBeInTheDocument();

    const search = screen.getByRole("searchbox", { name: "Search this folder" });
    act(() => search.focus());
    await act(async () => {
      await router.navigate(-1);
    });
    expect(await screen.findByText("Showing Top level: 2 items.")).toBeInTheDocument();
    expect(search).toHaveFocus();
  });

  it("does not let a failed context switch swallow the heading focus of the next navigation", async () => {
    const user = userEvent.setup();
    vi.spyOn(hostingService, "files").mockImplementation(async (_server, context, path = "") => {
      if (context === "live") throw new Error("listing failed");
      return listings[path] ?? [];
    });
    const { router } = render("?tab=files");
    await screen.findByText("Showing Top level: 2 items.");

    await user.click(screen.getByRole("radio", { name: "Live worker files" }));
    await waitFor(() => expect(locationOf(router)).toBe(`${workspace}?tab=files&area=live`));
    // The live listing never loads, so the context focus rule never reached its load step.
    await act(async () => {
      await router.navigate(`${workspace}?tab=files&path=profiles`);
    });

    const heading = await screen.findByRole("heading", { level: 3, name: "profiles" });
    await waitFor(() => expect(heading).toHaveFocus());
  });

  it("states that file changes are immediate", async () => {
    mockFiles();
    render("?tab=files");
    expect(await screen.findByText("Changes to files take effect immediately.")).toBeVisible();
  });

  it("guards leaving Files with a queued upload and never blocks folder navigation", async () => {
    const user = userEvent.setup();
    mockFiles();
    const { router } = render("?tab=files");

    await screen.findByText("readme.txt");
    await user.click(screen.getByRole("button", { name: "Upload" }));
    await user.upload(
      screen.getByLabelText("File (maximum 16 MiB)"),
      new File(["data"], "fresh.txt"),
    );
    expect(await screen.findByText(/fresh\.txt/)).toBeVisible();

    await user.click(
      within(await screen.findByRole("table")).getByRole("link", { name: "profiles" }),
    );
    expect(locationOf(router)).toBe(`${workspace}?tab=files&path=profiles`);
    expect(
      screen.queryByRole("dialog", { name: "Leave without finishing the upload?" }),
    ).toBeNull();

    await user.click(screen.getByRole("link", { name: "Go to settings" }));
    const dialog = await screen.findByRole("dialog", {
      name: "Leave without finishing the upload?",
    });
    expect(within(dialog).getByRole("button", { name: "Stay here" })).toBeVisible();
    expect(within(dialog).getByRole("button", { name: "Leave without uploading" })).toBeVisible();
    expect(within(dialog).queryByRole("button", { name: "Review changes" })).toBeNull();

    await user.click(within(dialog).getByRole("button", { name: "Stay here" }));
    expect(locationOf(router)).toBe(`${workspace}?tab=files&path=profiles`);

    await user.click(screen.getByRole("link", { name: "Go to settings" }));
    await user.click(
      within(
        await screen.findByRole("dialog", { name: "Leave without finishing the upload?" }),
      ).getByRole("button", { name: "Leave without uploading" }),
    );
    await waitFor(() => expect(locationOf(router)).toBe(`${workspace}?tab=settings`));
  });

  it("does not guard leaving Files while nothing is queued", async () => {
    const user = userEvent.setup();
    mockFiles();
    const { router } = render("?tab=files");

    await screen.findByText("readme.txt");
    await user.click(screen.getByRole("link", { name: "Go to settings" }));
    expect(locationOf(router)).toBe(`${workspace}?tab=settings`);
  });
});
