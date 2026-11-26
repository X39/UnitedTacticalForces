import { createContext, useContext, useMemo, useReducer, type JSX, type ReactNode } from "react";
import {
  countChanges,
  emptyServerChangeDraft,
  normalizeDraft,
  removeApplied,
  removeReviewItem,
  stageModPack,
  stageProfile,
  stageReset,
  stageSecret,
  stageValue,
  unstageModPack,
  unstageProfile,
  unstageSecret,
  unstageSetting,
  type ItemResult,
  type ReviewItemId,
  type SavedServerChanges,
  type ServerChangeDraft,
  type StagedProfile,
  type StagedSecret,
} from "@/features/hosting/serverChangeDraft";
import type { GameServerSecretValue } from "@/features/hosting/types";

interface DraftState {
  raw: ServerChangeDraft;
  oneTimeValues: ReadonlyMap<string, GameServerSecretValue>;
  applying: boolean;
  reviewAttempt: number;
  announcement: string;
}

type DraftAction =
  | { type: "stageValue"; key: string; raw: string }
  | { type: "stageReset"; key: string }
  | { type: "unstageSetting"; key: string }
  | { type: "stageModPack"; to: number | null }
  | { type: "unstageModPack" }
  | { type: "stageProfile"; profile: StagedProfile }
  | { type: "unstageProfile" }
  | { type: "stageSecret"; key: string; secret: StagedSecret }
  | { type: "unstageSecret"; key: string }
  | { type: "showOneTimeValue"; value: GameServerSecretValue }
  | { type: "dismissOneTimeValue"; key: string }
  | { type: "removeItem"; id: ReviewItemId }
  | { type: "applyResults"; results: readonly ItemResult[] }
  | { type: "discard" }
  | { type: "markReviewAttempt" }
  | { type: "setApplying"; value: boolean }
  | { type: "announce"; message: string };

const noOneTimeValues: ReadonlyMap<string, GameServerSecretValue> = new Map();

const initialState: DraftState = {
  raw: emptyServerChangeDraft,
  oneTimeValues: noOneTimeValues,
  applying: false,
  reviewAttempt: 0,
  announcement: "",
};

function withRaw(state: DraftState, raw: ServerChangeDraft): DraftState {
  return { ...state, raw };
}

/** Returns the one-time values without the given key; the same map when the key is not present. */
function withoutOneTimeValue(
  values: ReadonlyMap<string, GameServerSecretValue>,
  key: string,
): ReadonlyMap<string, GameServerSecretValue> {
  if (!values.has(key)) return values;
  const next = new Map(values);
  next.delete(key);
  return next;
}

function reduce(state: DraftState, action: DraftAction): DraftState {
  switch (action.type) {
    case "stageValue":
      return withRaw(state, stageValue(state.raw, action.key, action.raw));
    case "stageReset":
      return withRaw(state, stageReset(state.raw, action.key));
    case "unstageSetting":
      return withRaw(state, unstageSetting(state.raw, action.key));
    case "stageModPack":
      return withRaw(state, stageModPack(state.raw, action.to));
    case "unstageModPack":
      return withRaw(state, unstageModPack(state.raw));
    case "stageProfile":
      return withRaw(state, stageProfile(state.raw, action.profile));
    case "unstageProfile":
      return withRaw(state, unstageProfile(state.raw));
    case "stageSecret":
      // A new staged change of a credential makes the value shown for its previous rotation obsolete.
      return {
        ...withRaw(state, stageSecret(state.raw, action.key, action.secret)),
        oneTimeValues: withoutOneTimeValue(state.oneTimeValues, action.key),
      };
    case "unstageSecret":
      return withRaw(state, unstageSecret(state.raw, action.key));
    case "showOneTimeValue":
      return {
        ...state,
        oneTimeValues: new Map(state.oneTimeValues).set(action.value.key, action.value),
      };
    case "dismissOneTimeValue":
      return { ...state, oneTimeValues: withoutOneTimeValue(state.oneTimeValues, action.key) };
    case "removeItem":
      return withRaw(state, removeReviewItem(state.raw, action.id));
    case "applyResults":
      return withRaw(state, removeApplied(state.raw, action.results));
    case "discard":
      return withRaw(state, emptyServerChangeDraft);
    case "markReviewAttempt":
      return { ...state, reviewAttempt: state.reviewAttempt + 1 };
    case "setApplying":
      return { ...state, applying: action.value };
    case "announce":
      return { ...state, announcement: action.message };
  }
}

