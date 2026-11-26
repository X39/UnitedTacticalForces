import { useQuery } from "@tanstack/react-query";
import { useEffect, useId, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { Disclosure } from "@/components/Disclosure";
import { RadioGroupField, SelectField } from "@/components/FormFields";
import { StatePanel, StatusBadge } from "@/components/primitives";
import { EmptyState } from "@/components/StateViews";
import { ApiText } from "@/components/server/ApiText";
import { ConfigurationEntryField } from "@/components/server/ConfigurationEntryField";
import { ConfigurationFilter } from "@/components/server/ConfigurationFilter";
import {
  chipKeys,
  countEntries,
  filterEntries,
  isFilterActive,
  isRequiredEmpty,
  type ConfigurationFilterMode,
} from "@/components/server/configurationFilterRules";
import { SettingsPanel } from "@/components/server/SettingsPanel";
import "@/components/server/configuration.css";
import {
  configurationContribution,
  configurationContributionField,
} from "./implementations/registry";
import { PlacementEstimate } from "./PlacementEstimate";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import { useServerChangeDraft } from "@/features/hosting/useServerChangeDraft";
import {
  configurationEntryIssue,
  type ConfigurationIssue,
  type StagedProfile,
} from "@/features/hosting/serverChangeDraft";
import type { GameConfigurationEntry } from "@/features/hosting/types";
import { configurationInputValue, parentServerPath } from "@/features/hosting/workspaceModel";

/** The number of entries from which the Configuration panel offers the search field and the chips. */
const filterToolbarThreshold = 8;

/**
 * Splits the entries into the Common part and the rest, as the API declares the tier.
 *
 * A game that declares no Common entry gets no split: every entry stays in one flat list, so a Common heading is
 * never an empty stub. An entry without a tier counts as Advanced, which is the API default.
 *
 * @param entries The configuration entries in API order.
 * @returns The entries of the leading list, the entries behind the Advanced disclosure and whether the split applies.
 */
function splitByTier(entries: readonly GameConfigurationEntry[]): {
  lead: GameConfigurationEntry[];
  rest: GameConfigurationEntry[];
  split: boolean;
} {
  const common = entries.filter((entry) => entry.tier === "Common");
  if (common.length === 0) return { lead: [...entries], rest: [], split: false };
  return { lead: common, rest: entries.filter((entry) => entry.tier !== "Common"), split: true };
}

/**
 * Groups entries by generated target file and then by their group name; the order inside a group follows `order`.
 * Control-plane entries share the empty target because they are not written to a game file.
 *
 * @param entries The entries to group.
 * @returns The entries by target and group, keeping the order in which targets and groups first appear.
 */
function groupByTarget(
  entries: readonly GameConfigurationEntry[],
): Map<string, Map<string, GameConfigurationEntry[]>> {
  const targets = new Map<string, Map<string, GameConfigurationEntry[]>>();
  for (const entry of entries) {
    const targetPath = entry.isControlPlaneOnly ? "" : entry.targetPath;
    const groups = targets.get(targetPath) ?? new Map<string, GameConfigurationEntry[]>();
    const values = groups.get(entry.group) ?? [];
    values.push(entry);
    groups.set(entry.group, values);
    targets.set(targetPath, groups);
  }
  return targets;
}

/**
 * The Configuration panel of the Settings section, a collapsible {@link SettingsPanel} with the id
 * `server-configuration`. Its summary names the number of settings once they are loaded; the loading and the error
 * state, with a retry, render inside the panel and never open it. Staging a value, a reset or a profile opens it.
 *
 * Common settings come first and every other setting sits behind an Advanced disclosure that opens by itself when
 * something inside needs attention. Every entry renders through the shared entry field and stages its change in the
 * Settings draft instead of saving it.
 *
 * @param props.serverId The server whose configuration is shown.
 * @param props.gameIdentifier The stable game identifier, used to find the game contribution.
 * @param props.canModify Whether the member may change the configuration.
 */
export function ServerConfigurationPanel({
  serverId,
  gameIdentifier,
  canModify,
}: {
  serverId: string;
  gameIdentifier?: string;
  canModify: boolean;
}) {
  const { t } = useTranslation();
  const configuration = useQuery({
    queryKey: hostingQueryKeys.configuration(serverId),
    queryFn: ({ signal }) => hostingService.configuration(serverId, signal),
  });
  const contribution = configurationContribution(gameIdentifier);
  const demand = useQuery({
    queryKey: hostingQueryKeys.storageDemand(serverId),
    queryFn: ({ signal }) => hostingService.storageDemand(serverId, signal),
    enabled: contribution?.showPlacementEstimate === true,
  });
  const options = useQuery({
    queryKey: hostingQueryKeys.configurationOptions(serverId),
    queryFn: ({ signal }) => hostingService.serverConfigurationProfileOptions(serverId, signal),
    enabled: canModify,
  });
  const {
    draft,
    saved,
    readOnly,
    applying,
    reviewAttempt,
    stageValue,
    stageReset,
    unstageSetting,
    stageProfile,
    unstageProfile,
  } = useServerChangeDraft();
  const revisionGroupName = useId();
  const [blurred, setBlurred] = useState<ReadonlySet<string>>(() => new Set());
  const [overriddenTargets, setOverriddenTargets] = useState<string[]>([]);
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [seenTriggers, setSeenTriggers] = useState("");
  const [query, setQuery] = useState("");
  const [mode, setMode] = useState<ConfigurationFilterMode>("all");
  const [pinned, setPinned] = useState<{
    mode: ConfigurationFilterMode;
    keys: ReadonlySet<string>;
  }>(() => ({ mode: "all", keys: new Set() }));
  const [seenReviewAttempt, setSeenReviewAttempt] = useState(0);
  const entries = configuration.data?.entries;
  const changedCount = draft.values.size + draft.resets.size + (draft.profile ? 1 : 0);
  const { lead, rest, split } = splitByTier(entries ?? []);
  // The toolbar is noise for a short list; below the threshold nothing is ever filtered.
  const showToolbar = (entries?.length ?? 0) >= filterToolbarThreshold;
  const filtering = showToolbar && isFilterActive(query, mode);
  // The chip keeps the entries it matched while it is pressed. Without this, typing the first character into a
  // required-empty field under Needs a value, or undoing a change under Changed, would unmount the row the member
  // is editing and drop the focus. The count behind the chip stays live; only the visible list is stable. The state
  // is adjusted during render, keyed on the chip and the keys that match it.
  const liveChipKeys = chipKeys(entries ?? [], mode, draft);
  if (pinned.mode !== mode || liveChipKeys.some((key) => !pinned.keys.has(key))) {
    setPinned({
      mode,
      keys: new Set(pinned.mode === mode ? [...pinned.keys, ...liveChipKeys] : liveChipKeys),
    });
  }
  const filtered = filtering
    ? filterEntries(
        entries ?? [],
        query,
        mode,
        draft,
        pinned.mode === mode ? pinned.keys : undefined,
      )
    : (entries ?? []);

  useEffect(() => {
    const loaded = entries ?? [];
    const targets = [
      ...new Set(
        loaded
          .filter((entry) => !entry.isControlPlaneOnly && entry.targetPath.trim())
          .map((entry) => entry.targetPath),
      ),
    ];
    if (targets.length === 0) return;
    let active = true;
    void Promise.all(
      targets.map(async (target) => {
        const files = await hostingService.files(serverId, "persistent", parentServerPath(target));
        return files.some(
          (file) =>
            file.path.toLowerCase() === target.toLowerCase() &&
            (file.overridesGenerated || file.configurationStatus === "Conflict"),
        )
          ? target
          : undefined;
      }),
    )
      .then((values) => {
        if (active) setOverriddenTargets(values.filter((value): value is string => Boolean(value)));
      })
      .catch(() => {
        if (active) setOverriddenTargets([]);
      });
    return () => {
      active = false;
    };
  }, [entries, serverId]);

  const markBlurred = (key: string) =>
    setBlurred((current) => (current.has(key) ? current : new Set(current).add(key)));
  const issueText = (issue: ConfigurationIssue): string => {
    switch (issue.code) {
      case "required":
        return t("Server_Settings_Invalid_Required");
      case "min":
        return t("Server_Settings_Invalid_Min", { min: issue.min });
      case "max":
        return t("Server_Settings_Invalid_Max", { max: issue.max });
      case "number":
        return t("Server_Settings_Invalid_Number");
      case "decimal":
        return t("Server_Settings_Invalid_Decimal");
      case "choice":
        return t("Server_Settings_Invalid_Choice");
    }
  };
  /** The error of an entry: only a staged value has one, shown after the field was left or a review was tried. */
  const entryError = (entry: GameConfigurationEntry): string | undefined => {
    const staged = draft.values.get(entry.key);
    if (staged === undefined || !(blurred.has(entry.key) || reviewAttempt > 0)) return undefined;
    const issue = configurationEntryIssue(entry, staged);
    return issue ? issueText(issue) : undefined;
  };

  // The Advanced disclosure opens by itself when a trigger newly appears inside it: a staged change, a required
  // entry without a value, or an invalid staged value after a review attempt. The member can still close it; it
  // reopens only for a trigger it has not seen yet. The state is adjusted during render, keyed on the trigger set.
  const triggers = rest.flatMap((entry) => {
    const staged = draft.values.get(entry.key);
    const found: string[] = [];
    if (staged !== undefined || draft.resets.has(entry.key)) found.push(`staged:${entry.key}`);
    if (isRequiredEmpty(entry, staged ?? configurationInputValue(entry)))
      found.push(`required:${entry.key}`);
    if (
      reviewAttempt > 0 &&
      staged !== undefined &&
      configurationEntryIssue(entry, staged) !== undefined
    )
      found.push(`invalid:${entry.key}:${reviewAttempt}`);
    return found;
  });
  const triggerSignature = triggers.join("\n");
  if (triggerSignature !== seenTriggers) {
    const known = new Set(seenTriggers.split("\n"));
    setSeenTriggers(triggerSignature);
    if (triggers.some((trigger) => !known.has(trigger))) setAdvancedOpen(true);
  }

  // A review attempt with an invalid staged entry that the filter hides clears the filter, so that the field is
  // rendered and the Settings section can focus the first invalid one. The state is adjusted during render.
  if (reviewAttempt !== seenReviewAttempt) {
    setSeenReviewAttempt(reviewAttempt);
    if (filtering) {
      const shown = new Set(filtered.map((entry) => entry.key));
      const hiddenInvalid = (entries ?? []).some((entry) => {
        const staged = draft.values.get(entry.key);
        return (
          !shown.has(entry.key) &&
          staged !== undefined &&
          configurationEntryIssue(entry, staged) !== undefined
        );
      });
      if (hiddenInvalid) {
        setQuery("");
        setMode("all");
      }
    }
  }

  // The header always renders, so the loading and the error state live in the body of the collapsed panel.
  const stagedKeys = [
    ...[...draft.values.keys()].map((key) => `value:${key}`),
    ...[...draft.resets].map((key) => `reset:${key}`),
    ...(draft.profile ? ["profile"] : []),
  ];
  const panelBadge =
    changedCount > 0 ? (
      <StatusBadge tone="info">
        {t("Server_Change_PanelCount", { count: changedCount })}
      </StatusBadge>
    ) : undefined;
  const loaded = !configuration.isLoading && !configuration.isError && configuration.data;
  const shell = (body: ReactNode): ReactNode => (
    <SettingsPanel
      id="server-configuration"
      title={t("Server_Settings_ConfigurationTitle")}
      summary={
        loaded ? t("Server_Settings_Summary_Settings", { count: loaded.entries.length }) : undefined
      }
      badge={panelBadge}
      stagedKeys={stagedKeys}
    >
      <div className="configuration-panel">{body}</div>
    </SettingsPanel>
  );
  if (configuration.isLoading)
    return shell(
      <StatePanel kind="loading">{t("React_ServerWorkspace_LoadingConfiguration")}</StatePanel>,
    );
  if (configuration.isError)
    return shell(
      <StatePanel
        kind="error"
        action={
          <ActionButton variant="ghost" onClick={() => void configuration.refetch()}>
            {t("Server_Settings_Retry")}
          </ActionButton>
        }
      >
        {t("Server_Settings_LoadError")}
      </StatePanel>,
    );
  if (!configuration.data) return null;
  const locked = !canModify || readOnly || applying;

  // D-01: the profile and its revision mode are staged like every other change; the saved profile and revision
  // come from the server record, and choosing them again removes the staged item.
  const chosen: StagedProfile = draft.profile ?? saved.profile;
  const chosenIsSaved = chosen.profileId !== null && chosen.profileId === saved.profile.profileId;
  const chosenOption =
    chosen.profileId === null
      ? undefined
      : options.data?.find((option) => option.primaryKey === chosen.profileId);
  const pinnedRevisionId = chosenIsSaved
    ? (saved.profile.revisionId ?? configuration.data.profileRevisionId ?? null)
    : (chosenOption?.latestRevisionId ?? null);
  const pinnedRevisionNumber = chosenIsSaved
    ? configuration.data.profileRevisionNumber
    : (chosenOption?.latestRevisionNumber ?? null);
  const stageOrClear = (next: StagedProfile) => {
    if (next.profileId === saved.profile.profileId && next.revisionId === saved.profile.revisionId)
      unstageProfile();
    else stageProfile(next);
  };
  const chooseProfile = (profileId: string) => {
    const next = profileId === "" ? null : profileId;
    if (next === saved.profile.profileId) unstageProfile();
    else stageProfile({ profileId: next, revisionId: null });
  };
  const chooseRevisionMode = (mode: string) => {
    if (chosen.profileId === null) return;
    stageOrClear({
      profileId: chosen.profileId,
      revisionId: mode === "pinned" ? pinnedRevisionId : null,
    });
  };

  /** Renders entries as target blocks of fieldset groups; a caption names the written file under the first legend. */
  const renderEntries = (list: readonly GameConfigurationEntry[], captions: boolean): ReactNode => (
    <div className="configuration-part">
      {[...groupByTarget(list)].map(([target, groups]) => (
        <div key={target} className="configuration-target">
          {overriddenTargets.includes(target) ? (
            <StatePanel kind="warning">
              {t("Server_Settings_PhysicalOverride", { path: target })}
            </StatePanel>
          ) : (
            [...groups].map(([group, groupEntries], groupIndex) => (
              <fieldset key={group} className="configuration-group">
                <legend className="configuration-group__legend">
                  <ApiText>{group}</ApiText>
                </legend>
                {captions && groupIndex === 0 && (
                  <p className="configuration-group__caption">
                    {target
                      ? t("Server_Settings_Target", { path: target })
                      : t("React_Placement_ControlPlane")}
                  </p>
                )}
                {[...groupEntries]
                  .sort((left, right) => left.order - right.order)
                  .map((entry) => {
                    const staged = draft.values.get(entry.key);
                    return (
                      <ConfigurationEntryField
                        key={entry.key}
                        entry={entry}
                        value={staged ?? configurationInputValue(entry)}
                        staged={staged !== undefined}
                        resetStaged={draft.resets.has(entry.key)}
                        error={entryError(entry)}
                        disabled={locked}
                        contribution={configurationContributionField(contribution, entry.key)}
                        onChange={(next) => stageValue(entry.key, next)}
                        onBlur={() => markBlurred(entry.key)}
                        onUndo={() => unstageSetting(entry.key)}
                        onReset={canModify ? () => stageReset(entry.key) : undefined}
                      />
                    );
                  })}
              </fieldset>
            ))
          )}
        </div>
      ))}
    </div>
  );
  const counts = countEntries(configuration.data.entries, draft);
  const clearFilter = () => {
    setQuery("");
    setMode("all");
  };
  const advancedChanged = rest.filter(
    (entry) => draft.values.has(entry.key) || draft.resets.has(entry.key),
  ).length;

  return shell(
    <>
      <p className="configuration-panel__description">
        {t("Server_Settings_ConfigurationDescription")}
      </p>
      <SelectField
        label={t("React_ServerWorkspace_ConfigurationProfile")}
        disabled={locked}
        value={chosen.profileId ?? ""}
        options={[
          { value: "", label: t("React_ServerWorkspace_GameDefaultsOnly") },
          ...(options.data ?? []).map((option) => ({
            value: option.primaryKey,
            label: option.title,
          })),
        ]}
        onChange={(event) => chooseProfile(event.target.value)}
      />
      {chosen.profileId !== null && (
        <RadioGroupField
          legend={t("Server_Settings_Revision_Legend")}
          name={revisionGroupName}
          layout="segmented"
          value={chosen.revisionId === null ? "latest" : "pinned"}
          disabled={locked}
          reason={pinnedRevisionId === null ? t("Server_Settings_Revision_Unavailable") : undefined}
          options={[
            { value: "latest", label: t("Server_Settings_Revision_Latest") },
            {
              value: "pinned",
              label: t("Server_Settings_Revision_Pinned", { revision: pinnedRevisionNumber ?? "" }),
              disabled: pinnedRevisionId === null,
            },
          ]}
          onChange={chooseRevisionMode}
        />
      )}
      {!configuration.data.isComplete && (
        <StatePanel kind="warning">{t("React_ServerWorkspace_ConfigurationIncomplete")}</StatePanel>
      )}
      {contribution?.showPlacementEstimate && (
        <PlacementEstimate
          estimate={demand.data?.placementEstimate}
          stagedNote={changedCount > 0}
          pending={applying || demand.isFetching || configuration.isFetching}
          unavailable={demand.isError}
        />
      )}
      {showToolbar && (
        <ConfigurationFilter
          query={query}
          mode={mode}
          changedCount={counts.changed}
          attentionCount={counts.attention}
          shownCount={filtered.length}
          onQueryChange={setQuery}
          onModeChange={setMode}
        />
      )}
      {(entries ?? []).length === 0 ? (
        <StatePanel kind="empty">{t("Server_Settings_Empty")}</StatePanel>
      ) : filtering ? (
        filtered.length === 0 ? (
          <EmptyState
            title={t("Server_Settings_Filter_EmptyTitle")}
            action={
              <ActionButton variant="ghost" onClick={clearFilter}>
                {t("Server_Settings_Filter_Clear")}
              </ActionButton>
            }
          >
            {t("Server_Settings_Filter_EmptyBody")}
          </EmptyState>
        ) : (
          renderEntries(filtered, true)
        )
      ) : split ? (
        <>
          <h3 className="configuration-panel__part-title">{t("Server_Settings_Common")}</h3>
          {renderEntries(lead, false)}
          {rest.length > 0 && (
            <Disclosure
              title={t("Server_Settings_Advanced_Title")}
              summary={
                advancedChanged > 0
                  ? t("Server_Settings_Advanced_SummaryChanged", {
                      count: rest.length,
                      changed: advancedChanged,
                    })
                  : t("Server_Settings_Advanced_Summary", { count: rest.length })
              }
              open={advancedOpen}
              onOpenChange={setAdvancedOpen}
            >
              {renderEntries(rest, true)}
            </Disclosure>
          )}
        </>
      ) : (
        renderEntries(lead, true)
      )}
    </>,
  );
}
