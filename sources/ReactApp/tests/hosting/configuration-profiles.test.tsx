import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nextProvider } from "react-i18next";
import { createMemoryRouter, RouterProvider } from "react-router";
import { beforeEach } from "vitest";
import i18n, { i18nReady } from "@/app/i18n";
import { AppToastProvider } from "@/components/AppToasts";
import { ConfigurationProfileCreatePage } from "@/features/hosting/ConfigurationProfileCreatePage";
import { ConfigurationProfileWorkspacePage } from "@/features/hosting/ConfigurationProfileWorkspacePage";
import { ConfigurationProfilesPage } from "@/features/hosting/ConfigurationProfilesPage";
import { hostingService } from "@/features/hosting/services";
import type {
  ConfigurationDefinition,
  ConfigurationProfileDetails,
  ConfigurationProfileSummary,
} from "@/features/hosting/types";
import { findToast, toastPoliteness } from "../helpers/toastQueries";

const authState = vi.hoisted(() => ({
  claims: [{ identifier: "adm", value: null as string | null }],
}));

vi.mock("@/auth/auth", () => ({
  useAuth: () => ({
    user: {
      primaryKey: "10000000-0000-0000-0000-000000000001",
      nickname: "Commander",
      isBanned: false,
      isVerified: true,
      isDeleted: false,
      isVereinMember: false,
      claims: authState.claims,
      roles: [],
    },
    isLoading: false,
    isAuthenticated: true,
  }),
}));

const profile: ConfigurationProfileSummary = {
  primaryKey: "20000000-0000-0000-0000-000000000002",
  title: "Public operations",
  gameIdentifier: "arma3",
  isArchived: false,
  latestRevisionNumber: 1,
};

const archivedProfile: ConfigurationProfileSummary = {
  ...profile,
  primaryKey: "20000000-0000-0000-0000-000000000004",
  title: "Legacy baseline",
  isArchived: true,
};

const definitions: ConfigurationDefinition[] = [
  {
    key: "hostname",
    targetPath: "server.cfg",
    group: "General",
    displayName: "Hostname",
    description: "Server browser name.",
    kind: "String",
    required: true,
    sensitive: false,
    order: 10,
    defaultValue: null,
    minimum: null,
    maximum: null,
    allowedValues: [],
  },
  {
    key: "motd[]",
    targetPath: "server.cfg",
    group: "General",
    displayName: "Message of the day",
    description: "One line per message.",
    kind: "StringArray",
    required: false,
    sensitive: false,
    order: 20,
    defaultValue: [],
    minimum: null,
    maximum: null,
    allowedValues: [],
  },
  {
    key: "password",
    targetPath: "server.cfg",
    group: "Security",
    displayName: "Password",
    description: "Join secret.",
    kind: "String",
    required: false,
    sensitive: true,
    order: 30,
    defaultValue: null,
    minimum: null,
    maximum: null,
    allowedValues: [],
  },
];

function details(
  overrides: Partial<ConfigurationProfileDetails> = {},
): ConfigurationProfileDetails {
  return {
    profile,
    canModify: true,
    canManageAccess: false,
    canArchive: true,
    definitions,
    revisions: [
      {
        primaryKey: "30000000-0000-0000-0000-000000000003",
        revisionNumber: 1,
        timeStampCreated: "2026-08-13T12:00:00Z",
        values: { hostname: "UTF", password: "secret" },
        redactedKeys: [],
      },
    ],
    ...overrides,
  };
}

function renderPage(initialEntry = "/command/configuration-profiles") {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: "/command/configuration-profiles", element: <ConfigurationProfilesPage /> },
      {
        path: "/command/configuration-profiles/create",
        element: <ConfigurationProfileCreatePage />,
      },
      {
        path: "/command/configuration-profile-without-id",
        element: <ConfigurationProfileWorkspacePage />,
      },
      {
        path: "/command/configuration-profiles/:profileId",
        element: <ConfigurationProfileWorkspacePage />,
      },
    ],
    { initialEntries: [initialEntry] },
  );
  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <AppToastProvider>
          <RouterProvider router={router} />
        </AppToastProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
  return router;
}

