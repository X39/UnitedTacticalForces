/**
 * The staged-change draft of the server Settings section.
 *
 * This module is pure: it has no React, no persistence of any kind, no logging and it never reads the text of a
 * thrown error. Every function takes a draft and returns a new draft; nothing is mutated in place. The draft
 * holds only changes a member has staged (mod pack, configuration profile and revision mode, configuration
 * values, resets and credential changes). File operations are applied immediately and never enter the draft.
 */
import { ApiError } from "@/api/client";
import type {
  ConfigurationValue,
  GameConfigurationEntry,
  GameServerSecretStatus,
} from "@/features/hosting/types";
import {
  configurationInputValue,
  parseConfigurationEntryValue,
} from "@/features/hosting/workspaceModel";

/**
 * A staged credential change. A "set" carries the new value typed by the member; a "rotate" asks the server to
 * generate a new value.
 */
export type StagedSecret =
  { kind: "set"; value: string; externalProvider: boolean } | { kind: "rotate" };

/** The configuration profile and revision mode a server uses; null identifiers mean game defaults or follow-latest. */
export interface StagedProfile {
  /** Identifier of the selected configuration profile, or null for game defaults only. */
  profileId: string | null;
  /** Identifier of the pinned revision, or null to follow the latest revision. */
  revisionId: string | null;
}

/** All changes a member has staged for one server and not applied yet. */
export interface ServerChangeDraft {
  /** The staged mod pack choice, or null when none is staged. A `to` of null stages "no mod pack". */
  readonly modPack: { readonly to: number | null } | null;
  /** The staged configuration profile and revision mode, or null when none is staged. */
  readonly profile: StagedProfile | null;
  /** Staged raw configuration input by entry key. Disjoint from {@link ServerChangeDraft.resets}. */
  readonly values: ReadonlyMap<string, string>;
  /** Entry keys staged to return to their inherited value. Disjoint from {@link ServerChangeDraft.values}. */
  readonly resets: ReadonlySet<string>;
  /** Staged credential changes by secret key. */
  readonly secrets: ReadonlyMap<string, StagedSecret>;
}

/** The latest saved data a draft is normalized against. */
export interface SavedServerChanges {
  /** The saved mod pack identifier, or null when the server has no mod pack. */
  modPackId: number | null;
  /** The saved configuration profile and revision selection. */
  profile: StagedProfile;
  /** The saved configuration entries, or undefined while they are not loaded. */
  entries: readonly GameConfigurationEntry[] | undefined;
}

/** The draft with nothing staged. */
export const emptyServerChangeDraft: ServerChangeDraft = {
  modPack: null,
  profile: null,
  values: new Map(),
  resets: new Set(),
  secrets: new Map(),
};

/**
 * Stages a raw input value for a configuration entry and removes a staged reset of the same key.
 *
 * @param draft The current draft.
 * @param key The configuration entry key.
 * @param raw The raw text the member entered.
 * @returns A new draft with the value staged.
 */
export function stageValue(draft: ServerChangeDraft, key: string, raw: string): ServerChangeDraft {
  const values = new Map(draft.values);
  values.set(key, raw);
  const resets = new Set(draft.resets);
  resets.delete(key);
  return { ...draft, values, resets };
}

/**
 * Stages a reset to the inherited value for a configuration entry and removes a staged value of the same key.
 *
 * @param draft The current draft.
 * @param key The configuration entry key.
 * @returns A new draft with the reset staged.
 */
export function stageReset(draft: ServerChangeDraft, key: string): ServerChangeDraft {
  const values = new Map(draft.values);
  values.delete(key);
  const resets = new Set(draft.resets);
  resets.add(key);
  return { ...draft, values, resets };
}

/**
 * Removes both the staged value and the staged reset of a configuration entry.
 *
 * @param draft The current draft.
 * @param key The configuration entry key.
 * @returns A new draft without any staged change for the key.
 */
export function unstageSetting(draft: ServerChangeDraft, key: string): ServerChangeDraft {
  const values = new Map(draft.values);
  values.delete(key);
  const resets = new Set(draft.resets);
  resets.delete(key);
  return { ...draft, values, resets };
}

