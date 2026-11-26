import { readFileSync } from "node:fs";
import { join } from "node:path";
import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { ServerFilesPanel } from "@/features/hosting/ServerFilesPanel";
import { hostingService } from "@/features/hosting/services";
import type { GameServer, HostingFile } from "@/features/hosting/types";
import { renderWithProviders } from "../helpers/renderWithProviders";

const serverId = "10000000-0000-4000-8000-0000000000bb";
const server = { primaryKey: serverId, canModify: true } as unknown as GameServer;
const workspace = `/command/servers/${serverId}`;
const downloadUrl =
  "http://localhost/api/game-servers/server/files/persistent/content?path=notes.txt";

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

function mockApi(listing: HostingFile[] = [entry("profiles", true), entry("notes.txt", false)]) {
  vi.spyOn(hostingService, "files").mockImplementation(async (_server, _context, path = "") =>
    path === "" ? listing : [],
  );
  vi.spyOn(hostingService, "preservationSelections").mockResolvedValue([]);
  vi.spyOn(hostingService, "fileDownloadUrl").mockReturnValue(downloadUrl);
  return {
    deleteFile: vi.spyOn(hostingService, "deleteFile").mockResolvedValue(undefined),
    createDirectory: vi
      .spyOn(hostingService, "createDirectory")
      .mockResolvedValue(undefined as never),
    uploadFile: vi.spyOn(hostingService, "uploadFile").mockResolvedValue({} as never),
  };
}

function render(search = "?tab=files") {
  return renderWithProviders(<ServerFilesPanel server={server} />, {
    route: `${workspace}${search}`,
  });
}

async function rowOf(name: string): Promise<HTMLElement> {
  const table = await screen.findByRole("table");
  return within(table).getByText(name).closest("tr")!;
}

