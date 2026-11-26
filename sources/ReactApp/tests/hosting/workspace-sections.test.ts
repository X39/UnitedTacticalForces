import i18n, { i18nReady } from "@/app/i18n";
import {
  activityEntryAnchor,
  activityEntryTo,
  leavesWorkspaceSection,
  sectionSearch,
  sectionTo,
  workspaceSectionFromSearch,
  workspaceSectionLabelKeys,
  workspaceTabIds,
  type WorkspaceSection,
} from "@/features/hosting/workspaceSections";
import de from "@/locales/de.json";
import en from "@/locales/en.json";

const serverId = "6f1d1c52-7a7c-4a52-9b0e-0d6c6a1d9a11";

describe("workspace section registry", () => {
  beforeAll(async () => i18nReady);

  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("lists exactly the five sections in the specified order", () => {
    expect(workspaceTabIds).toEqual(["overview", "activity", "settings", "files", "machine"]);
  });

  it.each(workspaceTabIds)("accepts the section id %s for itself", (id) => {
    expect(workspaceSectionFromSearch(id)).toBe(id);
  });

  it.each([
    null,
    "",
    "jobs",
    "cloud",
    "runtime",
    "logs",
    "configuration",
    "credentials",
    "audit",
    "Activity",
    "OVERVIEW",
    " activity",
    "activity ",
    "__proto__",
    "constructor",
    "toString",
    "hasOwnProperty",
  ])("resolves the value %j to overview", (value) => {
    expect(workspaceSectionFromSearch(value)).toBe("overview");
  });

  it("writes no search for overview and only the tab parameter for the other sections", () => {
    expect(sectionSearch("overview")).toBe("");
    expect(sectionSearch("activity")).toBe("?tab=activity");
    expect(sectionSearch("settings")).toBe("?tab=settings");
    expect(sectionSearch("files")).toBe("?tab=files");
    expect(sectionSearch("machine")).toBe("?tab=machine");
  });

  it("never carries an action parameter into a section link", () => {
    for (const id of workspaceTabIds) {
      expect(sectionSearch(id)).not.toContain("action");
      expect(sectionTo(serverId, id).search).not.toContain("action");
    }
  });

  it("builds section links to the server workspace pathname", () => {
    const activity = sectionTo(serverId, "activity");
    expect(activity.pathname).toBe(`/command/servers/${serverId}`);
    expect(activity.search).toBe("?tab=activity");

    const overview = sectionTo(serverId, "overview");
    expect(overview.pathname).toBe(`/command/servers/${serverId}`);
    expect(overview.search).toBe("");
  });

  it("encodes the server id as one path segment", () => {
    expect(sectionTo("a/b c", "files").pathname).toBe("/command/servers/a%2Fb%20c");
  });

  it("builds the activity entry anchors", () => {
    expect(activityEntryAnchor("job", "abc")).toBe("activity-job-abc");
    expect(activityEntryAnchor("log", "42")).toBe("activity-log-42");
    expect(activityEntryAnchor("audit", "x-1")).toBe("activity-audit-x-1");
  });

  it("builds the activity entry link with the tab and the anchor fragment", () => {
    const to = activityEntryTo(serverId, activityEntryAnchor("job", "abc"));

    expect(to.pathname).toBe(`/command/servers/${serverId}`);
    expect(to.search).toBe("?tab=activity");
    expect(to.hash).toBe("#activity-job-abc");
  });

  describe("leavesWorkspaceSection", () => {
    const base = `/command/servers/${serverId}`;
    const at = (search: string, pathname = base) => ({ pathname, search });

    it("does not leave when only unrelated parameters change inside the section", () => {
      expect(
        leavesWorkspaceSection(at("?tab=settings"), at("?tab=settings&action=start"), "settings"),
      ).toBe(false);
      expect(
        leavesWorkspaceSection(at("?tab=files&path=a"), at("?tab=files&path=b"), "files"),
      ).toBe(false);
    });

    it("leaves when the resolved section changes", () => {
      expect(leavesWorkspaceSection(at("?tab=settings"), at("?tab=files"), "settings")).toBe(true);
      expect(leavesWorkspaceSection(at("?tab=settings"), at(""), "settings")).toBe(true);
    });

    it("leaves when the pathname differs even if the tab stays", () => {
      expect(
        leavesWorkspaceSection(
          at("?tab=settings"),
          at("?tab=settings", "/command/servers/other"),
          "settings",
        ),
      ).toBe(true);
    });

    it("resolves an unknown or prototype tab value to overview", () => {
      expect(leavesWorkspaceSection(at(""), at("?tab=jobs"), "overview")).toBe(false);
      expect(leavesWorkspaceSection(at(""), at("?tab=__proto__"), "overview")).toBe(false);
      expect(leavesWorkspaceSection(at("?tab=settings"), at("?tab=jobs"), "settings")).toBe(true);
    });
  });

  it("has a label key in both languages for every section", () => {
    const english: Record<string, string> = en;
    const german: Record<string, string> = de;
    const ids: readonly WorkspaceSection[] = workspaceTabIds;

    for (const id of ids) {
      const key = workspaceSectionLabelKeys[id];
      expect(english[key]?.trim(), `en ${key}`).toBeTruthy();
      expect(german[key]?.trim(), `de ${key}`).toBeTruthy();
    }
  });

  it("resolves a tab value to its localized label through the app i18n instance", async () => {
    const label = (value: string | null) =>
      i18n.t(workspaceSectionLabelKeys[workspaceSectionFromSearch(value)]);

    await i18n.changeLanguage("en");
    expect(label("machine")).toBe("Machine");
    expect(label("jobs")).toBe("Overview");

    await i18n.changeLanguage("de");
    expect(label("machine")).toBe("Maschine");
    expect(label("jobs")).toBe("Übersicht");
  });

  it("names the section navigation Server sections in English and Serverbereiche in German", () => {
    expect(en["Hosting_Detail_WorkspaceNavigation"]).toBe("Server sections");
    expect(de["Hosting_Detail_WorkspaceNavigation"]).toBe("Serverbereiche");
  });
});