describe("configuration-profile routes", () => {
  beforeAll(async () => i18nReady);

  beforeEach(() => {
    vi.spyOn(hostingService, "profiles").mockResolvedValue([]);
    vi.spyOn(hostingService, "profileCatalog").mockResolvedValue({
      canCreate: true,
      games: [{ gameIdentifier: "arma3", definitions }],
    });
    vi.spyOn(hostingService, "profile").mockResolvedValue(details());
    vi.spyOn(hostingService, "profileAccess").mockResolvedValue({
      allowedPermissions: ["cfg", "cfg:view", "cfg:modify", "cfg:manage-access", "cfg:archive"],
      users: [],
      roles: [],
    });
    vi.spyOn(hostingService, "createProfile").mockResolvedValue(profile);
    vi.spyOn(hostingService, "createProfileRevision").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "archiveProfile").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "changeProfileUserAccess").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "changeProfileRoleAccess").mockResolvedValue(undefined);
    vi.spyOn(hostingService, "profileUserCandidates").mockResolvedValue([]);
    vi.spyOn(hostingService, "profileRoleCandidates").mockResolvedValue([]);
    vi.spyOn(hostingService, "updateProfileAccessMatrix").mockResolvedValue(undefined);
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    authState.claims = [{ identifier: "adm", value: null }];
    await act(async () => i18n.changeLanguage("en"));
  });

  it("filters the full-width library and navigates through profile links", async () => {
    vi.mocked(hostingService.profiles).mockResolvedValue([profile, archivedProfile]);
    const user = userEvent.setup();
    const router = renderPage();

    expect(await screen.findByText("Total profiles")).toBeVisible();
    expect(screen.getByText("Public operations")).toBeVisible();
    expect(screen.getByText("Legacy baseline")).toBeVisible();

    await user.selectOptions(screen.getByLabelText("Status"), "archived");
    expect(screen.queryByText("Public operations")).not.toBeInTheDocument();
    expect(screen.getByText("Legacy baseline")).toBeVisible();

    await user.click(screen.getByRole("link", { name: "Open profile" }));
    expect(router.state.location.pathname).toBe(
      `/command/configuration-profiles/${archivedProfile.primaryKey}`,
    );
  });

  it("validates the creation wizard, serializes arrays, and excludes server secrets", async () => {
    const user = userEvent.setup();
    const router = renderPage("/command/configuration-profiles/create");

    await screen.findByRole("heading", { name: "Basics" });
    await user.type(screen.getByLabelText(/^Profile title/), "Public operations");
    await user.click(screen.getByRole("button", { name: "Next: configuration" }));
    await user.click(screen.getByRole("button", { name: "Review profile" }));
    expect(await screen.findByText("Hostname is required.")).toBeVisible();

    await user.type(screen.getByLabelText(/^Hostname/), "UTF Public");
    await user.type(screen.getByLabelText("Message of the day"), "Alpha\nBravo");
    expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Review profile" }));
    await user.click(screen.getByRole("button", { name: "Create profile" }));

    await waitFor(() =>
      expect(hostingService.createProfile).toHaveBeenCalledWith("Public operations", "arma3", [
        { key: "hostname", value: "UTF Public" },
        { key: "motd[]", value: ["Alpha", "Bravo"] },
      ]),
    );
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(
        `/command/configuration-profiles/${profile.primaryKey}`,
      ),
    );
  });

  it("publishes changed values from the dedicated configuration tab", async () => {
    const user = userEvent.setup();
    renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

    const hostname = await screen.findByLabelText(/^Hostname/);
    expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
    await user.clear(hostname);
    await user.type(hostname, "UTF Updated");
    expect(screen.getByText("1 unsaved change(s)")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Publish revision" }));

    await waitFor(() =>
      expect(hostingService.createProfileRevision).toHaveBeenCalledWith(
        profile.primaryKey,
        1,
        [{ key: "hostname", value: "UTF Updated" }],
        [],
      ),
    );
  });

  it("publishes missing required defaults without requiring the field to be edited", async () => {
    vi.mocked(hostingService.profile).mockResolvedValue(
      details({
        definitions: definitions.map((definition) =>
          definition.key === "hostname"
            ? { ...definition, defaultValue: "United Tactical Forces" }
            : definition,
        ),
        revisions: [
          {
            primaryKey: "30000000-0000-0000-0000-000000000003",
            revisionNumber: 1,
            timeStampCreated: "2026-08-13T12:00:00Z",
            values: { "motd[]": ["Alpha"] },
          },
        ],
      }),
    );
    const user = userEvent.setup();
    renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

    const message = await screen.findByLabelText("Message of the day");
    await user.clear(message);
    await user.type(message, "Bravo");
    await user.click(screen.getByRole("button", { name: "Publish revision" }));

    await waitFor(() =>
      expect(hostingService.createProfileRevision).toHaveBeenCalledWith(
        profile.primaryKey,
        1,
        [
          { key: "hostname", value: "United Tactical Forces" },
          { key: "motd[]", value: ["Bravo"] },
        ],
        [],
      ),
    );
  });

  it("shows an unset Boolean distinctly and changes it directly to false", async () => {
    const booleanDefinition = {
      key: "persistent",
      targetPath: "server.cfg",
      group: "Lifecycle",
      displayName: "Persistent battlefield",
      description: "Retain mission state.",
      kind: "Boolean",
      required: false,
      sensitive: false,
      order: 40,
      defaultValue: true,
      minimum: null,
      maximum: null,
      allowedValues: [],
    } satisfies ConfigurationDefinition;
    vi.mocked(hostingService.profile).mockResolvedValue(
      details({ definitions: [...definitions, booleanDefinition] }),
    );
    const user = userEvent.setup();
    renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

    const checkbox = await screen.findByRole("checkbox", { name: "Persistent battlefield" });
    expect(checkbox).toHaveProperty("indeterminate", true);
    expect(screen.getByText("Not set")).toBeVisible();
    await user.click(checkbox);
    expect(checkbox).not.toBeChecked();
    expect(checkbox).toHaveProperty("indeterminate", false);
    expect(screen.getByText("Disabled")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Publish revision" }));

    await waitFor(() =>
      expect(hostingService.createProfileRevision).toHaveBeenCalledWith(
        profile.primaryKey,
        1,
        [{ key: "persistent", value: false }],
        [],
      ),
    );
  });

  it("shows revision publishing failures as an alert toast", async () => {
    vi.mocked(hostingService.createProfileRevision).mockRejectedValue(
      new Error("The latest revision must be reloaded."),
    );
    const user = userEvent.setup();
    renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

    const hostname = await screen.findByLabelText(/^Hostname/);
    await user.clear(hostname);
    await user.type(hostname, "UTF Updated");
    await user.click(screen.getByRole("button", { name: "Publish revision" }));

    const toast = await findToast("The latest revision must be reloaded.");
    expect(toast).toHaveTextContent("The latest revision must be reloaded.");
    expect(toastPoliteness(toast)).toBe("assertive");
  });

  it("keeps redacted values out of controls and revision output for read-only profiles", async () => {
    vi.mocked(hostingService.profile).mockResolvedValue(
      details({
        canModify: false,
        canArchive: false,
        revisions: [
          {
            primaryKey: "30000000-0000-0000-0000-000000000003",
            revisionNumber: 1,
            timeStampCreated: "2026-08-13T12:00:00Z",
            values: { hostname: "UTF", password: null },
            sensitiveConfiguredKeys: ["password"],
            changes: [],
          },
        ],
      }),
    );
    const user = userEvent.setup();
    renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

    expect(await screen.findByRole("heading", { name: "Profile is read-only" })).toBeVisible();
    expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
    await user.click(screen.getByRole("link", { name: "Revisions" }));
    await user.click(await screen.findByText("View complete snapshot"));
    expect(await screen.findByText("Configured securely")).toBeVisible();
  });

  it("falls back from an unauthorized access deep link", async () => {
    const router = renderPage(`/command/configuration-profiles/${profile.primaryKey}?tab=access`);

    expect(await screen.findByRole("link", { name: "Configuration" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(screen.queryByRole("link", { name: "Access" })).not.toBeInTheDocument();
    await waitFor(() => expect(router.state.location.search).toBe("?tab=configuration"));
  });

  it("searches principals and saves the desired access matrix", async () => {
    vi.mocked(hostingService.profile).mockResolvedValue(details({ canManageAccess: true }));
    vi.mocked(hostingService.profileAccess).mockResolvedValue({
      allowedPermissions: ["cfg", "cfg:view"],
      users: [],
      roles: [
        { roleId: 7, title: "Operators", description: "Hosting team", permissions: ["cfg:view"] },
      ],
    });
    vi.mocked(hostingService.profileUserCandidates).mockResolvedValue([
      {
        userId: "40000000-0000-4000-8000-000000000004",
        nickname: "Quartermaster",
      },
    ]);
    const user = userEvent.setup();
    renderPage(`/command/configuration-profiles/${profile.primaryKey}?tab=access`);

    expect(await screen.findByRole("heading", { name: "Who can use this profile" })).toBeVisible();
    expect(await screen.findByText("Operators")).toBeVisible();
    const search = screen.getByRole("combobox", { name: "Search people" });
    await user.type(search, "Quarter");
    expect(await screen.findByRole("option", { name: /Quartermaster/ })).toBeVisible();
    await user.type(search, "{ArrowDown}{Enter}");
    await user.click(screen.getByRole("checkbox", { name: "Quartermaster: View" }));
    await user.click(screen.getByRole("button", { name: "Save access" }));
    await waitFor(() =>
      expect(hostingService.updateProfileAccessMatrix).toHaveBeenCalledWith(profile.primaryKey, {
        users: [
          {
            userId: "40000000-0000-4000-8000-000000000004",
            permissions: ["cfg:view"],
          },
        ],
        roles: [{ roleId: 7, permissions: ["cfg:view"] }],
      }),
    );
  });

  it("renders the library chrome in German", async () => {
    await act(async () => i18n.changeLanguage("de"));
    vi.mocked(hostingService.profiles).mockResolvedValue([profile]);
    renderPage();

    expect(await screen.findByRole("heading", { name: "Konfigurationsprofile" })).toBeVisible();
    expect(await screen.findByLabelText("Profile durchsuchen")).toBeVisible();
    expect(screen.getByRole("link", { name: "Neues Profil" })).toBeVisible();
  });

  describe("library, create form and access matrix on shared fields", () => {
    it("filters with a search field and explains how to clear a filter without matches", async () => {
      vi.mocked(hostingService.profiles).mockResolvedValue([profile, archivedProfile]);
      const user = userEvent.setup();
      renderPage();

      await screen.findByText("Total profiles");
      const search = screen.getByRole("searchbox", { name: "Search profiles" });
      expect(search).toBeVisible();
      await user.type(search, "zzz-no-match");

      expect(
        await screen.findByRole("heading", { name: "No profiles match these filters" }),
      ).toBeVisible();
      expect(
        screen.getByText("Adjust the search term or filters to show more profiles."),
      ).toBeVisible();
      await user.click(screen.getByRole("button", { name: "Clear search and filter" }));
      expect(screen.getByText("Public operations")).toBeVisible();
      expect(screen.getByRole("searchbox", { name: "Search profiles" })).toHaveValue("");
      expect(screen.getByLabelText("Status")).toHaveValue("all");
    });

    it("uses the shared select for the game filter with the padded chevron", async () => {
      vi.mocked(hostingService.profiles).mockResolvedValue([profile]);
      renderPage();

      const game = await screen.findByLabelText("Game");
      expect(game).toHaveClass("form-field__control--select");
      expect(game).toHaveValue("all");
    });

    it("keeps required markers, validation messages and field links on the create form", async () => {
      const user = userEvent.setup();
      renderPage("/command/configuration-profiles/create");

      const title = await screen.findByLabelText(/^Profile title/);
      expect(title).toBeRequired();
      expect(title).toHaveAttribute("aria-required", "true");
      expect(title).toHaveAttribute("maxlength", "256");
      await user.type(title, "Public operations");
      await user.click(screen.getByRole("button", { name: "Next: configuration" }));
      await user.click(screen.getByRole("button", { name: "Review profile" }));

      const hostname = await screen.findByLabelText(/^Hostname/);
      expect(hostname).toBeRequired();
      expect(hostname).toHaveAttribute("aria-invalid", "true");
      expect(screen.getByRole("alert")).toHaveTextContent("Hostname is required.");
      expect(hostname.getAttribute("aria-describedby")).toContain(
        screen.getByRole("alert").getAttribute("id"),
      );
    });

    it("marks the current wizard step and keeps later steps unreachable", async () => {
      renderPage("/command/configuration-profiles/create");

      const steps = await screen.findAllByRole("button", { name: /^\d/ });
      expect(steps[0]).toHaveAttribute("aria-current", "step");
      expect(steps[0]).toHaveClass("button");
      expect(steps[1]).toBeDisabled();
    });

    it("saves the access matrix payload with labelled shared checkboxes", async () => {
      vi.mocked(hostingService.profile).mockResolvedValue(details({ canManageAccess: true }));
      vi.mocked(hostingService.profileAccess).mockResolvedValue({
        allowedPermissions: ["cfg", "cfg:view", "cfg:modify"],
        users: [],
        roles: [
          {
            roleId: 7,
            title: "Operators",
            description: "Hosting team",
            permissions: ["cfg:view"],
          },
        ],
      });
      const user = userEvent.setup();
      renderPage(`/command/configuration-profiles/${profile.primaryKey}?tab=access`);

      expect(await screen.findByRole("radio", { name: "User" })).toBeChecked();
      const view = await screen.findByRole("checkbox", { name: "Operators: View" });
      expect(view).toBeChecked();
      const modify = screen.getByRole("checkbox", { name: "Operators: Modify" });
      expect(modify).not.toBeChecked();
      await user.click(modify);
      await user.click(screen.getByRole("button", { name: "Save access" }));

      await waitFor(() =>
        expect(hostingService.updateProfileAccessMatrix).toHaveBeenCalledWith(profile.primaryKey, {
          users: [],
          roles: [{ roleId: 7, permissions: ["cfg:view", "cfg:modify"] }],
        }),
      );
      expect(screen.getByRole("button", { name: "Remove: Operators" })).toBeVisible();
    });
  });

  describe("revision snapshots on the shared disclosure", () => {
    it("expands and collapses a snapshot by click and by Enter", async () => {
      const user = userEvent.setup();
      renderPage(`/command/configuration-profiles/${profile.primaryKey}?tab=revisions`);

      const toggle = await screen.findByRole("button", { name: "View complete snapshot" });
      expect(toggle).toHaveAttribute("aria-expanded", "false");
      expect(document.querySelector("details")).toBeNull();

      await user.click(toggle);
      expect(toggle).toHaveAttribute("aria-expanded", "true");
      expect(screen.getByRole("region", { name: "View complete snapshot" })).toBeVisible();

      toggle.focus();
      await user.keyboard("{Enter}");
      expect(toggle).toHaveAttribute("aria-expanded", "false");
      await user.keyboard("{Enter}");
      expect(toggle).toHaveAttribute("aria-expanded", "true");
    });
  });

  describe("profile workspace header, states and dismiss label", () => {
    function expectOneLeadingH1(title: string) {
      const h1 = screen.getAllByRole("heading", { level: 1 });
      expect(h1).toHaveLength(1);
      expect(h1[0]).toHaveTextContent(title);
      const first = screen.getAllByRole("heading")[0];
      expect(first).toBe(h1[0]);
    }

    it("renders one leading h1 from the shared header when the profile is loaded", async () => {
      renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

      await screen.findByLabelText(/^Hostname/);
      expectOneLeadingH1("Public operations");
      expect(screen.getByRole("button", { name: "Archive profile" })).toHaveClass(
        "button",
        "button--danger",
      );
    });

    it("renders one leading h1 above the loading state", async () => {
      vi.mocked(hostingService.profile).mockReturnValue(new Promise(() => undefined));
      renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

      expect(await screen.findByRole("status")).toBeVisible();
      expectOneLeadingH1("Configuration profiles");
    });

    it("renders one leading h1 above the forbidden state", async () => {
      authState.claims = [];
      renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

      expect(
        await screen.findByRole("heading", { name: "Configuration-profile access required" }),
      ).toBeVisible();
      expectOneLeadingH1("Configuration profiles");
    });

    it("renders one leading h1 above the not-found state", async () => {
      renderPage("/command/configuration-profile-without-id");

      expect(await screen.findByRole("heading", { name: "Profile unavailable" })).toBeVisible();
      expectOneLeadingH1("Configuration profiles");
    });

    it("renders one leading h1 above the error state and retries the real request", async () => {
      vi.mocked(hostingService.profile).mockRejectedValueOnce(new Error("Backend offline"));
      const user = userEvent.setup();
      renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

      expect(await screen.findByRole("alert")).toHaveTextContent("Backend offline");
      expectOneLeadingH1("Configuration profiles");
      await user.click(screen.getByRole("button", { name: "Try again" }));
      expect(await screen.findByRole("heading", { name: "Public operations" })).toBeVisible();
      expect(hostingService.profile).toHaveBeenCalledTimes(2);
    });

    it("names the effect of the archive dialog dismiss button in English", async () => {
      const user = userEvent.setup();
      renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

      await user.click(await screen.findByRole("button", { name: "Archive profile" }));
      const dialog = await screen.findByRole("dialog");
      expect(within(dialog).getByRole("button", { name: "Keep profile active" })).toBeVisible();
      expect(within(dialog).queryByRole("button", { name: "Cancel" })).not.toBeInTheDocument();
    });

    it("names the effect of the archive dialog dismiss button in German", async () => {
      await act(async () => i18n.changeLanguage("de"));
      const user = userEvent.setup();
      renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

      const archive = await screen.findByRole("button", { name: "Profil archivieren" });
      await user.click(archive);
      const dialog = await screen.findByRole("dialog");
      expect(within(dialog).getByRole("button", { name: "Profil aktiv lassen" })).toBeVisible();
    });

    it("reaches the section links and the archive button with the keyboard", async () => {
      const user = userEvent.setup();
      renderPage(`/command/configuration-profiles/${profile.primaryKey}`);

      await screen.findByLabelText(/^Hostname/);
      const archive = screen.getByRole("button", { name: "Archive profile" });
      const revisions = screen.getByRole("link", { name: "Revisions" });
      const reached = new Set<Element>();
      for (let step = 0; step < 12; step += 1) {
        await user.tab();
        if (document.activeElement) reached.add(document.activeElement);
      }
      expect(reached.has(archive)).toBe(true);
      expect(reached.has(revisions)).toBe(true);
    });
  });
});