/**
 * Stages a mod pack change.
 *
 * @param draft The current draft.
 * @param to The target mod pack identifier, or null for no mod pack.
 * @returns A new draft with the mod pack change staged.
 */
export function stageModPack(draft: ServerChangeDraft, to: number | null): ServerChangeDraft {
  return { ...draft, modPack: { to } };
}

/**
 * Removes the staged mod pack change.
 *
 * @param draft The current draft.
 * @returns A new draft without a staged mod pack change.
 */
export function unstageModPack(draft: ServerChangeDraft): ServerChangeDraft {
  return { ...draft, modPack: null };
}

/**
 * Stages a configuration profile and revision mode change.
 *
 * @param draft The current draft.
 * @param profile The target profile and revision selection.
 * @returns A new draft with the profile change staged.
 */
export function stageProfile(draft: ServerChangeDraft, profile: StagedProfile): ServerChangeDraft {
  return { ...draft, profile: { profileId: profile.profileId, revisionId: profile.revisionId } };
}

/**
 * Removes the staged profile change.
 *
 * @param draft The current draft.
 * @returns A new draft without a staged profile change.
 */
export function unstageProfile(draft: ServerChangeDraft): ServerChangeDraft {
  return { ...draft, profile: null };
}

/**
 * Stages a credential change. Setting an empty value removes the staged item instead.
 *
 * @param draft The current draft.
 * @param key The secret key.
 * @param secret The change to stage.
 * @returns A new draft with the credential change staged or removed.
 */
export function stageSecret(
  draft: ServerChangeDraft,
  key: string,
  secret: StagedSecret,
): ServerChangeDraft {
  if (secret.kind === "set" && secret.value.length === 0) return unstageSecret(draft, key);
  const secrets = new Map(draft.secrets);
  secrets.set(key, secret);
  return { ...draft, secrets };
}

/**
 * Removes the staged change of a credential.
 *
 * @param draft The current draft.
 * @param key The secret key.
 * @returns A new draft without a staged change for the secret.
 */
export function unstageSecret(draft: ServerChangeDraft, key: string): ServerChangeDraft {
  const secrets = new Map(draft.secrets);
  secrets.delete(key);
  return { ...draft, secrets };
}

/**
 * Normalizes a draft against the latest saved data so a staged item that is no longer an effective change
 * disappears. Saved data is only read, never copied into the draft.
 *
 * Dropped are: a mod pack equal to the saved one, a profile equal to both saved identifiers, a value equal to the
 * saved input, a value for a sensitive or redacted entry, a value or reset for a key the loaded entries do not
 * contain, and a reset for an entry whose value does not come from the server. While the entries are not loaded
 * the values and resets are kept as they are.
 *
 * @param draft The raw draft.
 * @param saved The latest saved data.
 * @returns The effective draft.
 */
export function normalizeDraft(
  draft: ServerChangeDraft,
  saved: SavedServerChanges,
): ServerChangeDraft {
  const modPack = draft.modPack && draft.modPack.to !== saved.modPackId ? draft.modPack : null;
  const profile =
    draft.profile &&
    (draft.profile.profileId !== saved.profile.profileId ||
      draft.profile.revisionId !== saved.profile.revisionId)
      ? draft.profile
      : null;
  let values: ReadonlyMap<string, string> = draft.values;
  let resets: ReadonlySet<string> = draft.resets;
  if (saved.entries) {
    const entries = new Map(saved.entries.map((entry) => [entry.key, entry]));
    values = new Map(
      [...draft.values].filter(([key, raw]) => {
        const entry = entries.get(key);
        if (!entry || entry.sensitive || entry.isRedacted) return false;
        return raw !== configurationInputValue(entry);
      }),
    );
    resets = new Set(
      [...draft.resets].filter((key) => {
        const entry = entries.get(key);
        return entry !== undefined && entry.source === "Server";
      }),
    );
  }
  return { modPack, profile, values, resets, secrets: draft.secrets };
}

/**
 * Counts the effective items of a draft: the mod pack, the profile, each value, each reset and each credential.
 *
 * @param draft The draft, normally already normalized.
 * @returns The number of staged changes.
 */