/** What the draft provider exposes to the panels, the bar and the review dialog of the Settings section. */
export interface ServerChangeDraftContextValue {
  /** The effective draft, normalized against the latest saved data on every render. */
  draft: ServerChangeDraft;
  /** The number of effective staged changes. */
  count: number;
  /** The latest saved data the draft is normalized against. */
  saved: SavedServerChanges;
  /** Whether the member can only look at the settings. */
  readOnly: boolean;
  /** Whether an apply run is in progress. */
  applying: boolean;
  /** How often the member tried to review; panels use it to reveal validation errors after a review attempt. */
  reviewAttempt: number;
  /** The text the Settings section renders in its single polite live region. */
  announcement: string;
  /** Stages a raw configuration input. */
  stageValue(key: string, raw: string): void;
  /** Stages a reset to the inherited value. */
  stageReset(key: string): void;
  /** Removes the staged value or reset of a setting. */
  unstageSetting(key: string): void;
  /** Stages a mod pack change; null stages no mod pack. */
  stageModPack(to: number | null): void;
  /** Removes the staged mod pack change. */
  unstageModPack(): void;
  /** Stages a profile and revision mode change. */
  stageProfile(profile: StagedProfile): void;
  /** Removes the staged profile change. */
  unstageProfile(): void;
  /** Stages a credential change. */
  stageSecret(key: string, secret: StagedSecret): void;
  /** Removes the staged change of a credential. */
  unstageSecret(key: string): void;
  /**
   * The newly generated credential values that are shown once, keyed by credential key.
   *
   * They live beside the draft, never in it, in an apply plan or in an apply result.
   */
  oneTimeValues: ReadonlyMap<string, GameServerSecretValue>;
  /** Shows a newly generated credential value once; it replaces an earlier one-time value of the same key. */
  showOneTimeValue(value: GameServerSecretValue): void;
  /** Removes the one-time value of a credential. */
  dismissOneTimeValue(key: string): void;
  /** Removes one review row from the draft. */
  removeItem(id: ReviewItemId): void;
  /** Removes the items an apply run applied. */
  applyResults(results: readonly ItemResult[]): void;
  /** Empties the draft. */
  discard(): void;
  /** Records that the member tried to review. */
  markReviewAttempt(): void;
  /** Sets whether an apply run is in progress. */
  setApplying(value: boolean): void;
  /** Sets the text of the polite live region. */
  announce(message: string): void;
}

const DraftContext = createContext<ServerChangeDraftContextValue | undefined>(undefined);

/** Props of {@link ServerChangeDraftProvider}. */
export interface ServerChangeDraftProviderProps {
  /** The latest saved data; keep the object referentially stable while the data is unchanged. */
  saved: SavedServerChanges;
  /** Whether the member can only look at the settings. */
  readOnly: boolean;
  /** The Settings section content. */
  children: ReactNode;
}

/**
 * Owns the staged-change draft of one Settings section instance.
 *
 * The draft lives only in React state and is gone when the provider unmounts. The effective draft is derived by
 * normalizing the raw draft against the saved data on every render; saved data is never copied into state.
 *
 * Newly generated credential values that are shown once sit beside the draft, never in it or in an apply plan, and
 * vanish when the provider unmounts, when the member dismisses them or when a new change of the same credential is
 * staged. They are never written to a cache, a storage or the URL.
 */
export function ServerChangeDraftProvider({
  saved,
  readOnly,
  children,
}: ServerChangeDraftProviderProps): JSX.Element {
  const [state, dispatch] = useReducer(reduce, initialState);
  const draft = useMemo(() => normalizeDraft(state.raw, saved), [state.raw, saved]);
  const count = countChanges(draft);

  const actions = useMemo(
    () => ({
      stageValue: (key: string, raw: string) => dispatch({ type: "stageValue", key, raw }),
      stageReset: (key: string) => dispatch({ type: "stageReset", key }),
      unstageSetting: (key: string) => dispatch({ type: "unstageSetting", key }),
      stageModPack: (to: number | null) => dispatch({ type: "stageModPack", to }),
      unstageModPack: () => dispatch({ type: "unstageModPack" }),
      stageProfile: (profile: StagedProfile) => dispatch({ type: "stageProfile", profile }),
      unstageProfile: () => dispatch({ type: "unstageProfile" }),
      stageSecret: (key: string, secret: StagedSecret) =>
        dispatch({ type: "stageSecret", key, secret }),
      unstageSecret: (key: string) => dispatch({ type: "unstageSecret", key }),
      showOneTimeValue: (value: GameServerSecretValue) =>
        dispatch({ type: "showOneTimeValue", value }),
      dismissOneTimeValue: (key: string) => dispatch({ type: "dismissOneTimeValue", key }),
      removeItem: (id: ReviewItemId) => dispatch({ type: "removeItem", id }),
      applyResults: (results: readonly ItemResult[]) => dispatch({ type: "applyResults", results }),
      discard: () => dispatch({ type: "discard" }),
      markReviewAttempt: () => dispatch({ type: "markReviewAttempt" }),
      setApplying: (value: boolean) => dispatch({ type: "setApplying", value }),
      announce: (message: string) => dispatch({ type: "announce", message }),
    }),
    [],
  );

  const value = useMemo<ServerChangeDraftContextValue>(
    () => ({
      draft,
      count,
      saved,
      readOnly,
      applying: state.applying,
      reviewAttempt: state.reviewAttempt,
      announcement: state.announcement,
      oneTimeValues: state.oneTimeValues,
      ...actions,
    }),
    [
      draft,
      count,
      saved,
      readOnly,
      state.applying,
      state.reviewAttempt,
      state.announcement,
      state.oneTimeValues,
      actions,
    ],
  );

  return <DraftContext.Provider value={value}>{children}</DraftContext.Provider>;
}

/**
 * Reads the staged-change draft of the surrounding Settings section.
 *
 * @returns The draft facts and the operations that change them.
 * @throws Error When called outside {@link ServerChangeDraftProvider}.
 */
export function useServerChangeDraft(): ServerChangeDraftContextValue {
  const value = useContext(DraftContext);
  if (!value)
    throw new Error("useServerChangeDraft must be used inside ServerChangeDraftProvider.");
  return value;
}
