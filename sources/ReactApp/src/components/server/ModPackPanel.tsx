import { useQuery } from "@tanstack/react-query";
import type { TFunction } from "i18next";
import { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { Disclosure } from "@/components/Disclosure";
import { SelectField, type SelectOption } from "@/components/FormFields";
import { StatePanel, StatusBadge } from "@/components/primitives";
import { ApiText } from "@/components/server/ApiText";
import { SettingsPanel } from "@/components/server/SettingsPanel";
import "@/components/server/mods.css";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, ServerModPackOption } from "@/features/hosting/types";
import { useServerChangeDraft } from "@/features/hosting/useServerChangeDraft";

/** Stands in for a pack title while a translated line is built, so the title can be wrapped in {@link ApiText}. */
const titleMarker = "⁣title⁣";

/**
 * Renders a translated line that was built with {@link titleMarker} and puts the API title in {@link ApiText}.
 *
 * @param line The translated line in which the title was replaced by the marker.
 * @param title The API-provided pack title.
 * @returns The line with the title wrapped, or the plain line when the marker is missing.
 */
function lineWithTitle(line: string, title: string): ReactNode {
  const [before, ...rest] = line.split(titleMarker);
  if (rest.length === 0) return line;
  return (
    <>
      {before}
      <ApiText>{title}</ApiText>
      {rest.join(titleMarker)}
    </>
  );
}

/** How many mods the contents disclosure lists before the member asks for all of them. */
const previewSize = 10;

/**
 * The contents of the effective mod pack as a collapsed disclosure, or the one line that explains why there is none.
 *
 * The contents load as soon as the pack is chosen and share their cache entry with the review comparison. The
 * disclosure title carries the pack title and the number of mods; it lists the first mods and offers Show all.
 *
 * @param props.serverId The server the pack is selected for.
 * @param props.packId The pack whose contents are shown.
 * @param props.title The pack title.
 */