export function countChanges(draft: ServerChangeDraft): number {
  return (
    (draft.modPack ? 1 : 0) +
    (draft.profile ? 1 : 0) +
    draft.values.size +
    draft.resets.size +
    draft.secrets.size
  );
}

/** Identifier of one apply step. */
export type ChangeStepId = "modPack" | "profile" | "settings" | `secret:${string}`;

/** One request of the apply plan. */
export type ApplyStep =
  | { id: "modPack"; kind: "modPack"; to: number | null; itemCount: 1 }
  | { id: "profile"; kind: "profile"; profile: StagedProfile; itemCount: 1 }
  | {
      id: "settings";
      kind: "settings";
      values: ConfigurationValue[];
      resetKeys: string[];
      itemCount: number;
      dependsOnProfile: boolean;
    }
  | { id: `secret:${string}`; kind: "secret"; key: string; secret: StagedSecret; itemCount: 1 };

/** Compares two strings by UTF-16 code unit order, independent of the locale. */
function compareOrdinal(left: string, right: string): number {
  if (left < right) return -1;
  return left > right ? 1 : 0;
}

/**
 * Builds the ordered apply plan: mod pack, profile, one configuration update, then each credential by key.
 *
 * Values are parsed with the kind of their entry. A value whose entry is missing or cannot be parsed is left
 * out, so callers validate the staged settings before they build a plan.
 *
 * @param draft The normalized draft.
 * @param entries The loaded configuration entries used to parse the staged values.
 * @returns The steps in execution order; steps without items are omitted.
 */
export function buildApplyPlan(
  draft: ServerChangeDraft,
  entries: readonly GameConfigurationEntry[],
): ApplyStep[] {
  const plan: ApplyStep[] = [];
  if (draft.modPack)
    plan.push({ id: "modPack", kind: "modPack", to: draft.modPack.to, itemCount: 1 });
  if (draft.profile)
    plan.push({
      id: "profile",
      kind: "profile",
      profile: { profileId: draft.profile.profileId, revisionId: draft.profile.revisionId },
      itemCount: 1,
    });
  const entriesByKey = new Map(entries.map((entry) => [entry.key, entry]));
  const values: ConfigurationValue[] = [];
  for (const key of [...draft.values.keys()].sort(compareOrdinal)) {
    const entry = entriesByKey.get(key);
    const raw = draft.values.get(key);
    if (!entry || raw === undefined) continue;
    try {
      values.push({ key, value: parseConfigurationEntryValue(entry, raw) });
    } catch {
      continue;
    }
  }
  const resetKeys = [...draft.resets].sort(compareOrdinal);
  if (values.length + resetKeys.length > 0)
    plan.push({
      id: "settings",
      kind: "settings",
      values,
      resetKeys,
      itemCount: values.length + resetKeys.length,
      dependsOnProfile: draft.profile !== null,
    });
  for (const key of [...draft.secrets.keys()].sort(compareOrdinal)) {
    const secret = draft.secrets.get(key);
    if (secret) plan.push({ id: `secret:${key}`, kind: "secret", key, secret, itemCount: 1 });
  }
  return plan;
}

/** Why an apply step failed, derived only from the API error code. */
export type ApplyFailureReason = "forbidden" | "conflict" | "other";

/** The outcome of one apply step. */
export type ItemResult = { id: ChangeStepId; itemCount: number } & (
  | { state: "applied" }
  | { state: "failed"; reason: ApplyFailureReason }
  | { state: "notTried"; reason: "dependency" }
);

/** The requests the apply runner issues; each rejects when the API refuses the change. */
export interface ApplyExecutors {
  /** Changes the mod pack of the server. */
  changeModPack(to: number | null): Promise<void>;
  /** Selects the configuration profile and revision mode. */
  selectProfile(profile: StagedProfile): Promise<void>;
  /** Updates configuration values and resets in one request. */
  updateConfiguration(values: ConfigurationValue[], resetKeys: string[]): Promise<void>;
  /** Sets a credential to a member-provided value. */
  setSecret(key: string, value: string, externalProvider: boolean): Promise<void>;
  /** Asks the server to generate a new credential value. */
  rotateSecret(key: string): Promise<void>;
}