describe("ServerFilesPanel inline row actions", () => {
  beforeEach(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });

  it("shows the actions of a row inline with the file name in their accessible names", async () => {
    mockApi();
    render();

    const fileRow = within(await rowOf("notes.txt"));
    const download = fileRow.getByRole("link", { name: "Download notes.txt" });
    expect(download).toHaveAttribute("href", downloadUrl);
    expect(download).toHaveAttribute("download", "notes.txt");
    expect(download).toHaveTextContent("Download");
    expect(fileRow.getByRole("button", { name: "Delete notes.txt" })).toHaveTextContent("Delete");

    const folderRow = within(await rowOf("profiles"));
    expect(folderRow.getByRole("button", { name: "Open profiles" })).toHaveTextContent("Open");
    expect(folderRow.getByRole("button", { name: "Delete profiles" })).toBeVisible();
  });

  it("has no popup menu, menu item or disclosure in the panel", async () => {
    mockApi();
    const { container } = render();

    await rowOf("notes.txt");
    expect(screen.queryByRole("menu")).toBeNull();
    expect(screen.queryByRole("menuitem")).toBeNull();
    expect(container.querySelector("details, summary")).toBeNull();
  });

  it("opens a folder from its inline Open action", async () => {
    mockApi();
    const { router } = render();

    await user().click(
      within(await rowOf("profiles")).getByRole("button", { name: "Open profiles" }),
    );
    expect(`${router.state.location.pathname}${router.state.location.search}`).toBe(
      `${workspace}?tab=files&path=profiles`,
    );
  });

  it("asks before deleting, focuses the dismiss control and returns focus to Delete on Escape", async () => {
    const api = mockApi();
    render();

    const deleteButton = within(await rowOf("notes.txt")).getByRole("button", {
      name: "Delete notes.txt",
    });
    await user().click(deleteButton);

    const dialog = await screen.findByRole("dialog", { name: "Delete file" });
    expect(within(dialog).getByRole("button", { name: "Keep file" })).toHaveFocus();
    expect(api.deleteFile).not.toHaveBeenCalled();

    // jsdom does not turn Escape into a cancel event, so the event a browser raises for Escape is dispatched.
    fireEvent(dialog, new Event("cancel", { cancelable: true }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(api.deleteFile).not.toHaveBeenCalled();
    expect(deleteButton).toHaveFocus();
  });

  it("offers no Delete action to a member without file rights and keeps Download", async () => {
    mockApi();
    renderWithProviders(
      <ServerFilesPanel server={{ ...server, canModify: false } as unknown as GameServer} />,
      { route: `${workspace}?tab=files` },
    );

    const fileRow = within(await rowOf("notes.txt"));
    expect(fileRow.getByRole("link", { name: "Download notes.txt" })).toBeVisible();
    expect(screen.queryByRole("button", { name: /^Delete / })).toBeNull();
  });

  it("deletes only after the confirmation", async () => {
    const api = mockApi();
    render();

    await user().click(
      within(await rowOf("notes.txt")).getByRole("button", { name: "Delete notes.txt" }),
    );
    const dialog = await screen.findByRole("dialog", { name: "Delete file" });
    await user().click(within(dialog).getByRole("button", { name: "Delete" }));
    await waitFor(() =>
      expect(api.deleteFile).toHaveBeenCalledWith(serverId, "persistent", "notes.txt", false),
    );
  });
});

/** Dispatches the cancel event a browser raises for Escape, which jsdom does not do itself. */
function pressEscape(dialog: HTMLElement): boolean {
  const event = new Event("cancel", { cancelable: true });
  fireEvent(dialog, event);
  return event.defaultPrevented;
}

describe("ServerFilesPanel dialogs on the shared shell", () => {
  beforeEach(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });

  it("names what dismissing each dialog does instead of the generic cancel label", async () => {
    mockApi();
    render();
    await rowOf("notes.txt");

    await user().click(screen.getByRole("button", { name: "Create folder" }));
    let dialog = await screen.findByRole("dialog", { name: "New directory" });
    expect(within(dialog).getByRole("button", { name: "Don't create folder" })).toBeVisible();
    expect(within(dialog).queryByRole("button", { name: "Cancel" })).toBeNull();
    pressEscape(dialog);
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    await user().click(screen.getByRole("button", { name: "Upload" }));
    dialog = await screen.findByRole("dialog", { name: "Upload or replace file" });
    expect(within(dialog).getByRole("button", { name: "Don't upload files" })).toBeVisible();
    expect(within(dialog).queryByRole("button", { name: "Cancel" })).toBeNull();
  });

  it("validates the folder name through the field and keeps create blocked until it is valid", async () => {
    const api = mockApi();
    render();
    await rowOf("notes.txt");

    await user().click(screen.getByRole("button", { name: "Create folder" }));
    const dialog = await screen.findByRole("dialog", { name: "New directory" });
    const name = within(dialog).getByRole("textbox", { name: "Folder name" });
    await user().type(name, "a/b");

    expect(name).toBeInvalid();
    expect(within(dialog).getByRole("alert")).toHaveTextContent(
      "Enter one folder name without slashes, “.”, or “..”.",
    );
    await user().click(within(dialog).getByRole("button", { name: "Create folder" }));
    expect(api.createDirectory).not.toHaveBeenCalled();

    await user().clear(name);
    await user().type(name, "missions");
    await user().click(within(dialog).getByRole("button", { name: "Create folder" }));
    await waitFor(() =>
      expect(api.createDirectory).toHaveBeenCalledWith(serverId, "persistent", "missions"),
    );
  });

  it("confirms the new folder dialog with the one gold action and keeps Delete danger", async () => {
    mockApi();
    const { container } = render();
    await rowOf("notes.txt");

    await user().click(screen.getByRole("button", { name: "Create folder" }));
    const folderDialog = await screen.findByRole("dialog", { name: "New directory" });
    expect(within(folderDialog).getByRole("button", { name: "Create folder" })).toHaveClass(
      "button--gold",
    );
    expect(folderDialog.querySelectorAll(".button--gold")).toHaveLength(1);
    pressEscape(folderDialog);
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    await user().click(
      within(await rowOf("notes.txt")).getByRole("button", { name: "Delete notes.txt" }),
    );
    const deleteDialog = await screen.findByRole("dialog", { name: "Delete file" });
    expect(within(deleteDialog).getByRole("button", { name: "Delete" })).toHaveClass(
      "button--danger",
    );
    expect(deleteDialog.querySelectorAll(".button--gold")).toHaveLength(0);

    // The inline preservation Add shares its view with the gold Upload, so it stays neutral: one gold per view.
    expect(container.querySelector("form .button[type=submit]")).not.toHaveClass("button--gold");
  });

  it("returns focus to the invoker when a dialog closes and ignores Escape while creating", async () => {
    const api = mockApi();
    let finish: () => void = () => undefined;
    api.createDirectory.mockImplementation(
      () => new Promise<never>((resolve) => (finish = () => resolve(undefined as never))),
    );
    render();
    await rowOf("notes.txt");

    const invoker = screen.getByRole("button", { name: "Create folder" });
    await user().click(invoker);
    let dialog = await screen.findByRole("dialog", { name: "New directory" });
    pressEscape(dialog);
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() => expect(invoker).toHaveFocus());

    await user().click(invoker);
    dialog = await screen.findByRole("dialog", { name: "New directory" });
    await user().type(within(dialog).getByRole("textbox", { name: "Folder name" }), "missions");
    await user().click(within(dialog).getByRole("button", { name: "Create folder" }));
    await waitFor(() => expect(api.createDirectory).toHaveBeenCalledTimes(1));
    expect(within(dialog).getByRole("button", { name: "Don't create folder" })).toBeDisabled();
    expect(pressEscape(dialog)).toBe(true);
    expect(screen.getByRole("dialog", { name: "New directory" })).toBeVisible();
    finish();
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it("keeps the replacement approval reachable by keyboard and blocks upload until it is given", async () => {
    const api = mockApi([entry("server.cfg", false)]);
    render();
    await rowOf("server.cfg");

    const invoker = screen.getByRole("button", { name: "Upload" });
    await user().click(invoker);
    const dialog = await screen.findByRole("dialog", { name: "Upload or replace file" });
    await user().upload(
      within(dialog).getByLabelText("File (maximum 16 MiB)"),
      new File(["hostname=test"], "server.cfg"),
    );
    const approval = await within(dialog).findByRole("checkbox", {
      name: "Replace 1 existing file(s)",
    });
    const confirm = within(dialog).getByRole("button", { name: "Upload" });
    expect(confirm).toHaveAttribute("aria-disabled", "true");
    await user().click(confirm);
    expect(api.uploadFile).not.toHaveBeenCalled();

    approval.focus();
    await user().keyboard(" ");
    expect(approval).toBeChecked();
    await user().click(confirm);
    await waitFor(() =>
      expect(api.uploadFile).toHaveBeenCalledWith(
        serverId,
        "persistent",
        "server.cfg",
        expect.any(File),
        false,
        expect.any(AbortSignal),
      ),
    );

    await waitFor(() =>
      expect(within(dialog).getByRole("button", { name: "Don't upload files" })).toBeEnabled(),
    );
    pressEscape(dialog);
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() => expect(invoker).toHaveFocus());
  });

  it("lets the user cancel a running upload from the dialog body while dismissal is disabled", async () => {
    const api = mockApi();
    let release: () => void = () => undefined;
    api.uploadFile.mockImplementation(
      (...args: unknown[]) =>
        new Promise((resolve, reject) => {
          const signal = args[5] as AbortSignal;
          release = () => resolve({} as never);
          signal.addEventListener("abort", () => reject(new Error("aborted")));
        }),
    );
    render();
    await rowOf("notes.txt");

    await user().click(screen.getByRole("button", { name: "Upload" }));
    const dialog = await screen.findByRole("dialog", { name: "Upload or replace file" });
    await user().upload(
      within(dialog).getByLabelText("File (maximum 16 MiB)"),
      new File(["data"], "fresh.txt"),
    );
    await within(dialog).findByText(/fresh\.txt/);
    await user().click(within(dialog).getByRole("button", { name: "Upload" }));

    const abort = await within(dialog).findByRole("button", { name: "Cancel uploads" });
    expect(within(dialog).getByRole("button", { name: "Don't upload files" })).toBeDisabled();
    expect(pressEscape(dialog)).toBe(true);
    expect(screen.getByRole("dialog", { name: "Upload or replace file" })).toBeVisible();

    await user().click(abort);
    await waitFor(() =>
      expect(within(dialog).getByRole("button", { name: "Don't upload files" })).toBeEnabled(),
    );
    release();
  });
});

