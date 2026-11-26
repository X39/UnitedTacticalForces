import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
  type RefObject,
} from "react";
import { useTranslation } from "react-i18next";
import { useLocation, useSearchParams } from "react-router";
import { useAppToast } from "@/components/AppToasts";
import { CostConfirmDialog } from "@/components/server/CostConfirmDialog";
import {
  CostConfirmationContext,
  type CostConfirmationContextValue,
  type CostConfirmationRequest,
  type CostConfirmationSource,
} from "@/components/server/useCostConfirmation";
import { lookupActionVocabulary, toastKeyFor } from "@/features/hosting/actionVocabulary";
import { hostingQueryKeys, hostingService } from "@/features/hosting/services";
import type { GameServer, ServerAction } from "@/features/hosting/types";

/** Name of the query parameter that deep-links to an action. */
const actionSearchParameter = "action";

/** Interval at which the open dialog re-reads the server to notice a change in availability. */
const liveRefreshMilliseconds = 5_000;

interface ActiveRequest {
  serverId: string;
  actionId: string;
  source: CostConfirmationSource;
  invoker: HTMLElement | null;
  returnFocus: RefObject<HTMLElement | null> | undefined;
  onSubmitted: (() => void) | undefined;
  pathname: string;
}

/**
 * Runs the requester's success callback without letting a failure in it affect the confirmed action's follow-up.
 *
 * @param callback The optional callback supplied with the request.
 */
function notifySubmitted(callback: (() => void) | undefined): void {
  if (!callback) return;
  try {
    callback();
  } catch {
    // The callback only updates the requesting surface; the toast, invalidations and close must still run.
  }
}

interface ExecuteVariables {
  serverId: string;
  actionId: string;
  confirmation: string | undefined;
}

function sameId(left: string, right: string): boolean {
  return left.toLowerCase() === right.toLowerCase();
}

function findAction(server: GameServer | undefined, actionId: string): ServerAction | undefined {
  return server?.actions.find((candidate) => sameId(candidate.id, actionId));
}

/**
 * Owns the single cost confirmation dialog of a route layout.
 *
 * Entry points call `useCostConfirmation().request(...)`; only the confirm handler of the dialog executes the
 * action. The dialog is a user experience guard: the API validates permission, challenge and pending state
 * again for every request.
 *
 * It must be mounted inside the router, the query client, the auth provider and the toast provider.
 */