/**
 * Maps a rejection to a failure reason using only the API error code. The error text is never inspected because
 * it can echo member input.
 *
 * @param error The rejection value.
 * @returns "forbidden" or "conflict" for those API codes, otherwise "other".
 */
export function failureReason(error: unknown): ApplyFailureReason {
  if (error instanceof ApiError) {
    if (error.code === "forbidden") return "forbidden";
    if (error.code === "conflict") return "conflict";
  }
  return "other";
}

async function runStep(step: ApplyStep, executors: ApplyExecutors): Promise<void> {
  switch (step.kind) {
    case "modPack":
      return executors.changeModPack(step.to);
    case "profile":
      return executors.selectProfile(step.profile);
    case "settings":
      return executors.updateConfiguration(step.values, step.resetKeys);
    case "secret":
      return step.secret.kind === "set"
        ? executors.setSecret(step.key, step.secret.value, step.secret.externalProvider)
        : executors.rotateSecret(step.key);
  }
}

/**
 * Runs the plan sequentially. A failure never blocks an independent step and nothing is rolled back; the
 * settings step is not tried when the profile step failed, because values are validated against the profile.
 * The runner never throws.
 *
 * @param plan The steps from {@link buildApplyPlan}.
 * @param executors The injected requests.
 * @returns One result per step in plan order.
 */
export async function runApplyPlan(
  plan: readonly ApplyStep[],
  executors: ApplyExecutors,
): Promise<ItemResult[]> {
  const results: ItemResult[] = [];
  let profileFailed = false;
  for (const step of plan) {
    if (step.kind === "settings" && step.dependsOnProfile && profileFailed) {
      results.push({
        id: step.id,
        itemCount: step.itemCount,
        state: "notTried",
        reason: "dependency",
      });
      continue;
    }
    try {
      await runStep(step, executors);
      results.push({ id: step.id, itemCount: step.itemCount, state: "applied" });
    } catch (error) {
      if (step.kind === "profile") profileFailed = true;
      results.push({
        id: step.id,
        itemCount: step.itemCount,
        state: "failed",
        reason: failureReason(error),
      });
    }
  }
  return results;
}

/**
 * Removes the applied steps from the draft; failed and not-tried steps stay staged.
 *
 * @param draft The raw draft.
 * @param results The results of an apply run.
 * @returns A new draft without the applied items.
 */
export function removeApplied(
  draft: ServerChangeDraft,
  results: readonly ItemResult[],
): ServerChangeDraft {
  let next = draft;
  for (const result of results) {
    if (result.state !== "applied") continue;
    if (result.id === "modPack") next = unstageModPack(next);
    else if (result.id === "profile") next = unstageProfile(next);
    else if (result.id === "settings") next = { ...next, values: new Map(), resets: new Set() };
    else next = unstageSecret(next, result.id.slice("secret:".length));
  }
  return next;
}

/**
 * Counts items across the results.
 *
 * @param results The results of an apply run.
 * @returns The applied item count, the total item count and the items that remain staged.
 */
export function summarizeResults(results: readonly ItemResult[]): {
  applied: number;
  total: number;
  remaining: number;
} {
  const total = results.reduce((sum, result) => sum + result.itemCount, 0);
  const applied = results.reduce(
    (sum, result) => sum + (result.state === "applied" ? result.itemCount : 0),
    0,
  );
  return { applied, total, remaining: total - applied };
}

/** A typed reason a staged configuration value is not acceptable; the UI maps each code to its own copy. */
export type ConfigurationIssue =
  | { code: "required" }
  | { code: "min"; min: number }
  | { code: "max"; max: number }
  | { code: "number" }
  | { code: "decimal" }
  | { code: "choice" };

/**
 * Validates a raw input against its configuration entry and returns a typed issue instead of English text.
 * It follows the semantics of `validateConfigurationEntry`: a blank optional entry is valid, an integer parse
 * failure is "number", any other numeric parse failure is "decimal".
 *
 * @param entry The configuration entry the raw input belongs to.
 * @param raw The raw text the member entered.
 * @returns The first issue found, or undefined when the input is acceptable.
 */