describe("ServerFilesPanel shared controls", () => {
  beforeEach(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });

  it("filters the listing through the shared search field and clears it from the keyboard", async () => {
    mockApi();
    render();
    await rowOf("notes.txt");

    const search = screen.getByRole("searchbox", { name: "Search this folder" });
    await user().type(search, "zzz");
    expect(screen.getByText("No files match")).toBeVisible();
    expect(screen.getByText("Clear the search to see everything in this folder.")).toBeVisible();

    await user().keyboard("{Escape}");
    expect(search).toHaveValue("");
    expect(search).toHaveFocus();
    expect(await rowOf("notes.txt")).toBeVisible();

    await user().type(search, "prof");
    await user().click(screen.getByRole("button", { name: "Clear search" }));
    expect(search).toHaveValue("");
  });

  it("explains an empty folder with a title, the next step and an upload action", async () => {
    mockApi([]);
    render();

    expect(await screen.findByText("This folder is empty")).toBeVisible();
    expect(screen.getByText("Upload files to add them to this folder.")).toBeVisible();
    expect(screen.getAllByRole("button", { name: "Upload" })).toHaveLength(2);
  });

  it("labels the preservation fields and keeps the selection form working", async () => {
    mockApi();
    render();
    await rowOf("notes.txt");

    expect(screen.getByRole("textbox", { name: "Server-relative path" })).toBeRequired();
    expect(screen.getByRole("combobox", { name: "Selection scope" })).toHaveValue("File");
    expect(screen.getByRole("combobox", { name: "Deletion policy" })).toHaveValue("CreateOrUpdate");
    expect(screen.getByRole("button", { name: "Add selection" })).toBeDisabled();
  });

  it("shows the folder tree states outside the tree role", async () => {
    mockApi([entry("notes.txt", false)]);
    const { container } = render();
    await rowOf("notes.txt");

    expect(screen.getByText("No folders")).toBeVisible();
    expect(container.querySelector('[role="tree"]')).toBeNull();
    expect(container.querySelector("li.next-hosting-tree-state")).toBeNull();
  });

  it("keeps a visible focus ring on the search and removes no outline in the files styles", () => {
    // jsdom does not load stylesheets, so the rule text is asserted: the search is the shared field, whose input
    // draws the focus ring token, and the hosting styles no longer remove an outline.
    const sourceRoot = join(import.meta.dirname, "..", "..", "src");
    const hostingCss = readFileSync(join(sourceRoot, "features", "hosting", "hosting.css"), "utf8");
    const fieldCss = readFileSync(
      join(sourceRoot, "components", "server", "fleet-toolbar.css"),
      "utf8",
    );
    expect(hostingCss).not.toMatch(/outline:\s*(none|0)\b/);
    expect(hostingCss).not.toContain("next-hosting-file-search");
    expect(fieldCss).toMatch(
      /\.search-control__input:focus-visible\s*\{[^}]*outline:\s*var\(--utf-focus-ring\)/,
    );
  });

  it("contains no raw control, disclosure or dialog element in the panel", async () => {
    mockApi();
    const { container } = render();
    await rowOf("notes.txt");

    expect(container.querySelector("details, summary, [role=menu], [role=menuitem]")).toBeNull();
    for (const button of container.querySelectorAll("button")) {
      expect(button.className).toContain("button");
    }
  });
});

