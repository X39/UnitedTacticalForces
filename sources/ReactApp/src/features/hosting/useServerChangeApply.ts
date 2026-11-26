import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useMemo, useRef, useState } from "react";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import {
  buildApplyPlan,
  describeReview,
  runApplyPlan,
  summarizeResults,
  type ApplyExecutors,
  type ApplyStep,
  type ItemResult,
  type ReviewContext,
  type ReviewGroup,
} from "@/features/hosting/serverChangeDraft";
import { useServerChangeDraft } from "@/features/hosting/useServerChangeDraft";
import type { GameServer, GameServerSecretValue } from "@/features/hosting/types";

/** Options of {@link useApplyServerChanges}. */
export interface ApplyServerChangesOptions {
  /**
   * Called once when an apply run left nothing failed and nothing not tried.
   *
   * @param count The number of items that were applied.
   */
  onAllApplied(count: number): void;
}

/** The apply operation of the Settings section and its outcome. */
export interface ServerChangeApplier {
  /** Applies every staged change through the existing endpoints, one request after the other. */
  apply(): void;
  /** Whether an apply run is in progress. */
  pending: boolean;
  /** The per-item results of the last run, or null before a run and after {@link ServerChangeApplier.clearResults}. */
  results: readonly ItemResult[] | null;
  /** Forgets the results of the last run. */
  clearResults(): void;
}

/**
 * Builds the requests of one apply run for a server.
 *
 * The rotate executor hands the generated value straight to `onGenerated` and resolves to nothing, so the value
 * never becomes part of an apply result or of the mutation data.
 *
 * @param serverId The server the requests change.
 * @param onGenerated Receives the value a rotation generated, once.
 * @returns The executors the apply runner calls.
 */
function executorsFor(
  serverId: string,
  onGenerated: (value: GameServerSecretValue) => void,
): ApplyExecutors {
  return {
    changeModPack: (to) => hostingService.changeModPack(serverId, to),
    selectProfile: ({ profileId, revisionId }) =>
      hostingService.selectConfigurationProfile(serverId, profileId, revisionId),
    updateConfiguration: async (values, resetKeys) => {
      await hostingService.updateConfiguration(serverId, values, resetKeys);
    },
    setSecret: (key, value, externalProvider) =>
      hostingService.setSecret(serverId, key, value, externalProvider),
    rotateSecret: async (key) => {
      onGenerated(await hostingService.rotateSecret(serverId, key));
    },
  };
}

/**
 * Applies the staged changes of a server and reports the outcome per item.
 *
 * The run is a mutation without variables: the plan (which can carry typed credential values) lives in a ref
 * only for the duration of the run, and the mutation cache keeps nothing (`gcTime: 0`). Applied items leave the
 * draft; failed and not-tried items stay staged. After the run the queries that show saved server state are
 * invalidated with exact keys, because the fleet key is a prefix of every per-server key.
 *
 * @param server The server whose changes are applied.
 * @param options The completion callback.
 * @returns The apply operation and its results.
 */
export function useApplyServerChanges(
  server: GameServer,
  options: ApplyServerChangesOptions,
): ServerChangeApplier {
  const client = useQueryClient();
  const { draft, saved, applyResults, setApplying, showOneTimeValue } = useServerChangeDraft();
  const serverId = server.primaryKey;
  const planRef = useRef<ApplyStep[]>([]);
  const [results, setResults] = useState<readonly ItemResult[] | null>(null);

  const mutation = useMutation<ItemResult[], Error, void>({
    gcTime: 0,
    mutationFn: () => runApplyPlan(planRef.current, executorsFor(serverId, showOneTimeValue)),
    onMutate: () => client.cancelQueries({ queryKey: hostingQueryKeys.storageDemand(serverId) }),
    onSettled: async (outcome) => {
      planRef.current = [];
      const finished = outcome ?? [];
      await Promise.all(
        [
          hostingQueryKeys.server(serverId),
          hostingQueryKeys.configuration(serverId),
          hostingQueryKeys.configurationOptions(serverId),
          hostingQueryKeys.storageDemand(serverId),
          hostingQueryKeys.modPackOptions(serverId),
          hostingQueryKeys.secrets(serverId),
          hostingQueryKeys.fleet,
        ].map((queryKey) => client.invalidateQueries({ queryKey, exact: true })),
      );
      applyResults(finished);
      setApplying(false);
      const summary = summarizeResults(finished);
      const complete = finished.length > 0 && summary.remaining === 0;
      // A complete run leaves nothing to report: stale "Applied" states must not decorate later changes.
      setResults(complete ? null : finished);
      if (complete) options.onAllApplied(summary.applied);
    },
  });
  const { mutate } = mutation;

  const apply = useCallback(() => {
    const plan = buildApplyPlan(draft, saved.entries ?? []);
    if (plan.length === 0) return;
    planRef.current = plan;
    setResults(null);
    setApplying(true);
    mutate();
  }, [draft, saved.entries, setApplying, mutate]);
  const clearResults = useCallback(() => setResults(null), []);

  return { apply, pending: mutation.isPending, results, clearResults };
}

/**
 * Describes the staged changes of a server for the review dialog.
 *
 * The saved facts come from the same queries the panels use, so the Before values always read the latest saved
 * data. The mod pack options are requested only while a mod pack change is staged, profile options only for a
 * member who may modify the server and the credentials only for a member who may manage them.
 *
 * @param server The server whose draft is described.
 * @returns The review groups in their fixed order; empty groups are omitted.
 */
export function useReviewGroups(server: GameServer): ReviewGroup[] {
  const { draft, saved } = useServerChangeDraft();
  const serverId = server.primaryKey;
  const configuration = useQuery({
    queryKey: hostingQueryKeys.configuration(serverId),
    queryFn: ({ signal }) => hostingService.configuration(serverId, signal),
  });
  const profiles = useQuery({
    queryKey: hostingQueryKeys.configurationOptions(serverId),
    queryFn: ({ signal }) => hostingService.serverConfigurationProfileOptions(serverId, signal),
    enabled: server.canModify === true,
  });
  const secrets = useQuery({
    queryKey: hostingQueryKeys.secrets(serverId),
    queryFn: ({ signal }) => hostingService.secrets(serverId, signal),
    enabled: server.canManageSecrets === true,
  });
  const modPacks = useQuery({
    queryKey: hostingQueryKeys.modPackOptions(serverId),
    queryFn: ({ signal }) => hostingService.modPackOptions(serverId, signal),
    enabled: server.canModify === true && draft.modPack !== null,
  });

  const context = useMemo<ReviewContext>(
    () => ({
      savedModPack: {
        id: saved.modPackId,
        title: server.selectedModPackDefinitionTitle ?? null,
      },
      modPackTitles: new Map((modPacks.data ?? []).map((pack) => [pack.primaryKey, pack.title])),
      profiles: profiles.data ?? [],
      savedProfile: saved.profile,
      savedProfileTitle: server.selectedConfigurationProfileTitle ?? null,
      savedProfileRevisionNumber: configuration.data?.profileRevisionNumber ?? null,
      entries: configuration.data?.entries ?? [],
      secrets: secrets.data ?? [],
    }),
    [
      saved.modPackId,
      saved.profile,
      server.selectedModPackDefinitionTitle,
      server.selectedConfigurationProfileTitle,
      modPacks.data,
      profiles.data,
      configuration.data,
      secrets.data,
    ],
  );
  return useMemo(() => describeReview(draft, context), [draft, context]);
}