export function configurationEntryIssue(
  entry: GameConfigurationEntry,
  raw: string,
): ConfigurationIssue | undefined {
  const blank = raw.trim().length === 0;
  if (blank && entry.isRedacted) return undefined;
  if (blank) return entry.required ? { code: "required" } : undefined;
  let value: unknown;
  try {
    value = parseConfigurationEntryValue(entry, raw);
  } catch {
    return { code: entry.kind.toLowerCase() === "integer" ? "number" : "decimal" };
  }
  if (typeof value === "number") {
    if (entry.minimum !== null && value < entry.minimum) return { code: "min", min: entry.minimum };
    if (entry.maximum !== null && value > entry.maximum) return { code: "max", max: entry.maximum };
  }
  if (
    entry.choices?.length &&
    !entry.choices.some((choice) => JSON.stringify(choice.value) === JSON.stringify(value))
  )
    return { code: "choice" };
  if (
    entry.allowedValues.length > 0 &&
    typeof value === "string" &&
    !entry.allowedValues.includes(value)
  )
    return { code: "choice" };
  return undefined;
}

/**
 * Finds the staged values that fail validation. Resets are never invalid.
 *
 * @param draft The normalized draft.
 * @param entries The loaded configuration entries.
 * @returns The keys of the invalid staged values in the order of the entries.
 */
export function validateStagedSettings(
  draft: ServerChangeDraft,
  entries: readonly GameConfigurationEntry[],
): string[] {
  const invalid: string[] = [];
  for (const entry of entries) {
    const raw = draft.values.get(entry.key);
    if (raw !== undefined && configurationEntryIssue(entry, raw) !== undefined)
      invalid.push(entry.key);
  }
  return invalid;
}

/** The most characters a credential value may have after trimming; it mirrors the API limit. */
export const maxCredentialValueLength = 4096;

/** A typed reason a staged credential value is not acceptable; the UI maps each code to its own fixed copy. */
export type SecretValueIssue = "blank" | "tooLong" | "control" | "token";

/**
 * Checks a typed credential value against the rules the API enforces, so a member never applies a value the game
 * cannot use. The API stays authoritative; this check only gives feedback before the review.
 *
 * The rules apply to the trimmed value: it must not be empty, must not exceed {@link maxCredentialValueLength}
 * characters and must not contain a control character (code units below 0x20 and 0x7F to 0x9F). A value of the
 * `Token` format also has to be printable ASCII (0x21 to 0x7E) without a double quote or a backslash.
 *
 * @param value The raw value the member typed.
 * @param format The characters the game can use, from the saved status of the credential.
 * @returns The first issue found, or undefined when the value is acceptable. The issue never carries the value.
 */
export function credentialValueIssue(
  value: string,
  format: GameServerSecretStatus["valueFormat"],
): SecretValueIssue | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) return "blank";
  if (trimmed.length > maxCredentialValueLength) return "tooLong";
  for (let index = 0; index < trimmed.length; index += 1) {
    const unit = trimmed.charCodeAt(index);
    if (unit < 0x20 || (unit >= 0x7f && unit <= 0x9f)) return "control";
  }
  if (format === "Token") {
    for (let index = 0; index < trimmed.length; index += 1) {
      const unit = trimmed.charCodeAt(index);
      if (unit < 0x21 || unit > 0x7e || unit === 0x22 || unit === 0x5c) return "token";
    }
  }
  return undefined;
}

/**
 * Finds the staged credential values that fail validation. Rotations carry no typed value and are never invalid.
 *
 * @param draft The normalized draft.
 * @param secrets The loaded credentials with the value format each game accepts.
 * @returns The keys of the credentials with an invalid staged value, in the order of the list. A key without a
 * status is skipped.
 */
export function validateStagedSecrets(
  draft: ServerChangeDraft,
  secrets: readonly Pick<GameServerSecretStatus, "key" | "valueFormat">[],
): string[] {
  const invalid: string[] = [];
  for (const secret of secrets) {
    const staged = draft.secrets.get(secret.key);
    if (
      staged?.kind === "set" &&
      credentialValueIssue(staged.value, secret.valueFormat) !== undefined
    )
      invalid.push(secret.key);
  }
  return invalid;
}

/** Identifier of one row of the review. */
export type ReviewItemId =
  "modPack" | "profile" | `value:${string}` | `reset:${string}` | `secret:${string}`;