function ModPackContents({
  serverId,
  packId,
  title,
}: {
  serverId: string;
  packId: number;
  title: string;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [showAll, setShowAll] = useState(false);
  const contents = useQuery({
    queryKey: hostingQueryKeys.modPackContents(serverId, packId),
    queryFn: ({ signal }) => hostingService.modPackContents(serverId, packId, signal),
  });
  if (contents.isPending)
    return <p className="mods-panel__line">{t("Server_Mods_Contents_Loading")}</p>;
  if (contents.isError)
    return <p className="mods-panel__line">{t("Server_Mods_Contents_Failed")}</p>;
  if (!contents.data.isAvailable)
    return <p className="mods-panel__line">{t("Server_Mods_Contents_Unknown")}</p>;
  const items = contents.data.items;
  if (items.length === 0)
    return <p className="mods-panel__line">{t("Server_Mods_Contents_Empty")}</p>;
  const visible = showAll ? items : items.slice(0, previewSize);
  return (
    <Disclosure
      title={lineWithTitle(
        t("Server_Mods_Contents_Title", { title: titleMarker, count: contents.data.total }),
        title,
      )}
      open={open}
      onOpenChange={setOpen}
    >
      <ul className="mods-panel__list">
        {visible.map((item, index) => (
          <li key={`${item.key}:${index}`}>
            <ApiText>{item.title}</ApiText>
          </li>
        ))}
      </ul>
      {items.length > previewSize && (
        <ActionButton
          variant="ghost"
          size="compact"
          aria-expanded={showAll}
          onClick={() => setShowAll((current) => !current)}
        >
          {showAll
            ? t("Server_Mods_Diff_ShowFewer")
            : t("Server_Mods_Diff_ShowAll", { count: items.length })}
        </ActionButton>
      )}
    </Disclosure>
  );
}

/**
 * The packs the API marked as not available, each with the reason it sends or the fallback copy.
 *
 * @param props.packs The unavailable packs; the disclosure is omitted when there are none.
 */
function UnavailablePacks({ packs }: { packs: readonly ServerModPackOption[] }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  if (packs.length === 0) return null;
  return (
    <Disclosure
      title={t("Server_Mods_UnavailableList", { count: packs.length })}
      open={open}
      onOpenChange={setOpen}
    >
      <ul className="mods-panel__list mods-panel__list--packs">
        {packs.map((pack) => (
          <li key={pack.primaryKey}>
            <span className="mods-panel__pack-title">
              <ApiText>{pack.title}</ApiText>
            </span>
            <span className="mods-panel__pack-reason">
              {pack.unavailableReason ? (
                <ApiText>{pack.unavailableReason}</ApiText>
              ) : (
                t("Server_Mods_UnavailableFallback")
              )}
            </span>
          </li>
        ))}
      </ul>
    </Disclosure>
  );
}

/**
 * Builds the options of the mod pack chooser.
 *
 * The option for no mod pack comes first and only exists when the game does not require a pack. Available packs
 * follow, with the saved pack marked as current; the saved pack stays selectable even when it is no longer
 * available, so the select never shows a value it cannot represent. Unavailable packs come last and are disabled.
 *
 * @param options The mod pack options of the server.
 * @param savedId The saved mod pack, or null.
 * @param savedTitle The saved pack title when the options do not list the saved pack.
 * @param requiresModPack Whether the game requires a mod pack.
 * @param chosenId The effective selection, used to represent an unselected required pack.
 * @param t The translation function.
 * @returns The select options in display order.
 */
function chooserOptions(
  options: readonly ServerModPackOption[],
  savedId: number | null,
  savedTitle: string | null,
  requiresModPack: boolean,
  chosenId: number | null,
  t: TFunction,
): SelectOption[] {
  const current = ` ${t("Server_Mods_Current")}`;
  const result: SelectOption[] = [];
  if (!requiresModPack) result.push({ value: "", label: t("Hosting_Create_ModPackNone") });
  else if (chosenId === null)
    result.push({ value: "", label: t("Hosting_Create_ModPackNone"), disabled: true });
  const selectable = options.filter(
    (option) => option.isAvailable !== false || option.primaryKey === savedId,
  );
  for (const option of selectable) {
    result.push({
      value: String(option.primaryKey),
      label: option.primaryKey === savedId ? `${option.title}${current}` : option.title,
      lang: "en",
    });
  }
  if (savedId !== null && !options.some((option) => option.primaryKey === savedId)) {
    result.push({
      value: String(savedId),
      label: `${savedTitle ?? String(savedId)}${current}`,
      lang: "en",
    });
  }
  const unavailable = ` ${t("Server_Mods_Unavailable")}`;
  for (const option of options) {
    if (option.isAvailable === false && option.primaryKey !== savedId) {
      result.push({
        value: String(option.primaryKey),
        label: `${option.title}${unavailable}`,
        disabled: true,
        lang: "en",
      });
    }
  }
  return result;
}

/**
 * The Mods panel of the Settings section: a capability-driven chooser for the mod pack of the server.
 *
 * The panel is a collapsible {@link SettingsPanel} whose summary line names the saved pack, or the staged pack while a
 * change is staged, and whose badge counts the staged change. It exists only for games whose capability supports mod
 * packs and contains no game-specific rule. Choosing
 * another pack stages the change in the Settings draft; choosing the saved pack again removes it. Nothing is saved
 * before the member applies the staged changes from the review.
 *
 * @param props.server The server whose mod pack is shown.
 */
export function ModPackPanel({ server }: { server: GameServer }) {
  const { t } = useTranslation();
  const { draft, saved, readOnly, applying, stageModPack, unstageModPack } = useServerChangeDraft();
  const capabilities = useQuery({
    queryKey: hostingQueryKeys.capabilities,
    queryFn: ({ signal }) => hostingService.capabilities(signal),
  });
  const capability = capabilities.data?.find(
    (item) => item.gameIdentifier.toLowerCase() === server.gameIdentifier.toLowerCase(),
  );
  const supported = capability !== undefined && capability.supportsModPacks !== false;
  const packs = useQuery({
    queryKey: hostingQueryKeys.modPackOptions(server.primaryKey),
    queryFn: ({ signal }) => hostingService.modPackOptions(server.primaryKey, signal),
    enabled: supported,
  });
  if (!supported) return null;

  const savedId = saved.modPackId;
  const staged = draft.modPack !== null;
  const chosenId = draft.modPack ? draft.modPack.to : savedId;
  const options = packs.data ?? [];
  const titleOf = (id: number | null): string | null =>
    id === null ? null : (options.find((option) => option.primaryKey === id)?.title ?? null);
  const savedTitle = titleOf(savedId) ?? server.selectedModPackDefinitionTitle ?? null;
  const chosenTitle = titleOf(chosenId) ?? (chosenId === savedId ? savedTitle : null);
  const choose = (value: string) => {
    const next = value === "" ? null : Number(value);
    if (next === savedId) unstageModPack();
    else stageModPack(next);
  };

  const summaryTitle = chosenId === null ? null : (chosenTitle ?? String(chosenId));
  return (
    <SettingsPanel
      id="server-mods"
      title={t("Server_Mods_Title")}
      summary={<bdi>{summaryTitle ?? t("Hosting_Create_ModPackNone")}</bdi>}
      badge={
        staged ? (
          <StatusBadge tone="info">{t("Server_Change_PanelCount", { count: 1 })}</StatusBadge>
        ) : undefined
      }
      stagedKeys={staged ? ["modPack"] : []}
    >
      <div className="mods-panel">
        <p className="mods-panel__description">{t("Server_Mods_Description")}</p>
        {packs.isLoading ? (
          <StatePanel kind="loading">{t("Server_Mods_Loading")}</StatePanel>
        ) : packs.isError ? (
          <StatePanel
            kind="error"
            action={
              <ActionButton variant="ghost" onClick={() => void packs.refetch()}>
                {t("Server_Settings_Retry")}
              </ActionButton>
            }
          >
            {t("Server_Mods_LoadError")}
          </StatePanel>
        ) : options.length === 0 ? (
          <StatePanel kind="empty">{t("Server_Mods_Empty")}</StatePanel>
        ) : (
          <>
            <SelectField
              label={t("Server_Create_ModPack_Label")}
              value={chosenId === null ? "" : String(chosenId)}
              disabled={readOnly || applying}
              options={chooserOptions(
                options,
                savedId,
                savedTitle,
                capability?.requiresModPack === true,
                chosenId,
                t,
              )}
              onChange={(event) => choose(event.target.value)}
            />
            <p className="mods-panel__line">
              {savedTitle === null || savedId === null
                ? t("Server_Mods_CurrentNone")
                : lineWithTitle(t("Server_Mods_CurrentLine", { title: titleMarker }), savedTitle)}
            </p>
            {staged && (
              <p className="mods-panel__line">
                {chosenId === null
                  ? t("Server_Mods_StagedLine", { title: t("Hosting_Create_ModPackNone") })
                  : lineWithTitle(
                      t("Server_Mods_StagedLine", { title: titleMarker }),
                      chosenTitle ?? String(chosenId),
                    )}
              </p>
            )}
            {chosenId !== null && (
              <ModPackContents
                key={chosenId}
                serverId={server.primaryKey}
                packId={chosenId}
                title={chosenTitle ?? String(chosenId)}
              />
            )}
            <UnavailablePacks packs={options.filter((option) => option.isAvailable === false)} />
          </>
        )}
      </div>
    </SettingsPanel>
  );
}