describe("ServerFilesPanel upload dialog drop zone", () => {
  beforeEach(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });

  /** Opens the upload dialog from the toolbar and returns it. */
  async function openUploadDialog(row = "notes.txt"): Promise<HTMLElement> {
    await rowOf(row);
    await user().click(
      within(screen.getByRole("toolbar", { name: "File actions" })).getByRole("button", {
        name: "Upload",
      }),
    );
    return screen.findByRole("dialog", { name: "Upload or replace file" });
  }

  it("opens on a drop zone with its title, its line and the two choose buttons", async () => {
    mockApi();
    render();
    const dialog = await openUploadDialog();

    const zone = dialog.querySelector(".file-dropzone")!;
    expect(zone).not.toBeNull();
    expect(within(zone as HTMLElement).getByText("Drop files here")).toBeVisible();
    expect(within(zone as HTMLElement).getByText("Or choose what to upload.")).toBeVisible();
    const choose = within(zone as HTMLElement).getByRole("button", { name: "Choose files" });
    const folder = within(zone as HTMLElement).getByRole("button", { name: "Choose folder" });
    expect(choose).toHaveClass("button--ghost");
    expect(folder).toHaveClass("button--ghost");
    expect(zone.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
    expect(dialog.querySelector(".next-hosting-upload-pickers")).toBeNull();
    // Focus starts on the first choose control, not on the focusable body region.
    await waitFor(() => expect(choose).toHaveFocus());
  });

  it("queues files dropped on the zone like files dropped on the list", async () => {
    mockApi();
    render();
    const dialog = await openUploadDialog();

    const file = new File(["data"], "dropped.txt");
    const zone = dialog.querySelector(".file-dropzone")!;
    fireEvent.drop(zone, {
      dataTransfer: { files: [file], items: [], types: ["Files"] },
    });
    expect(await within(dialog).findByText(/dropped\.txt/)).toBeVisible();
    expect(within(dialog).getByRole("button", { name: "Upload" })).not.toHaveAttribute(
      "aria-disabled",
    );
  });

  it("cancels the browser default for a file dropped beside the zone, so the app is never replaced", async () => {
    mockApi();
    render();
    const dialog = await openUploadDialog();

    const files = { files: [new File(["data"], "stray.txt")], items: [], types: ["Files"] };
    const limit = dialog.querySelector(".file-dropzone__limit")!;
    // fireEvent returns false when a handler cancelled the event.
    expect(fireEvent.dragOver(limit, { dataTransfer: { ...files } })).toBe(false);
    expect(fireEvent.drop(limit, { dataTransfer: { ...files } })).toBe(false);
    expect(fireEvent.drop(dialog, { dataTransfer: { ...files } })).toBe(false);
    // A stray drop queues nothing; only the zone does.
    expect(within(dialog).queryByText(/stray\.txt/)).toBeNull();
    // A drag that carries no files, such as selected text, keeps its default.
    expect(
      fireEvent.dragOver(limit, { dataTransfer: { files: [], items: [], types: ["text/plain"] } }),
    ).toBe(true);
  });

  it("keeps the gold Upload unavailable with a visible reason until a file is queued", async () => {
    const api = mockApi();
    render();
    const dialog = await openUploadDialog();

    const confirm = within(dialog).getByRole("button", { name: "Upload" });
    expect(confirm).toHaveClass("button--gold");
    expect(confirm).toHaveAttribute("aria-disabled", "true");
    expect(confirm).not.toBeDisabled();
    const reason = within(dialog).getByText("Choose files or drop them into the explorer.");
    expect(reason).toBeVisible();
    expect(confirm.getAttribute("aria-describedby")).toContain(reason.id);
    await user().click(confirm);
    expect(api.uploadFile).not.toHaveBeenCalled();

    await user().upload(
      within(dialog).getByLabelText("File (maximum 16 MiB)"),
      new File(["data"], "fresh.txt"),
    );
    await within(dialog).findByText(/fresh\.txt/);
    expect(within(dialog).queryByText("Choose files or drop them into the explorer.")).toBeNull();
    expect(within(dialog).getByRole("button", { name: "Upload" })).not.toHaveAttribute(
      "aria-disabled",
    );
  });

  it("still blocks Upload until a replacement is approved once a file is queued", async () => {
    mockApi([entry("server.cfg", false)]);
    render();
    const dialog = await openUploadDialog("server.cfg");
    await user().upload(
      within(dialog).getByLabelText("File (maximum 16 MiB)"),
      new File(["hostname=test"], "server.cfg"),
    );
    await within(dialog).findByRole("checkbox", { name: "Replace 1 existing file(s)" });
    expect(within(dialog).getByRole("button", { name: "Upload" })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
  });

  it("removes only the chosen queued file through a named icon-only control", async () => {
    mockApi([entry("server.cfg", false)]);
    render();
    const dialog = await openUploadDialog("server.cfg");
    await user().upload(within(dialog).getByLabelText("File (maximum 16 MiB)"), [
      new File(["hostname=test"], "server.cfg"),
      new File(["data"], "fresh.txt"),
    ]);
    await within(dialog).findByRole("checkbox", { name: "Replace 1 existing file(s)" });

    const removeFresh = within(dialog).getByRole("button", {
      name: "Remove fresh.txt from the queue",
    });
    expect(removeFresh).toHaveClass("button--icon-only");
    expect(removeFresh.textContent?.trim()).toBe("");
    await user().click(removeFresh);
    expect(within(dialog).queryByText(/fresh\.txt/)).toBeNull();
    expect(within(dialog).getByText(/server\.cfg/)).toBeVisible();

    // The collision approval follows the remaining queue: removing the colliding file drops it.
    await user().click(
      within(dialog).getByRole("button", { name: "Remove server.cfg from the queue" }),
    );
    expect(within(dialog).queryByRole("checkbox")).toBeNull();
    expect(within(dialog).getByText("Choose files or drop them into the explorer.")).toBeVisible();
  });

  it("moves focus to the next remove control, then to the choose control, when a queued file is removed", async () => {
    mockApi();
    render();
    const dialog = await openUploadDialog();
    await user().upload(within(dialog).getByLabelText("File (maximum 16 MiB)"), [
      new File(["a"], "first.txt"),
      new File(["b"], "second.txt"),
    ]);
    await within(dialog).findByText(/second\.txt/);

    await user().click(
      within(dialog).getByRole("button", { name: "Remove first.txt from the queue" }),
    );
    await waitFor(() =>
      expect(
        within(dialog).getByRole("button", { name: "Remove second.txt from the queue" }),
      ).toHaveFocus(),
    );

    await user().click(
      within(dialog).getByRole("button", { name: "Remove second.txt from the queue" }),
    );
    await waitFor(() =>
      expect(within(dialog).getByRole("button", { name: "Choose files" })).toHaveFocus(),
    );
  });

  it("offers no remove control while a batch runs", async () => {
    const api = mockApi();
    api.uploadFile.mockImplementation(() => new Promise(() => undefined));
    render();
    const dialog = await openUploadDialog();
    await user().upload(
      within(dialog).getByLabelText("File (maximum 16 MiB)"),
      new File(["data"], "fresh.txt"),
    );
    await within(dialog).findByText(/fresh\.txt/);
    expect(
      within(dialog).getByRole("button", { name: "Remove fresh.txt from the queue" }),
    ).toBeVisible();
    await user().click(within(dialog).getByRole("button", { name: "Upload" }));
    await within(dialog).findByRole("button", { name: "Cancel uploads" });
    expect(within(dialog).queryByRole("button", { name: /^Remove / })).toBeNull();
  });

  it("renders each queued file as a compact row with its size, a toned status badge and the Will replace badge", async () => {
    mockApi([entry("server.cfg", false)]);
    render();
    const dialog = await openUploadDialog("server.cfg");
    const oversized = new File(["x"], "huge.bin");
    Object.defineProperty(oversized, "size", { value: 16 * 1024 * 1024 + 1 });
    await user().upload(within(dialog).getByLabelText("File (maximum 16 MiB)"), [
      new File(["hostname=test"], "server.cfg"),
      new File(["data"], "fresh.txt"),
      oversized,
    ]);
    await within(dialog).findByRole("checkbox", { name: "Replace 1 existing file(s)" });

    const list = dialog.querySelector("ul.file-upload-queue")!;
    expect(list).toHaveClass("next-hosting-upload-queue");
    const rows = Array.from(list.querySelectorAll<HTMLElement>("li.file-upload-queue__row"));
    expect(rows).toHaveLength(3);

    const [replace, fresh, huge] = rows as [HTMLElement, HTMLElement, HTMLElement];
    expect(within(replace).getByText("server.cfg").tagName).toBe("STRONG");
    expect(replace.querySelector(".file-upload-queue__size")).toHaveTextContent("13 B");
    const queued = within(replace).getByText("Queued");
    expect(queued).toHaveClass("status-badge", "status-badge--neutral");
    expect(within(replace).getByText("Will replace")).toHaveClass("status-badge--warning");
    expect(
      within(replace).getByRole("button", { name: "Remove server.cfg from the queue" }),
    ).toBeVisible();

    expect(within(fresh).getByText("Queued")).toHaveClass("status-badge--neutral");
    expect(within(fresh).queryByText("Will replace")).toBeNull();

    expect(within(huge).getByText("Rejected")).toHaveClass("status-badge--danger");
    expect(huge.querySelector(".file-upload-queue__error")).toHaveTextContent(/16 MiB|too large/i);
    expect(within(huge).queryByRole("button")).toBeNull();

    // The approval checkbox sits after the list, inside the same dialog body.
    const approval = within(dialog).getByRole("checkbox", { name: "Replace 1 existing file(s)" });
    expect(list.compareDocumentPosition(approval) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(dialog.querySelector(".dialog__body")).toContainElement(approval);
  });

  it("follows the status of an upload with the badge tone and keeps the summary a polite live region", async () => {
    const api = mockApi();
    api.uploadFile.mockImplementation(() => new Promise(() => undefined));
    render();
    const dialog = await openUploadDialog();
    await user().upload(
      within(dialog).getByLabelText("File (maximum 16 MiB)"),
      new File(["data"], "fresh.txt"),
    );
    await within(dialog).findByText("fresh.txt");
    await user().click(within(dialog).getByRole("button", { name: "Upload" }));

    const row = await waitFor(() => {
      const found = dialog.querySelector<HTMLElement>("li.file-upload-queue__row")!;
      expect(within(found).getByText("Uploading")).toHaveClass("status-badge--info");
      return found;
    });
    expect(row).toHaveAttribute("data-status", "uploading");
    const summary = dialog.querySelector(".file-upload-summary")!;
    expect(summary).toHaveAttribute("aria-live", "polite");
    expect(
      summary.compareDocumentPosition(dialog.querySelector("ul.file-upload-queue")!) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("keeps every file rule in the token sheet, with the dialog widths and the stacked actions", () => {
    const sourceRoot = join(import.meta.dirname, "..", "..", "src");
    const filesCss = readFileSync(join(sourceRoot, "components", "server", "files.css"), "utf8");
    const hostingCss = readFileSync(join(sourceRoot, "features", "hosting", "hosting.css"), "utf8");
    expect(hostingCss).not.toMatch(/next-hosting-(file|explorer|upload|tree|immediate)/);
    expect(filesCss).toMatch(
      /\.dialog\.next-hosting-upload-dialog\s*\{[^}]*width:\s*min\(var\(--utf-review-dialog-width\)/,
    );
    expect(filesCss).toContain(".dialog.next-hosting-file-dialog .dialog__actions > *");
  });

  it("keeps the upload queue styles in the token sheet and out of the legacy sheet", () => {
    const sourceRoot = join(import.meta.dirname, "..", "..", "src");
    const filesCss = readFileSync(join(sourceRoot, "components", "server", "files.css"), "utf8");
    const hostingCss = readFileSync(join(sourceRoot, "features", "hosting", "hosting.css"), "utf8");
    expect(filesCss).toContain(".file-upload-queue__row");
    expect(filesCss).toContain(".file-upload-summary");
    expect(hostingCss).not.toMatch(/next-hosting-upload-(queue|summary|error)/);
  });

  it("styles the drop zone with a dashed control border and no gold", () => {
    const sourceRoot = join(import.meta.dirname, "..", "..", "src");
    const filesCss = readFileSync(join(sourceRoot, "components", "server", "files.css"), "utf8");
    const zoneRule = /\.file-dropzone\s*\{([^}]*)\}/.exec(filesCss)?.[1] ?? "";
    expect(zoneRule).toMatch(/border:\s*1px dashed var\(--utf-border-control\)/);
    expect(zoneRule).toMatch(/min-height:\s*var\(--utf-dropzone-min-height\)/);
    expect(filesCss).not.toMatch(/--(utf-)?gold/);
  });
});

describe("ServerFilesPanel file browser card", () => {
  beforeEach(async () => {
    await i18nReady;
    await i18n.changeLanguage("en");
    vi.restoreAllMocks();
  });

  /** The folder heading of the list pane. */
  function listHeading(container: HTMLElement): HTMLElement {
    return container.querySelector<HTMLElement>(".file-browser__list h3")!;
  }

  it("names the context at the root and the folder inside a folder, never an empty heading", async () => {
    mockApi();
    const { container, unmount } = render();
    await rowOf("notes.txt");
    expect(listHeading(container).tagName).toBe("H3");
    expect(listHeading(container)).toHaveTextContent("Persistent expected files");
    unmount();

    const live = render("?tab=files&area=live");
    await rowOf("notes.txt");
    expect(listHeading(live.container)).toHaveTextContent("Live worker files");
    live.unmount();

    const folder = render("?tab=files&path=profiles");
    await waitFor(() => expect(listHeading(folder.container)).toHaveTextContent("profiles"));
    expect(listHeading(folder.container).textContent?.trim()).not.toBe("");
  });

  it("drops the permanent immediate note and shows it in the footer after the item count", async () => {
    mockApi();
    const { container } = render();
    await rowOf("notes.txt");

    expect(screen.queryByText("File changes happen right away. They are not staged.")).toBeNull();
    expect(container.querySelector(".next-hosting-immediate-note")).toBeNull();
    const footer = container.querySelector<HTMLElement>(".file-browser > footer")!;
    expect(footer).toHaveAttribute("aria-live", "polite");
    const parts = Array.from(footer.children).map((part) => part.textContent);
    expect(parts).toEqual(["2 items", "Changes to files take effect immediately."]);
  });

  it("renders the table, the empty state and the no-result state after the heading inside the card", async () => {
    mockApi();
    const { container, unmount } = render();
    const table = await screen.findByRole("table");
    const card = container.querySelector(".file-browser")!;
    expect(card).toContainElement(table);
    expect(
      listHeading(container).compareDocumentPosition(table) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    await user().type(screen.getByRole("searchbox", { name: "Search this folder" }), "zzz");
    const noResults = screen.getByText("No files match");
    expect(card).toContainElement(noResults);
    expect(
      listHeading(container).compareDocumentPosition(noResults) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    unmount();

    mockApi([]);
    const empty = render();
    const emptyTitle = await screen.findByText("This folder is empty");
    expect(empty.container.querySelector(".file-browser")).toContainElement(emptyTitle);
    expect(
      listHeading(empty.container).compareDocumentPosition(emptyTitle) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("renders one workspace panel that holds the card and keeps the hook classes", async () => {
    mockApi();
    const { container } = render();
    await rowOf("notes.txt");

    const panel = container.querySelector("section.workspace-panel.next-hosting-files-panel")!;
    expect(panel).not.toBeNull();
    expect(panel.querySelectorAll(".file-browser")).toHaveLength(1);
    const card = panel.querySelector(".file-browser")!;
    expect(card.querySelector(".next-hosting-explorer")).not.toBeNull();
    expect(card.querySelector(".next-hosting-file-content")).not.toBeNull();
    expect(card.querySelector(".next-hosting-file-folder-heading")).not.toBeNull();
  });

  it("puts the path, Upload, New folder, an icon-only Refresh and the search in one named toolbar", async () => {
    mockApi();
    const { container } = render();
    await rowOf("notes.txt");

    const toolbar = screen.getByRole("toolbar", { name: "File actions" });
    expect(container.querySelector(".file-browser")).toContainElement(toolbar);
    expect(within(toolbar).getByRole("navigation", { name: "Folder path" })).toBeInTheDocument();
    expect(within(toolbar).getByRole("searchbox", { name: "Search this folder" })).toBeVisible();
    const upload = within(toolbar).getByRole("button", { name: "Upload" });
    expect(upload).toHaveClass("button--gold");
    expect(within(toolbar).getByRole("button", { name: "Create folder" })).toHaveClass(
      "button--ghost",
    );
    const refresh = within(toolbar).getByRole("button", { name: "Refresh" });
    expect(refresh).toHaveClass("button--icon-only");
    expect(refresh.textContent?.trim()).toBe("");
    // Upload is the one gold action of the view.
    expect(container.querySelectorAll(".button--gold")).toHaveLength(1);
  });

  it("offers a plain button toolbar without Upload or New folder to a member without file rights", async () => {
    mockApi();
    renderWithProviders(
      <ServerFilesPanel server={{ ...server, canModify: false } as unknown as GameServer} />,
      { route: `${workspace}?tab=files` },
    );
    await rowOf("notes.txt");

    const toolbar = screen.getByRole("toolbar", { name: "File actions" });
    expect(within(toolbar).queryByRole("button", { name: "Upload" })).toBeNull();
    expect(within(toolbar).queryByRole("button", { name: "Create folder" })).toBeNull();
    expect(within(toolbar).getByRole("button", { name: "Refresh" })).toBeVisible();
  });

  it("keeps the empty folder upload action neutral so Upload stays the only gold action", async () => {
    mockApi([]);
    const { container } = render();

    await screen.findByText("This folder is empty");
    const emptyAction = container.querySelector(".state-panel__action .button")!;
    expect(emptyAction).toHaveTextContent("Upload");
    expect(emptyAction).toHaveClass("button--primary");
    expect(container.querySelectorAll(".button--gold")).toHaveLength(1);
  });

  it("switches the file context with a segmented radio group and the Arrow keys", async () => {
    mockApi();
    const { container, router } = render();
    await rowOf("notes.txt");

    expect(screen.queryByRole("tab")).toBeNull();
    expect(screen.queryByRole("tablist")).toBeNull();
    const group = screen.getByRole("group", { name: "File context" });
    const legend = container.querySelector("legend");
    expect(legend).toHaveClass("sr-only");
    expect(container.querySelector("[data-layout='segmented']")).toContainElement(group);
    const persistent = within(group).getByRole("radio", { name: "Persistent expected files" });
    const live = within(group).getByRole("radio", { name: "Live worker files" });
    expect(persistent).toBeChecked();

    persistent.focus();
    await user().keyboard("{ArrowRight}");
    await waitFor(() => expect(live).toBeChecked());
    expect(router.state.location.search).toBe("?tab=files&area=live");
  });

  it("lays the list pane out as a top-aligned flex column without the old grid template", () => {
    const sourceRoot = join(import.meta.dirname, "..", "..", "src");
    const filesCss = readFileSync(join(sourceRoot, "components", "server", "files.css"), "utf8");
    const hostingCss = readFileSync(join(sourceRoot, "features", "hosting", "hosting.css"), "utf8");
    expect(filesCss).not.toContain("grid-template-rows");
    expect(filesCss).not.toMatch(/margin-top:\s*auto/);
    expect(filesCss).not.toContain("32rem");
    const listRule = /\.file-browser__list\s*\{([^}]*)\}/.exec(filesCss)?.[1] ?? "";
    expect(listRule).toMatch(/align-self:\s*start/);
    expect(listRule).toMatch(/flex-direction:\s*column/);
    expect(hostingCss).not.toMatch(
      /next-hosting-file-content|next-hosting-explorer|next-hosting-immediate/,
    );
  });
});

let activeUser: ReturnType<typeof userEvent.setup> | undefined;

/** One user-event instance per test, so keyboard state is not shared between tests. */
function user(): ReturnType<typeof userEvent.setup> {
  activeUser ??= userEvent.setup();
  return activeUser;
}

beforeEach(() => {
  activeUser = undefined;
});