/** A value shown in the Before or After column of the review; the dialog maps each kind to its copy. */
export type ReviewValue =
  | { kind: "text"; text: string; apiText: boolean }
  | { kind: "boolean"; value: boolean }
  | { kind: "notSet" }
  | { kind: "inherited" }
  | { kind: "secretChanged" }
  | { kind: "secretRotated" }
  | { kind: "noModPack" }
  | { kind: "gameDefaults" }
  | { kind: "profileLatest"; title: string }
  | { kind: "profilePinned"; title: string; revision: number | null };

/**
 * One row of the review.
 *
 * Credential rows carry a placeholder Before value: an existing credential is never described, so the dialog
 * shows only the After value for that group. Mod pack and profile rows have an empty name because the dialog
 * renders their label from copy.
 */
export interface ReviewItem {
  /** Identifier used to remove the row from the draft. */
  id: ReviewItemId;
  /** The apply step this row belongs to. */
  stepId: ChangeStepId;
  /** The display name of the changed item. */
  name: string;
  /** Whether the name is English text from the API. */
  nameIsApiText: boolean;
  /** The saved state. */
  before: ReviewValue;
  /** The state after the change is applied. */
  after: ReviewValue;
}

/** A group of review rows. */
export interface ReviewGroup {
  /** The kind of change the group holds. */
  kind: "modPack" | "profile" | "settings" | "credentials";
  /** The rows of the group. */
  items: ReviewItem[];
}

/** The latest saved facts the review needs to describe a draft. */
export interface ReviewContext {
  /** The saved mod pack identifier and title. */
  savedModPack: { id: number | null; title: string | null };
  /** Titles of the mod packs the member can pick, by identifier. */
  modPackTitles: ReadonlyMap<number, string>;
  /** The selectable configuration profiles. */
  profiles: readonly { primaryKey: string; title: string; latestRevisionNumber: number }[];
  /** The saved profile and revision selection. */
  savedProfile: StagedProfile;
  /** The title of the saved profile, or null when none is selected. */
  savedProfileTitle: string | null;
  /** The number of the saved revision, or null when unknown. */
  savedProfileRevisionNumber: number | null;
  /** The loaded configuration entries. */
  entries: readonly GameConfigurationEntry[];
  /** The managed credentials, used only for their display names. */
  secrets: readonly { key: string; displayName: string }[];
}

function choiceLabel(entry: GameConfigurationEntry, raw: string): string | undefined {
  if (!entry.choices?.length) return undefined;
  try {
    const parsed = JSON.stringify(parseConfigurationEntryValue(entry, raw));
    return entry.choices.find((choice) => JSON.stringify(choice.value) === parsed)?.label;
  } catch {
    return undefined;
  }
}

function describeInput(entry: GameConfigurationEntry, raw: string): ReviewValue {
  const kind = entry.kind.toLowerCase();
  if (raw.trim().length === 0) return { kind: "notSet" };
  if (kind === "boolean") return { kind: "boolean", value: raw === "true" };
  const label = choiceLabel(entry, raw);
  if (label !== undefined) return { kind: "text", text: label, apiText: true };
  if (kind === "stringarray")
    return {
      kind: "text",
      text: raw
        .split(/\r?\n/)
        .map((line) => line.trim())
        .filter(Boolean)
        .join(", "),
      apiText: false,
    };
  return { kind: "text", text: raw, apiText: false };
}

function describeProfile(
  profileId: string | null,
  revisionId: string | null,
  title: string,
  revision: number | null,
): ReviewValue {
  if (profileId === null) return { kind: "gameDefaults" };
  return revisionId === null
    ? { kind: "profileLatest", title }
    : { kind: "profilePinned", title, revision };
}

function compareReviewEntries(left: GameConfigurationEntry, right: GameConfigurationEntry): number {
  return (
    left.group.localeCompare(right.group) ||
    left.order - right.order ||
    compareOrdinal(left.key, right.key)
  );
}