export function CostConfirmationProvider({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const { showToast } = useAppToast();
  const location = useLocation();
  const [, setSearchParams] = useSearchParams();
  const [active, setActive] = useState<ActiveRequest | null>(null);
  const resolving = useRef<string | null>(null);
  const pathnameRef = useRef(location.pathname);
  const shownRef = useRef(false);
  const pendingRef = useRef(false);

  const stripAction = useCallback(() => {
    setSearchParams(
      (current) => {
        if (!current.has(actionSearchParameter)) return current;
        const next = new URLSearchParams(current);
        next.delete(actionSearchParameter);
        return next;
      },
      { replace: true },
    );
  }, [setSearchParams]);
  const stripActionRef = useRef(stripAction);

  const serverId = active?.serverId ?? "";
  const live = useQuery<GameServer, Error>({
    queryKey: hostingQueryKeys.server(serverId),
    queryFn: async ({ signal }) => {
      const result = await hostingService.server(serverId, signal);
      if (!result) throw new Error("The server is no longer available.");
      return result;
    },
    enabled: active !== null,
    staleTime: liveRefreshMilliseconds,
    refetchInterval: active ? liveRefreshMilliseconds : false,
    placeholderData: () =>
      queryClient
        .getQueryData<GameServer[]>(hostingQueryKeys.fleet)
        ?.find((candidate) => sameId(candidate.primaryKey, serverId)),
  });
  const execute = useMutation({
    mutationFn: ({ serverId: id, actionId, confirmation }: ExecuteVariables) =>
      hostingService.executeAction(id, actionId, confirmation),
  });
  const { reset: resetExecution } = execute;

  const server = live.data;
  const action = active ? findAction(server, active.actionId) : undefined;
  const pathChanged = active !== null && active.pathname !== location.pathname;
  const serverGone = active !== null && live.isError && !server;
  const shown = active !== null && !pathChanged && server !== undefined && action !== undefined;
  const matchesVariables =
    active !== null &&
    execute.variables !== undefined &&
    sameId(execute.variables.serverId, active.serverId) &&
    sameId(execute.variables.actionId, active.actionId);
  const busy = execute.isPending && matchesVariables;
  const requestFailed = execute.isError && matchesVariables;

  // Adjust state while rendering (no effect) when the open request can no longer be shown.
  if (active !== null && (pathChanged || serverGone)) setActive(null);

  useEffect(() => {
    pathnameRef.current = location.pathname;
    stripActionRef.current = stripAction;
    shownRef.current = shown;
    pendingRef.current = execute.isPending;
  });

  const restoreFocus = useCallback((closing: ActiveRequest, refreshed?: Promise<unknown>) => {
    const place = () => {
      const preferred = closing.returnFocus?.current;
      if (preferred?.isConnected) {
        preferred.focus();
        return;
      }
      if (closing.invoker?.isConnected) {
        closing.invoker.focus();
        return;
      }
      document.querySelector<HTMLElement>("main h1")?.focus();
    };
    requestAnimationFrame(place);
    if (refreshed === undefined) return;
    // A confirmed action usually turns the control that asked into its pending state, which React mounts anew when the
    // refreshed server arrives. The control that just got focus then disappears and focus falls to the page body.
    // Place focus once more after that render, and only when focus really was lost, so a move the member made
    // meanwhile is never undone.
    void refreshed.then(() => {
      requestAnimationFrame(() =>
        requestAnimationFrame(() => {
          const active = document.activeElement;
          if (active === null || active === document.body) place();
        }),
      );
    });
  }, []);

  const close = (closing: ActiveRequest, refreshed?: Promise<unknown>) => {
    resetExecution();
    setActive(null);
    if (closing.source === "deep-link") stripAction();
    restoreFocus(closing, refreshed);
  };

  const request = useCallback(
    (incoming: CostConfirmationRequest) => {
      const key = `${incoming.serverId.toLowerCase()}|${incoming.actionId.toLowerCase()}`;
      if (resolving.current === key) return;
      const element = document.activeElement;
      const invoker =
        incoming.source !== "deep-link" &&
        element instanceof HTMLElement &&
        element !== document.body
          ? element
          : null;
      const pathname = pathnameRef.current;

      const fail = () => {
        showToast(t("Server_Access_LoadError"), "error");
        if (incoming.source === "deep-link") stripActionRef.current();
      };
      const open = (found: GameServer) => {
        const target = findAction(found, incoming.actionId);
        if (!target) {
          fail();
          return;
        }
        queryClient.setQueryData(hostingQueryKeys.server(incoming.serverId), found);
        if (!pendingRef.current) resetExecution();
        const next: ActiveRequest = {
          serverId: incoming.serverId,
          actionId: incoming.actionId,
          source: incoming.source,
          invoker,
          returnFocus: incoming.returnFocus,
          onSubmitted: incoming.onSubmitted,
          pathname,
        };
        setActive((current) =>
          current &&
          shownRef.current &&
          sameId(current.serverId, next.serverId) &&
          sameId(current.actionId, next.actionId)
            ? current
            : next,
        );
      };

      const cached =
        queryClient.getQueryData<GameServer>(hostingQueryKeys.server(incoming.serverId)) ??
        queryClient
          .getQueryData<GameServer[]>(hostingQueryKeys.fleet)
          ?.find((candidate) => sameId(candidate.primaryKey, incoming.serverId));
      if (cached) {
        open(cached);
        return;
      }
      resolving.current = key;
      hostingService
        .server(incoming.serverId)
        .then((found) => {
          if (found) open(found);
          else fail();
        })
        .catch(fail)
        .finally(() => {
          resolving.current = null;
        });
    },
    [queryClient, resetExecution, showToast, t],
  );

  const confirm = (confirmation: string | undefined) => {
    if (!active || !server || !action || execute.isPending) return;
    if (!action.enabled || action.pending) return;
    const closing = active;
    execute.mutate(
      { serverId: closing.serverId, actionId: action.id, confirmation },
      {
        onSuccess: () => {
          notifySubmitted(closing.onSubmitted);
          const vocabulary = lookupActionVocabulary(action.id);
          showToast(
            t(toastKeyFor(action.id), {
              name: server.title,
              action: vocabulary ? t(vocabulary.label) : action.label || action.id,
            }),
            "success",
          );
          const refreshed = Promise.allSettled([
            queryClient.invalidateQueries({ queryKey: hostingQueryKeys.fleet }),
            queryClient.invalidateQueries({
              queryKey: hostingQueryKeys.server(closing.serverId),
            }),
            queryClient.invalidateQueries({ queryKey: hostingQueryKeys.jobs(closing.serverId) }),
            queryClient.invalidateQueries({
              queryKey: hostingQueryKeys.operations(closing.serverId),
            }),
            queryClient.invalidateQueries({ queryKey: hostingQueryKeys.notifications }),
          ]);
          close(closing, refreshed);
        },
      },
    );
  };

  const value = useMemo<CostConfirmationContextValue>(() => ({ request }), [request]);
  return (
    <CostConfirmationContext.Provider value={value}>
      {children}
      {shown && active && server && action && (
        <CostConfirmDialog
          key={`${active.serverId}|${active.actionId}`}
          server={server}
          action={action}
          busy={busy}
          requestFailed={requestFailed}
          stale={!action.enabled || action.pending}
          onConfirm={confirm}
          onDismiss={() => {
            if (!busy) close(active);
          }}
        />
      )}
    </CostConfirmationContext.Provider>
  );
}
