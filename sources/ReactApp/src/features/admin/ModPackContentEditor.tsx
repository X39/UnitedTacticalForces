import { useMutation, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { StatePanel, StatusBadge } from "@/components/primitives";
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
  const { t: translate } = useTranslation();
  const t = (key: string) => translate(key as never, { defaultValue: key });
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
            <span className="kicker">{t("Mods")}</span>
            <h3>{t("Catalog and source providers")}</h3>
          </div>
          <StatusBadge tone={value.mods.length ? "success" : "neutral"}>
            {value.mods.length}
          </StatusBadge>
        </header>
        <label className="field">
          <span>{t("Search mod catalog")}</span>
          <input value={catalogSearch} onChange={(event) => setCatalogSearch(event.target.value)} />
        </label>
        {catalog.isError ? (
          <StatePanel kind="error">{t("The mod catalog could not be loaded.")}</StatePanel>
        ) : (
          <div className="modpack-catalog" role="list" aria-label={t("Available mods")}>
            {(catalog.data ?? []).map((mod) => (
              <button
                className="modpack-catalog__item"
                type="button"
                role="listitem"
                key={mod.primaryKey}
                disabled={selectedIds.has(mod.primaryKey)}
                onClick={() => onChange({ ...value, mods: [...value.mods, fromCatalog(mod)] })}
              >
                <strong>{mod.title}</strong>
                <small>
                  {mod.providerIdentifier} · {mod.canonicalSourceIdentifier}
                </small>
                <span>{selectedIds.has(mod.primaryKey) ? t("Selected") : t("Add")}</span>
              </button>
            ))}
          </div>
        )}
        <div className="modpack-source-entry">
          <label className="field">
            <span>{t("Paste one source per line")}</span>
            <textarea
              rows={4}
              value={sourceText}
              placeholder="https://steamcommunity.com/sharedfiles/filedetails/?id=…"
              onChange={(event) => setSourceText(event.target.value)}
            />
          </label>
          <label className="field">
            <span>{t("Provider")}</span>
            <select
              value={providerIdentifier}
              onChange={(event) => setProviderIdentifier(event.target.value)}
            >
              <option value="">{t("Detect automatically")}</option>
              {bindings.map((binding) => (
                <option key={binding.providerIdentifier} value={binding.providerIdentifier}>
                  {binding.displayName}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>{t("Version, branch, tag, or commit")}</span>
            <input
              value={versionSelector}
              onChange={(event) => setVersionSelector(event.target.value)}
            />
          </label>
          <button
            className="button button--secondary"
            type="button"
            disabled={!sourceText.trim() || preview.isPending}
            onClick={() => preview.mutate()}
          >
            {preview.isPending ? t("Resolving…") : t("Review sources")}
          </button>
        </div>
        {preview.isError && (
          <StatePanel kind="error">
            {preview.error instanceof Error
              ? preview.error.message
              : t("Source resolution failed.")}
          </StatePanel>
        )}
        {(capability.modPackFormats?.length ?? 0) > 0 && (
          <div className="modpack-format-import">
            <strong>{t("Import a game mod-pack format")}</strong>
            {capability.modPackFormats?.map((format) => (
              <label className="file-drop" key={format.identifier}>
                <span>{format.displayName}</span>
                <small>
                  .{format.fileExtension} · {Math.floor(format.maximumSizeBytes / 1024)} KiB
                </small>
                <input
                  type="file"
                  accept={`.${format.fileExtension},${format.mediaType}`}
                  disabled={formatPreview.isPending}
                  onChange={(event) => {
                    const file = event.target.files?.[0];
                    if (file) formatPreview.mutate({ formatIdentifier: format.identifier, file });
                    event.target.value = "";
                  }}
                />
              </label>
            ))}
            {formatPreview.isError && (
              <StatePanel kind="error">
                {formatPreview.error instanceof Error
                  ? formatPreview.error.message
                  : t("Format import failed.")}
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
                        {mod.pinnedVersionId ? t("Pinned") : t("Latest")} · {mod.versionLabel} ·{" "}
                        {mod.installerIdentifier || t("Installer unavailable")}
                      </small>
                    )}
                  </div>
                  <StatusBadge tone={mod.modId ? "info" : "warning"}>
                    {mod.modId ? t("Catalog") : t("New catalog entry")}
                  </StatusBadge>
                </div>
                {!mod.modId && (
                  <div className="modpack-version-grid">
                    <label className="field">
                      <span>{t("Title")}</span>
                      <input
                        value={mod.title}
                        onChange={(event) => update(index, { title: event.target.value })}
                      />
                    </label>
                    <label className="field">
                      <span>{t("Usage")}</span>
                      <select
                        value={mod.usageRole}
                        onChange={(event) =>
                          update(index, {
                            usageRole: event.target.value as SelectedModContent["usageRole"],
                          })
                        }
                      >
                        <option value="Shared">{t("Shared")}</option>
                        <option value="ClientOnly">{t("Client only")}</option>
                        <option value="ServerOnly">{t("Server only")}</option>
                      </select>
                    </label>
                    <label className="field">
                      <span>{t("Version label")}</span>
                      <input
                        value={mod.versionLabel}
                        onChange={(event) => update(index, { versionLabel: event.target.value })}
                      />
                    </label>
                    {binding?.supportsVersionSelector && (
                      <label className="field">
                        <span>{t("Version selector")}</span>
                        <input
                          value={mod.versionSelector}
                          onChange={(event) =>
                            update(index, { versionSelector: event.target.value })
                          }
                        />
                      </label>
                    )}
                    <label className="field">
                      <span>{t("Installer")}</span>
                      <select
                        value={mod.installerIdentifier}
                        onChange={(event) =>
                          update(index, { installerIdentifier: event.target.value })
                        }
                      >
                        {(binding?.installerIdentifiers ?? []).map((installer) => (
                          <option key={installer}>{installer}</option>
                        ))}
                      </select>
                    </label>
                    <label className="field">
                      <span>{t("Installer configuration (JSON)")}</span>
                      <input
                        value={mod.installerConfiguration}
                        onChange={(event) =>
                          update(index, { installerConfiguration: event.target.value })
                        }
                      />
                    </label>
                    {mod.providerIdentifier === "uploaded-artifact" && (
                      <>
                        <label className="field">
                          <span>{t("ZIP artifact")}</span>
                          <input
                            type="file"
                            accept=".zip,application/zip"
                            onChange={(event) =>
                              update(index, { uploadFile: event.target.files?.[0] ?? null })
                            }
                          />
                        </label>
                      </>
                    )}
                  </div>
                )}
                <div className="modpack-order-actions">
                  <button
                    type="button"
                    className="button button--ghost"
                    disabled={index === 0}
                    onClick={() => move(index, -1)}
                    aria-label={t("Move mod up")}
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    className="button button--ghost"
                    disabled={index === value.mods.length - 1}
                    onClick={() => move(index, 1)}
                    aria-label={t("Move mod down")}
                  >
                    ↓
                  </button>
                  <button
                    type="button"
                    className="button button--ghost"
                    onClick={() =>
                      onChange({
                        ...value,
                        mods: value.mods.filter((_, candidate) => candidate !== index),
                      })
                    }
                  >
                    {t("Remove")}
                  </button>
                </div>
              </li>
            );
          })}
        </ol>
      </section>

      <section className="modpack-editor-section">
        <header className="command-panel__header">
          <div>
            <span className="kicker">{t("Nested mod packs")}</span>
            <h3>{t("Reusable dependencies")}</h3>
          </div>
          <StatusBadge>{value.dependencies.length}</StatusBadge>
        </header>
        <label className="field">
          <span>{t("Search mod packs")}</span>
          <input value={packSearch} onChange={(event) => setPackSearch(event.target.value)} />
        </label>
        <div className="modpack-selector__options" role="group" aria-label={t("Nested mod packs")}>
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
                <label key={id} className="modpack-selector__option">
                  <input
                    type="checkbox"
                    checked={checked}
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
                  <span>
                    <strong>{pack.title}</strong>
                    <small>
                      {t("Latest revision")} #{latest?.primaryKey}
                    </small>
                  </span>
                </label>
              );
            })}
        </div>
        {value.dependencies.map((dependency) => (
          <label className="field" key={dependency.definitionId}>
            <span>
              {dependency.title} · {t("Optional pinned revision")}
            </span>
            <input
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
          </label>
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