/**
 * Describes a normalized draft for the review dialog as typed groups and rows. No staged credential value ever
 * appears in a descriptor; credentials are described only as changed or rotated.
 *
 * Groups come in the order mod pack, profile, settings, credentials and empty groups are omitted. Setting rows
 * sort by group and order; credential rows sort by key.
 *
 * @param draft The normalized draft.
 * @param context The latest saved facts.
 * @returns The review groups.
 */
export function describeReview(draft: ServerChangeDraft, context: ReviewContext): ReviewGroup[] {
  const groups: ReviewGroup[] = [];

  if (draft.modPack) {
    const to = draft.modPack.to;
    const savedId = context.savedModPack.id;
    groups.push({
      kind: "modPack",
      items: [
        {
          id: "modPack",
          stepId: "modPack",
          name: "",
          nameIsApiText: false,
          before:
            savedId === null
              ? { kind: "noModPack" }
              : {
                  kind: "text",
                  text: context.savedModPack.title ?? String(savedId),
                  apiText: true,
                },
          after:
            to === null
              ? { kind: "noModPack" }
              : { kind: "text", text: context.modPackTitles.get(to) ?? String(to), apiText: true },
        },
      ],
    });
  }

  if (draft.profile) {
    const staged = draft.profile;
    const chosen = context.profiles.find((profile) => profile.primaryKey === staged.profileId);
    const revision =
      staged.revisionId !== null && staged.revisionId !== context.savedProfile.revisionId
        ? (chosen?.latestRevisionNumber ?? null)
        : context.savedProfileRevisionNumber;
    groups.push({
      kind: "profile",
      items: [
        {
          id: "profile",
          stepId: "profile",
          name: "",
          nameIsApiText: false,
          before: describeProfile(
            context.savedProfile.profileId,
            context.savedProfile.revisionId,
            context.savedProfileTitle ?? "",
            context.savedProfileRevisionNumber,
          ),
          after: describeProfile(
            staged.profileId,
            staged.revisionId,
            chosen?.title ?? "",
            revision,
          ),
        },
      ],
    });
  }

  const settings: ReviewItem[] = [];
  for (const entry of [...context.entries].sort(compareReviewEntries)) {
    if (entry.sensitive || entry.isRedacted) continue;
    const before = describeInput(entry, configurationInputValue(entry));
    const raw = draft.values.get(entry.key);
    if (raw !== undefined)
      settings.push({
        id: `value:${entry.key}`,
        stepId: "settings",
        name: entry.displayName,
        nameIsApiText: true,
        before,
        after: describeInput(entry, raw),
      });
    else if (draft.resets.has(entry.key))
      settings.push({
        id: `reset:${entry.key}`,
        stepId: "settings",
        name: entry.displayName,
        nameIsApiText: true,
        before,
        after: { kind: "inherited" },
      });
  }
  if (settings.length > 0) groups.push({ kind: "settings", items: settings });

  const names = new Map(context.secrets.map((secret) => [secret.key, secret.displayName]));
  const credentials: ReviewItem[] = [...draft.secrets.keys()].sort(compareOrdinal).map((key) => {
    const secret = draft.secrets.get(key);
    return {
      id: `secret:${key}` as const,
      stepId: `secret:${key}` as const,
      name: names.get(key) ?? key,
      nameIsApiText: true,
      before: { kind: "notSet" },
      after: secret?.kind === "rotate" ? { kind: "secretRotated" } : { kind: "secretChanged" },
    };
  });
  if (credentials.length > 0) groups.push({ kind: "credentials", items: credentials });

  return groups;
}

/**
 * Removes the one staged item a review row stands for.
 *
 * @param draft The raw draft.
 * @param id The identifier of the review row.
 * @returns A new draft without that item.
 */
export function removeReviewItem(draft: ServerChangeDraft, id: ReviewItemId): ServerChangeDraft {
  if (id === "modPack") return unstageModPack(draft);
  if (id === "profile") return unstageProfile(draft);
  if (id.startsWith("secret:")) return unstageSecret(draft, id.slice("secret:".length));
  if (id.startsWith("value:")) {
    const values = new Map(draft.values);
    values.delete(id.slice("value:".length));
    return { ...draft, values };
  }
  const resets = new Set(draft.resets);
  resets.delete(id.slice("reset:".length));
  return { ...draft, resets };
}
