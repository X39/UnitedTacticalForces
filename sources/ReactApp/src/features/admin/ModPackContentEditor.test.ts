import { describe, expect, it } from "vitest";
import {
  type ModPackEditorValue,
  validateModPackEditor,
} from "@/features/admin/ModPackContentEditor";

const existingMod = {
  key: "catalog:one",
  modId: "one",
  pinnedVersionId: null,
  source: "42",
  title: "Catalog mod",
  providerIdentifier: "steam-workshop",
  usageRole: "Shared" as const,
  versionLabel: "Latest",
  versionSelector: "",
  installerIdentifier: "arma3-workshop",
  installerConfiguration: "{}",
  uploadFile: null,
};

describe("validateModPackEditor", () => {
  it("requires direct content or a nested mod pack", () => {
    expect(validateModPackEditor({ mods: [], dependencies: [] })).toBe(
      "Add at least one mod or nested mod pack.",
    );
  });

  it("accepts an existing catalog mod", () => {
    expect(validateModPackEditor({ mods: [existingMod], dependencies: [] })).toBeNull();
  });

  it("accepts a nested-only revision", () => {
    expect(
      validateModPackEditor({
        mods: [],
        dependencies: [{ definitionId: 7, title: "Base", pinnedRevisionId: null }],
      }),
    ).toBeNull();
  });

  it("validates new uploaded artifacts", () => {
    const value: ModPackEditorValue = {
      dependencies: [],
      mods: [
        {
          ...existingMod,
          key: "draft:uploaded",
          modId: null,
          providerIdentifier: "uploaded-artifact",
        },
      ],
    };
    expect(validateModPackEditor(value)).toBe("Uploaded artifacts need a ZIP file.");
  });

  it("rejects malformed installer configuration", () => {
    expect(
      validateModPackEditor({
        dependencies: [],
        mods: [{ ...existingMod, key: "draft:git", modId: null, installerConfiguration: "{" }],
      }),
    ).toBe("Installer configuration must be valid JSON.");
  });
});
