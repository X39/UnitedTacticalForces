import { useMutation, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { FileChooserButton } from "@/components/FileChooserButton";
import { CheckboxField, SelectField, TextAreaField, TextField } from "@/components/FormFields";
import { StatePanel, StatusBadge } from "@/components/primitives";
import { SearchField } from "@/components/SearchField";
import { ErrorState } from "@/components/StateViews";
import { toneOf } from "@/components/statusTones";
import { assetReadinessTones, modOriginTones } from "@/features/admin/assetTones";
import {
  latestActiveRevision,
  modPackQueryKeys,
  modPackService,
  type ModCatalogItem,
  type ModProviderBinding,
  type ModSourceDraft,
  type NormalizedGameCapability,
} from "@/features/admin/modPacks";

export interface SelectedModContent {
  key: string;
  modId: string | null;
  pinnedVersionId: string | null;
  source: string;
  title: string;
  providerIdentifier: string;
  usageRole: "Shared" | "ClientOnly" | "ServerOnly";
  versionLabel: string;
  versionSelector: string;
  installerIdentifier: string;
  installerConfiguration: string;
  uploadFile: File | null;
}

export interface SelectedNestedPack {
  definitionId: number;
  title: string;
  pinnedRevisionId: number | null;
}

export interface ModPackEditorValue {
  mods: SelectedModContent[];
  dependencies: SelectedNestedPack[];
}

/**
 * Example source address shown inside the source text area.
 *
 * It is a locale independent URL shape rather than translated copy, so it stays a constant instead of a locale key.
 */
const sourcePlaceholder = "https://steamcommunity.com/sharedfiles/filedetails/?id=…";

const uploadedArtifactCache = new WeakMap<File, { primaryKey: string; versionId: string }>();

function fromCatalog(mod: ModCatalogItem): SelectedModContent {
  const latest = mod.versions[0];
  return {
    key: `catalog:${mod.primaryKey}`,
    modId: mod.primaryKey,
    pinnedVersionId: null,
    source: mod.canonicalSourceIdentifier,
    title: mod.title,
    providerIdentifier: mod.providerIdentifier,
    usageRole: mod.usageRole,
    versionLabel: latest?.versionLabel ?? "Latest",
    versionSelector: latest?.versionSelector ?? "",
    installerIdentifier: latest?.installerIdentifier ?? "",
    installerConfiguration: latest?.installerConfiguration ?? "{}",
    uploadFile: null,
  };
}

function fromPreview(draft: ModSourceDraft, binding: ModProviderBinding): SelectedModContent {
  return {
    key: draft.existingModId
      ? `catalog:${draft.existingModId}`
      : `draft:${draft.providerIdentifier}:${draft.canonicalSourceIdentifier}`,
    modId: draft.existingModId,
    pinnedVersionId: null,
    source: draft.input,
    title: draft.title,
    providerIdentifier: draft.providerIdentifier,
    usageRole: "Shared",
    versionLabel: draft.versionSelector || "Latest",
    versionSelector: draft.versionSelector,
    installerIdentifier: draft.installerIdentifiers[0] ?? binding.installerIdentifiers[0] ?? "",
    installerConfiguration: "{}",
    uploadFile: null,
  };
}

export function ModPackContentEditor({
  capability,
  value,
  onChange,
  excludeDefinitionId,
}: {
  capability: NormalizedGameCapability;
  value: ModPackEditorValue;
  onChange(value: ModPackEditorValue): void;
  excludeDefinitionId?: number;
}) {
  const { t } = useTranslation();
  const [catalogSearch, setCatalogSearch] = useState("");
  const [packSearch, setPackSearch] = useState("");
  const [sourceText, setSourceText] = useState("");
  const [providerIdentifier, setProviderIdentifier] = useState("");
  const [versionSelector, setVersionSelector] = useState("");
  const [importWarnings, setImportWarnings] = useState<string[]>([]);
  const bindings = capability.modProviderBindings ?? [];
  const selectedIds = useMemo(
    () => new Set(value.mods.map((mod) => mod.modId).filter(Boolean)),
    [value.mods],
  );
  const catalog = useQuery({
    queryKey: modPackQueryKeys.mods(capability.gameIdentifier ?? "", catalogSearch.trim()),
    queryFn: ({ signal }) =>
      modPackService.mods(capability.gameIdentifier ?? "", catalogSearch.trim(), signal),
    enabled: Boolean(capability.gameIdentifier),
  });
  const packs = useQuery({
    queryKey: modPackQueryKeys.selector(capability.gameIdentifier ?? "", packSearch.trim()),
    queryFn: ({ signal }) =>
      modPackService.list(
        {
          skip: 0,
          take: 100,
          myModPacksOnly: false,
          gameIdentifier: capability.gameIdentifier ?? "",
          ...(packSearch.trim() ? { search: packSearch.trim() } : {}),
        },
        signal,
      ),
    enabled: Boolean(capability.gameIdentifier),
  });
  const preview = useMutation({
    mutationFn: () => {
      const sources = sourceText
        .split(/\r?\n/)
        .map((input) => input.trim())
        .filter(Boolean)
        .map((input) => ({
          input,
          ...(providerIdentifier ? { providerIdentifier } : {}),
          ...(versionSelector.trim() ? { versionSelector: versionSelector.trim() } : {}),
        }));
      return modPackService.previewSources(capability.gameIdentifier ?? "", sources);
    },
    onSuccess: (drafts) => {
      const additions = drafts.flatMap((draft) => {
        const binding = bindings.find(
          (candidate) => candidate.providerIdentifier === draft.providerIdentifier,
        );
        return binding ? [fromPreview(draft, binding)] : [];
      });
      const keys = new Set(value.mods.map((mod) => mod.key));
      onChange({
        ...value,
        mods: [...value.mods, ...additions.filter((item) => !keys.has(item.key))],
      });
      setSourceText("");
    },
  });
  const formatPreview = useMutation({
    mutationFn: ({ formatIdentifier, file }: { formatIdentifier: string; file: File }) =>
      modPackService.previewFormat(capability.gameIdentifier ?? "", formatIdentifier, file),
    onSuccess: (result) => {
      const additions = result.entries.flatMap((draft) => {
        const binding = bindings.find(
          (candidate) => candidate.providerIdentifier === draft.providerIdentifier,
        );
        return binding ? [fromPreview(draft, binding)] : [];
      });
      const keys = new Set(value.mods.map((mod) => mod.key));
      onChange({
        ...value,
        mods: [...value.mods, ...additions.filter((item) => !keys.has(item.key))],
      });
      setImportWarnings(result.warnings);
    },
  });
  const move = (index: number, offset: number) => {
    const target = index + offset;
    if (target < 0 || target >= value.mods.length) return;
    const mods = [...value.mods];
    const current = mods[index];
    const other = mods[target];
    if (!current || !other) return;
    mods[index] = other;
    mods[target] = current;
    onChange({ ...value, mods });
  };
  const update = (index: number, patch: Partial<SelectedModContent>) => {
    onChange({
      ...value,
      mods: value.mods.map((mod, candidate) => (candidate === index ? { ...mod, ...patch } : mod)),
    });
  };
  const selectedPackIds = new Set(value.dependencies.map((dependency) => dependency.definitionId));

  return (
    <div className="modpack-content-editor">
      <section className="modpack-editor-section">
        <header className="command-panel__header">
          <div>
            <span className="kicker">{t("React_ModPackEditor_Mods")}</span>
            <h3>{t("React_ModPackEditor_CatalogAndSources")}</h3>
          </div>
          <StatusBadge tone={toneOf(assetReadinessTones, value.mods.length ? "ready" : "optional")}>
            {value.mods.length}
          </StatusBadge>
        </header>
        <SearchField
          label={t("React_ModPackEditor_SearchCatalog")}
          placeholder={t("React_ModPackEditor_SearchCatalog")}
          clearLabel={t("Common_SearchClear")}
          value={catalogSearch}
          onChange={setCatalogSearch}
        />
        {catalog.isError ? (
          <ErrorState onRetry={() => void catalog.refetch()}>
            {t("React_ModPackEditor_CatalogLoadError")}
          </ErrorState>
        ) : (
          <div
            className="modpack-catalog"
            role="list"
            aria-label={t("React_ModPackEditor_AvailableMods")}
          >
            {(catalog.data ?? []).map((mod) => (
              <div role="listitem" key={mod.primaryKey}>
                <ActionButton
                  variant="ghost"
                  className="modpack-catalog__item"
                  disabled={selectedIds.has(mod.primaryKey)}
                  onClick={() => onChange({ ...value, mods: [...value.mods, fromCatalog(mod)] })}
                >
                  <strong>{mod.title}</strong>
                  <small>
                    {mod.providerIdentifier} · {mod.canonicalSourceIdentifier}
                  </small>
                  <span>{selectedIds.has(mod.primaryKey) ? t("Selected") : t("Add")}</span>
                </ActionButton>
              </div>
            ))}
          </div>
        )}
        <div className="modpack-source-entry">
          <TextAreaField
            label={t("React_ModPackEditor_PasteSources")}
            rows={4}
            value={sourceText}
            placeholder={sourcePlaceholder}
            onChange={(event) => setSourceText(event.target.value)}
          />
          <SelectField
            label={t("Provider")}
            value={providerIdentifier}
            options={[
              { value: "", label: t("React_ModPackEditor_DetectAutomatically") },
              ...bindings.map((binding) => ({
                value: binding.providerIdentifier,
                label: binding.displayName,
              })),
            ]}
            onChange={(event) => setProviderIdentifier(event.target.value)}
          />
          <TextField
            label={t("React_ModPackEditor_VersionSelectorHint")}
            value={versionSelector}
            onChange={(event) => setVersionSelector(event.target.value)}
          />
          <ActionButton
            variant="ghost"
            disabled={!sourceText.trim() || preview.isPending}
            onClick={() => preview.mutate()}
          >
            {preview.isPending
              ? t("React_ModPackEditor_Resolving")
              : t("React_ModPackEditor_ReviewSources")}
          </ActionButton>
        </div>
        {preview.isError && (
          <StatePanel kind="error">
            {preview.error instanceof Error
              ? preview.error.message
              : t("React_ModPackEditor_SourceResolutionFailed")}
          </StatePanel>
        )}
        {(capability.modPackFormats?.length ?? 0) > 0 && (
          <div className="modpack-format-import">
            <strong>{t("React_ModPackEditor_ImportFormat")}</strong>
            {capability.modPackFormats?.map((format) => (
              <div className="modpack-format-import__item" key={format.identifier}>
                <FileChooserButton
                  inputLabel={format.displayName}
                  disabled={formatPreview.isPending}
                  onFiles={(files) => {
                    const file = files[0];
                    if (file) formatPreview.mutate({ formatIdentifier: format.identifier, file });
                  }}
                >
                  {format.displayName}
                </FileChooserButton>
                <small>
                  .{format.fileExtension} · {Math.floor(format.maximumSizeBytes / 1024)} KiB
                </small>
              </div>
            ))}
            {formatPreview.isError && (
              <StatePanel kind="error">
                {formatPreview.error instanceof Error
                  ? formatPreview.error.message
                  : t("React_ModPackEditor_FormatImportFailed")}
              </StatePanel>
            )}
            {importWarnings.map((warning) => (
              <StatePanel kind="warning" key={warning}>
                {warning}
              </StatePanel>
            ))}
          </div>
        )}
        <ol className="modpack-selected-content">
          {value.mods.map((mod, index) => {
            const binding = bindings.find(
              (item) => item.providerIdentifier === mod.providerIdentifier,
            );
            return (
              <li key={mod.key}>
                <div className="modpack-selected-content__heading">
                  <div>
                    <strong>{mod.title}</strong>
                    <small>
                      {mod.providerIdentifier} · {mod.source}
                    </small>
                    {mod.modId && (
                      <small>
                        {mod.pinnedVersionId ? t("React_ModPackEditor_Pinned") : t("Latest")} ·{" "}
                        {mod.versionLabel} ·{" "}
                        {mod.installerIdentifier || t("React_ModPackEditor_InstallerUnavailable")}
                      </small>
                    )}
                  </div>
                  <StatusBadge tone={toneOf(modOriginTones, mod.modId ? "catalog" : "new-entry")}>
                    {mod.modId
                      ? t("React_ModPackEditor_Catalog")
                      : t("React_ModPackEditor_NewCatalogEntry")}
                  </StatusBadge>
                </div>
                {!mod.modId && (
                  <div className="modpack-version-grid">
                    <TextField
                      label={t("Title")}
                      value={mod.title}
                      onChange={(event) => update(index, { title: event.target.value })}
                    />
                    <SelectField
                      label={t("React_ModPackEditor_Usage")}
                      value={mod.usageRole}
                      options={[
                        { value: "Shared", label: t("React_ModPackEditor_Shared") },
                        { value: "ClientOnly", label: t("React_ModPackEditor_ClientOnly") },
                        { value: "ServerOnly", label: t("React_ModPackEditor_ServerOnly") },
                      ]}
                      onChange={(event) =>
                        update(index, {
                          usageRole: event.target.value as SelectedModContent["usageRole"],
                        })
                      }
                    />
                    <TextField
                      label={t("React_ModPackEditor_VersionLabel")}
                      value={mod.versionLabel}
                      onChange={(event) => update(index, { versionLabel: event.target.value })}
                    />
                    {binding?.supportsVersionSelector && (
                      <TextField
                        label={t("React_ModPackEditor_VersionSelector")}
                        value={mod.versionSelector}
                        onChange={(event) => update(index, { versionSelector: event.target.value })}
                      />
                    )}
                    <SelectField
                      label={t("React_ModPackEditor_Installer")}
                      value={mod.installerIdentifier}
                      options={(binding?.installerIdentifiers ?? []).map((installer) => ({
                        value: installer,
                        label: installer,
                      }))}
                      onChange={(event) =>
                        update(index, { installerIdentifier: event.target.value })
                      }
                    />
                    <TextField
                      label={t("React_ModPackEditor_InstallerConfiguration")}
                      value={mod.installerConfiguration}
                      onChange={(event) =>
                        update(index, { installerConfiguration: event.target.value })
                      }
                    />
                    {mod.providerIdentifier === "uploaded-artifact" && (
                      <TextField
                        type="file"
                        label={t("React_ModPackEditor_ZipArtifact")}
                        accept=".zip,application/zip"
                        onChange={(event) =>
                          update(index, { uploadFile: event.target.files?.[0] ?? null })
                        }
                      />
                    )}
                  </div>
                )}
                <div className="modpack-order-actions">
                  <ActionButton
                    variant="ghost"
                    iconOnly
                    disabled={index === 0}
                    onClick={() => move(index, -1)}
                    aria-label={t("React_ModPackEditor_MoveModUp")}
                  >
                    ↑
                  </ActionButton>
                  <ActionButton
                    variant="ghost"
                    iconOnly
                    disabled={index === value.mods.length - 1}
                    onClick={() => move(index, 1)}
                    aria-label={t("React_ModPackEditor_MoveModDown")}
                  >
                    ↓
                  </ActionButton>
                  <ActionButton
                    variant="ghost"
                    onClick={() =>
                      onChange({
                        ...value,
                        mods: value.mods.filter((_, candidate) => candidate !== index),
                      })
                    }
                  >
                    {t("Remove")}
                  </ActionButton>
                </div>
              </li>
            );
          })}
        </ol>
      </section>

      <section className="modpack-editor-section">
        <header className="command-panel__header">
          <div>
            <span className="kicker">{t("React_ModPackEditor_NestedModPacks")}</span>
            <h3>{t("React_ModPackEditor_ReusableDependencies")}</h3>
          </div>
          <StatusBadge>{value.dependencies.length}</StatusBadge>
        </header>
        <SearchField
          label={t("Search mod packs")}
          placeholder={t("Search mod packs")}
          clearLabel={t("Common_SearchClear")}
          value={packSearch}
          onChange={setPackSearch}
        />
        <div
          className="modpack-selector__options"
          role="group"
          aria-label={t("React_ModPackEditor_NestedModPacks")}
        >
          {(packs.data ?? [])
            .filter(
              (pack) =>
                pack.primaryKey !== excludeDefinitionId &&
                latestActiveRevision(pack)?.primaryKey != null,
            )
            .map((pack) => {
              const id = pack.primaryKey;
              if (id == null) return null;
              const checked = selectedPackIds.has(id);
              const latest = latestActiveRevision(pack);
              return (
                <div key={id} className="modpack-selector__option">
                  <CheckboxField
                    checked={checked}
                    label={
                      <span className="modpack-selector__label">
                        <strong>{pack.title}</strong>
                        <small>
                          {t("Latest revision")} #{latest?.primaryKey}
                        </small>
                      </span>
                    }
                    onChange={(event) =>
                      onChange({
                        ...value,
                        dependencies: event.target.checked
                          ? [
                              ...value.dependencies,
                              {
                                definitionId: id,
                                title: pack.title ?? `#${id}`,
                                pinnedRevisionId: null,
                              },
                            ]
                          : value.dependencies.filter((item) => item.definitionId !== id),
                      })
                    }
                  />
                </div>
              );
            })}
        </div>
        {value.dependencies.map((dependency) => (
          <TextField
            key={dependency.definitionId}
            label={`${dependency.title} · ${t("React_ModPackEditor_OptionalPinnedRevision")}`}
            inputMode="numeric"
            value={dependency.pinnedRevisionId ?? ""}
            onChange={(event) =>
              onChange({
                ...value,
                dependencies: value.dependencies.map((item) =>
                  item.definitionId === dependency.definitionId
                    ? {
                        ...item,
                        pinnedRevisionId: event.target.value ? Number(event.target.value) : null,
                      }
                    : item,
                ),
              })
            }
          />
        ))}
      </section>
    </div>
  );
}

export function validateModPackEditor(value: ModPackEditorValue): string | null {
  if (!value.mods.length && !value.dependencies.length)
    return "Add at least one mod or nested mod pack.";
  for (const mod of value.mods) {
    if (mod.modId) continue;
    if (!mod.title.trim() || !mod.installerIdentifier || !mod.versionLabel.trim())
      return "Every new mod needs a title, version label, and installer.";
    try {
      JSON.parse(mod.installerConfiguration);
    } catch {
      return "Installer configuration must be valid JSON.";
    }
    if (mod.providerIdentifier === "uploaded-artifact" && !mod.uploadFile)
      return "Uploaded artifacts need a ZIP file.";
  }
  return null;
}

export async function materializeRevision(
  gameIdentifier: string,
  value: ModPackEditorValue,
): Promise<import("@/features/admin/modPacks").NormalizedModPackRevisionInput> {
  const mods = [] as Array<{ modId: string; pinnedVersionId: string | null }>;
  const newMods =
    [] as import("@/features/admin/modPacks").NormalizedModPackRevisionInput["newMods"];
  for (const mod of value.mods) {
    if (mod.modId) {
      mods.push({ modId: mod.modId, pinnedVersionId: mod.pinnedVersionId });
      continue;
    }
    if (mod.providerIdentifier === "uploaded-artifact" && mod.uploadFile) {
      const created =
        uploadedArtifactCache.get(mod.uploadFile) ??
        (await modPackService.createUploadedArtifact({
          gameIdentifier,
          title: mod.title.trim(),
          usageRole: mod.usageRole,
          installerIdentifier: mod.installerIdentifier,
          file: mod.uploadFile,
        }));
      uploadedArtifactCache.set(mod.uploadFile, created);
      mods.push({ modId: created.primaryKey, pinnedVersionId: created.versionId });
      continue;
    }
    newMods.push({
      title: mod.title.trim(),
      providerIdentifier: mod.providerIdentifier,
      source: mod.source,
      versionSelector: mod.versionSelector.trim(),
      usageRole: mod.usageRole,
      installerIdentifier: mod.installerIdentifier,
      installerConfiguration: mod.installerConfiguration,
    });
  }
  return {
    mods,
    newMods,
    dependencies: value.dependencies.map(({ definitionId, pinnedRevisionId }) => ({
      definitionId,
      pinnedRevisionId,
    })),
  };
}
