import { writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import {
  checkAllowances,
  checkRatchet,
  loadFamilies,
  loadRules,
  lowerCeilings,
  scanSource,
  scanTree,
  type FamilyFile,
  type FileCounts,
} from "./scan";

const ratchetDir = import.meta.dirname;
const projectRoot = resolve(ratchetDir, "..", "..");
const rules = loadRules(ratchetDir);

/** The families and the Wave 2 plan that owns each family file. */
const familyOwners: Record<string, string> = {
  shell: "69-08",
  "shared-workspace": "69-24",
  "hosting-core": "69-09",
  "hosting-files": "69-10",
  "hosting-profiles": "69-11",
  "hosting-finance": "69-12",
  "hosting-runtime": "69-13",
  "admin-authority": "69-14",
  "admin-members": "69-15",
  "events-admin": "69-16",
  "events-member": "69-17",
  "admin-content": "69-18",
  "admin-media-reports": "69-19",
  "admin-modpacks-terrains": "69-20",
  "admin-verein-settings": "69-21",
  "public-hero": "69-22",
  "public-forms-wiki": "69-23",
};

/**
 * Seed assignment of a violating file to a family; the first matching glob wins and `*` stays inside
 * one path segment. It is used only when a family file is seeded with `UTF_RATCHET_SEED=1`.
 */
const seedAssignments: ReadonlyArray<readonly [string, string]> = [
  ["src/main.tsx", "shell"],
  ["src/components/CommandCenterLayout.tsx", "shell"],
  ["src/components/CommunityLayout.tsx", "shell"],
  ["src/components/ProfileMenu.tsx", "shell"],
  ["src/components/commandNavigation.tsx", "shell"],
  ["src/pages/AuthenticationPage.tsx", "shell"],
  ["src/pages/CommandOverviewPage.tsx", "shell"],
  ["src/components/PublicHero.tsx", "public-hero"],
  ["src/components/server/CostConfirmDialog.tsx", "hosting-runtime"],
  ["src/components/server/StorageDemandDetails.tsx", "hosting-runtime"],
  ["src/components/*.tsx", "shared-workspace"],
  ["src/components/server/*.tsx", "shared-workspace"],
  ["src/features/hosting/AutomationPanel.tsx", "shared-workspace"],
  ["src/features/hosting/WorkspaceShell.tsx", "shared-workspace"],
  ["src/features/hosting/ServerConfigurationPanel.tsx", "shared-workspace"],
  ["src/features/hosting/ServerSecretsPanel.tsx", "shared-workspace"],
  ["src/features/hosting/sections/OverviewSection.tsx", "shared-workspace"],
  ["src/features/hosting/sections/ActivitySection.tsx", "shared-workspace"],
  ["src/features/hosting/sections/SettingsSection.tsx", "shared-workspace"],
  ["src/features/hosting/sections/MachineSection.tsx", "shared-workspace"],
  ["src/features/hosting/sections/CloudMachinePanel.tsx", "hosting-runtime"],
  ["src/features/hosting/FleetPages.tsx", "hosting-core"],
  ["src/features/hosting/components.tsx", "hosting-core"],
  ["src/features/hosting/CreateServerPage.tsx", "hosting-core"],
  ["src/features/hosting/ServerWorkspacePage.tsx", "hosting-core"],
  ["src/features/hosting/ServerFilesPanel.tsx", "hosting-files"],
  ["src/features/hosting/ConfigurationProfile*.tsx", "hosting-profiles"],
  ["src/features/hosting/HostingFinance*Page.tsx", "hosting-finance"],
  ["src/features/hosting/FinanceProtocol.tsx", "hosting-finance"],
  ["src/features/hosting/FinanceDialog.tsx", "hosting-finance"],
  ["src/features/hosting/FinanceAccountPicker.tsx", "hosting-finance"],
  ["src/features/hosting/CostAlertsPage.tsx", "hosting-finance"],
  ["src/features/hosting/CostAlertSettingsPanel.tsx", "hosting-finance"],
  ["src/features/hosting/ServerRuntimePanel.tsx", "hosting-runtime"],
  ["src/features/hosting/ServerLogsPanel.tsx", "hosting-runtime"],
  ["src/features/hosting/HostingAuditFeed.tsx", "hosting-runtime"],
  ["src/features/admin/ClaimsPage.tsx", "admin-authority"],
  ["src/features/admin/RolesPage.tsx", "admin-authority"],
  ["src/features/admin/RoleDetailPage.tsx", "admin-authority"],
  ["src/features/admin/MembersPage.tsx", "admin-members"],
  ["src/features/admin/MemberDetailPage.tsx", "admin-members"],
  ["src/features/admin/VereinMembershipRequestsPage.tsx", "admin-members"],
  ["src/features/admin/EventAdminPage.tsx", "events-admin"],
  ["src/features/admin/EventCreatePage.tsx", "events-admin"],
  ["src/features/admin/EventEditPage.tsx", "events-admin"],
  ["src/features/admin/EventEntityPicker.tsx", "events-admin"],
  ["src/features/admin/EventSlotPlanner.tsx", "events-admin"],
  ["src/features/admin/EventObjectiveIconPicker.tsx", "events-admin"],
  ["src/features/events/*.tsx", "events-admin"],
  ["src/features/events-member/*.tsx", "events-member"],
  ["src/features/admin/ContentHubPage.tsx", "admin-content"],
  ["src/features/admin/PublicPage*.tsx", "admin-content"],
  ["src/features/admin/GdprContentEditor.tsx", "admin-content"],
  ["src/features/admin/MediaGalleryPage.tsx", "admin-media-reports"],
  ["src/features/admin/SiteImagesPage.tsx", "admin-media-reports"],
  ["src/features/admin/GalleryAssetPicker.tsx", "admin-media-reports"],
  ["src/features/admin/FieldReport*.tsx", "admin-media-reports"],
  ["src/features/admin/ModPack*.tsx", "admin-modpacks-terrains"],
  ["src/features/admin/Terrain*.tsx", "admin-modpacks-terrains"],
  ["src/features/admin/VereinDocument*.tsx", "admin-verein-settings"],
  ["src/features/admin/VereinKassenbuch*.tsx", "admin-verein-settings"],
  ["src/features/admin/VereinSettingsPage.tsx", "admin-verein-settings"],
  ["src/features/admin/SettingsPage.tsx", "admin-verein-settings"],
  ["src/pages/HomePage.tsx", "public-hero"],
  ["src/pages/UpcomingEventsPage.tsx", "public-hero"],
  ["src/pages/UnitPage.tsx", "public-hero"],
  ["src/pages/VereinPages.tsx", "public-hero"],
  ["src/pages/JoinPage.tsx", "public-forms-wiki"],
  ["src/pages/GdprPage.tsx", "public-forms-wiki"],
  ["src/pages/FieldReportsPage.tsx", "public-forms-wiki"],
  ["src/pages/FieldReportDetailPage.tsx", "public-forms-wiki"],
  ["src/features/wiki/*.tsx", "public-forms-wiki"],
];

function seedFamilyOf(path: string): string | undefined {
  for (const [glob, family] of seedAssignments) {
    const pattern = glob.replace(/[.+?^${}()|[\]\\]/g, "\\$&").replaceAll("*", "[^/]*");
    if (new RegExp(`^${pattern}$`).test(path)) return family;
  }
  return undefined;
}

function familyFileName(family: string): string {
  return join(ratchetDir, `family.${family}.json`);
}

function writeFamily(file: FamilyFile): void {
  writeFileSync(familyFileName(file.family), `${JSON.stringify(file, null, 2)}\n`);
}

const zero: FileCounts = { controls: 0, lookalikes: 0, literals: 0 };

function sampleFamily(files: FamilyFile["files"]): FamilyFile {
  return { family: "sample", owner: "69-00", files };
}

const updateTarget = process.env["UTF_RATCHET_UPDATE"];
const seedFamilies = process.env["UTF_RATCHET_SEED"] === "1";

describe("ratchet ceiling writer (opt-in with UTF_RATCHET_UPDATE=<family|all>)", () => {
  it.skipIf(!updateTarget)("lowers the ceilings of the selected families", () => {
    const names = Object.keys(familyOwners);
    if (updateTarget !== "all" && !names.includes(updateTarget ?? "")) {
      throw new Error(`Unknown family "${updateTarget}". Use one of: ${names.join(", ")}, all.`);
    }
    const counts = scanTree(projectRoot, rules);
    const existing = new Map(loadFamilies(ratchetDir).map((file) => [file.family, file]));
    for (const name of names) {
      if (updateTarget !== "all" && updateTarget !== name) continue;
      const current = existing.get(name) ?? { family: name, owner: familyOwners[name]!, files: {} };
      const seedPaths = seedFamilies
        ? [...counts.keys()].filter((path) => seedFamilyOf(path) === name)
        : [];
      writeFamily(lowerCeilings(current, counts, seedPaths));
    }
  });
});

describe("raw control and literal text ratchet", () => {
  it("counts raw controls and button look-alikes in an inline sample", () => {
    const sample = [
      "export const Sample = () => (",
      "  <div>",
      '    <button type="button">x</button>',
      "    <button />",
      "    <input />",
      '    <Link className="button button--ghost" to="/">x</Link>',
      '    <a className={`button ${tone}`} href="/">x</a>',
      "  </div>",
      ");",
    ].join("\n");

    expect(scanSource("src/Sample.tsx", sample, rules)).toEqual({
      controls: 3,
      lookalikes: 2,
      literals: 0,
    });
  });

  it("counts nothing for the shared controls", () => {
    const sample = [
      "export const Sample = () => (",
      "  <>",
      "    <ActionButton onClick={save}>{t('Common_Save')}</ActionButton>",
      '    <TextField label={t("Common_Name")} className="field button-group" />',
      "  </>",
      ");",
    ].join("\n");

    expect(scanSource("src/Sample.tsx", sample, rules)).toEqual(zero);
  });

  it("exempts the primitive files from the control rules", () => {
    const source = '<button className="button" />;';

    expect(scanSource("src/components/ActionControl.tsx", source, rules)).toEqual(zero);
    expect(scanSource("src/components/Other.tsx", source, rules)).toEqual({
      controls: 1,
      lookalikes: 1,
      literals: 0,
    });
  });

  it("fails when a count exceeds its ceiling", () => {
    const counts = new Map([["src/A.tsx", scanSource("src/A.tsx", "<button /><button />", rules)]]);
    const family = sampleFamily({
      "src/A.tsx": { controls: 1, lookalikes: 0, literals: 0, reason: "converted in 69-00" },
    });

    expect(checkRatchet([family], counts)).toEqual([
      "sample: src/A.tsx has 2 controls, above its ceiling 1",
    ]);
  });

  it("fails when a violating file is in no family", () => {
    const counts = new Map([["src/B.tsx", scanSource("src/B.tsx", "<input />", rules)]]);

    expect(checkRatchet([sampleFamily({})], counts)).toHaveLength(1);
  });

  it("fails when a file is in two families", () => {
    const entry = { controls: 1, lookalikes: 0, literals: 0, reason: "converted in 69-00" };
    const counts = new Map([["src/C.tsx", scanSource("src/C.tsx", "<input />", rules)]]);
    const first = sampleFamily({ "src/C.tsx": entry });
    const second = { ...sampleFamily({ "src/C.tsx": entry }), family: "other" };

    expect(checkRatchet([first, second], counts).join("\n")).toMatch(/in two families/);
  });

  it("fails when an entry has no reason", () => {
    const counts = new Map([["src/D.tsx", scanSource("src/D.tsx", "<input />", rules)]]);
    const family = sampleFamily({
      "src/D.tsx": { controls: 1, lookalikes: 0, literals: 0, reason: "  " },
    });

    expect(checkRatchet([family], counts).join("\n")).toMatch(/no reason/);
  });

  it("lowers ceilings, removes zero entries and refuses to raise a ceiling", () => {
    const entry = (controls: number) => ({
      controls,
      lookalikes: 0,
      literals: 0,
      reason: "converted in 69-00",
    });
    const family = sampleFamily({ "src/E.tsx": entry(3), "src/F.tsx": entry(1) });
    const lowered = lowerCeilings(
      family,
      new Map([
        ["src/E.tsx", scanSource("src/E.tsx", "<input />", rules)],
        ["src/F.tsx", scanSource("src/F.tsx", "<div />", rules)],
      ]),
    );

    expect(lowered.files).toEqual({ "src/E.tsx": entry(1) });
    expect(() =>
      lowerCeilings(
        family,
        new Map([["src/F.tsx", scanSource("src/F.tsx", "<input /><input />", rules)]]),
      ),
    ).toThrow(/Refusing to raise/);
  });

  it("holds every family at or below its ceiling for the real source tree", () => {
    const counts = scanTree(projectRoot, rules);

    expect(counts.size).toBeGreaterThan(0);
    expect(checkRatchet(loadFamilies(ratchetDir), counts)).toEqual([]);
  });

  it("fails for a family entry outside allowedRemainders, an allowance without reason and a stale allowance", () => {
    const entry = { controls: 1, lookalikes: 0, literals: 0, reason: "converted in 69-00" };
    const sampleRules = {
      ...rules,
      allowedRemainders: [
        { file: "src/Allowed.tsx", reason: "editor internal (UI-SPEC allowance)" },
        { file: "src/Blank.tsx", reason: "  " },
        { file: "src/Stale.tsx", reason: "editor internal (UI-SPEC allowance)" },
      ],
    };
    const family = sampleFamily({
      "src/Allowed.tsx": entry,
      "src/Blank.tsx": entry,
      "src/Other.tsx": entry,
    });

    expect(checkAllowances([family], sampleRules)).toEqual([
      "sample: src/Other.tsx is on the ratchet but not in allowedRemainders",
      "allowedRemainders: src/Blank.tsx has no reason",
      "allowedRemainders: src/Stale.tsx is no longer on the ratchet; remove the allowance",
    ]);
  });

  it("holds only documented allowances on the ratchet of the real source tree", () => {
    expect(rules.allowedRemainders.length).toBeGreaterThan(0);
    expect(checkAllowances(loadFamilies(ratchetDir), rules)).toEqual([]);
  });

  it("holds every ceiling at the current count", () => {
    const counts = scanTree(projectRoot, rules);

    for (const family of loadFamilies(ratchetDir)) {
      expect(lowerCeilings(family, counts).files, `${family.family} ceilings`).toEqual(
        family.files,
      );
    }
  });

  it("has one family file for every family of the family map", () => {
    const files = loadFamilies(ratchetDir);

    expect(files.map((file) => file.family).sort()).toEqual(Object.keys(familyOwners).sort());
    for (const file of files) expect(file.owner).toBe(familyOwners[file.family]);
  });
});
