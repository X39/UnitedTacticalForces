import {
  CheckmarkCircle20Regular,
  DismissCircle20Regular,
  Info20Regular,
  Warning20Regular,
} from "@fluentui/react-icons";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { useTranslation } from "react-i18next";
import { useLocation, useNavigate } from "react-router";
import "@/components/shared-primitives.css";

type AppToastIntent = "success" | "warning" | "error" | "info";
type AppToastPauseReason = "focus" | "pointer";

interface AppToast {
  id: number;
  intent: AppToastIntent;
  message: string;
}

interface AppToastTimer {
  handle: number | null;
  pauseReasons: Set<AppToastPauseReason>;
  remaining: number;
  startedAt: number;
}

type AppToastContextValue = {
  showToast(message: string, intent?: AppToastIntent): void;
};

const AppToastContext = createContext<AppToastContextValue | null>(null);

function ToastIcon({ intent }: { intent: AppToastIntent }) {
  if (intent === "success") return <CheckmarkCircle20Regular aria-hidden="true" />;
  if (intent === "warning") return <Warning20Regular aria-hidden="true" />;
  if (intent === "error") return <DismissCircle20Regular aria-hidden="true" />;
  return <Info20Regular aria-hidden="true" />;
}

/**
 * Provides the toast API and renders the toast viewport.
 *
 * The viewport always contains one polite and one assertive live region, also while no toast is shown. A live region
 * must exist in the accessibility tree before its content changes, because a region that is injected together with
 * its content is often not announced (WR-04). Success, info and warning toasts are added to the polite region and
 * error toasts to the assertive one; `aria-atomic="false"` with `aria-relevant="additions text"` announces each new
 * toast once without reading the earlier ones again.
 *
 * The regions carry `aria-live` instead of `role="status"` or `role="alert"`: an always-present status and alert
 * element would collide with the single status and alert panels of every page. A toast has no role of its own.
 *
 * @param props.children The application content that may show toasts through {@link useAppToast}.
 */
export function AppToastProvider({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const [toasts, setToasts] = useState<AppToast[]>([]);
  const nextId = useRef(1);
  const timers = useRef(new Map<number, AppToastTimer>());
  const dismissToast = useCallback((id: number) => {
    const timer = timers.current.get(id);
    if (timer?.handle !== null && timer?.handle !== undefined) window.clearTimeout(timer.handle);
    timers.current.delete(id);
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);
  const pauseToast = useCallback((id: number, reason: AppToastPauseReason) => {
    const timer = timers.current.get(id);
    if (!timer) return;
    timer.pauseReasons.add(reason);
    if (timer.handle === null) return;
    window.clearTimeout(timer.handle);
    timer.handle = null;
    timer.remaining = Math.max(0, timer.remaining - (Date.now() - timer.startedAt));
  }, []);
  const resumeToast = useCallback(
    (id: number, reason: AppToastPauseReason) => {
      const timer = timers.current.get(id);
      if (!timer) return;
      timer.pauseReasons.delete(reason);
      if (timer.pauseReasons.size || timer.handle !== null) return;
      timer.startedAt = Date.now();
      timer.handle = window.setTimeout(() => dismissToast(id), Math.max(timer.remaining, 1));
    },
    [dismissToast],
  );
  const showToast = useCallback(
    (message: string, intent: AppToastIntent = "info") => {
      const id = nextId.current++;
      const duration = intent === "error" ? 8_000 : 5_000;
      setToasts((current) => [...current, { id, intent, message }]);
      const timer: AppToastTimer = {
        handle: null,
        pauseReasons: new Set(),
        remaining: duration,
        startedAt: Date.now(),
      };
      timer.handle = window.setTimeout(() => dismissToast(id), duration);
      timers.current.set(id, timer);
    },
    [dismissToast],
  );
  const value = useMemo(() => ({ showToast }), [showToast]);

  useEffect(
    () => () => {
      for (const timer of timers.current.values()) {
        if (timer.handle !== null) window.clearTimeout(timer.handle);
      }
      timers.current.clear();
    },
    [],
  );

  const renderToast = (toast: AppToast) => (
    <div
      key={toast.id}
      className="app-toast"
      data-intent={toast.intent}
      onMouseEnter={() => pauseToast(toast.id, "pointer")}
      onMouseLeave={() => resumeToast(toast.id, "pointer")}
      onFocus={() => pauseToast(toast.id, "focus")}
      onBlur={(event) => {
        if (event.currentTarget.contains(event.relatedTarget as Node | null)) return;
        resumeToast(toast.id, "focus");
      }}
    >
      <span className="app-toast__icon">
        <ToastIcon intent={toast.intent} />
      </span>
      <p>{toast.message}</p>
      <button
        type="button"
        className="app-toast__dismiss"
        aria-label={t("Common_Close")}
        onClick={() => dismissToast(toast.id)}
      >
        <span aria-hidden="true">×</span>
      </button>
    </div>
  );

  return (
    <AppToastContext.Provider value={value}>
      {children}
      <div className="app-toast-viewport">
        <div
          className="app-toast-region"
          aria-live="polite"
          aria-atomic="false"
          aria-relevant="additions text"
        >
          {toasts.filter((toast) => toast.intent !== "error").map(renderToast)}
        </div>
        <div
          className="app-toast-region"
          aria-live="assertive"
          aria-atomic="false"
          aria-relevant="additions text"
        >
          {toasts.filter((toast) => toast.intent === "error").map(renderToast)}
        </div>
      </div>
    </AppToastContext.Provider>
  );
}

export function useAppToast(): AppToastContextValue {
  const context = useContext(AppToastContext);
  if (!context) throw new Error("useAppToast must be used inside AppToastProvider.");
  return context;
}

export function useAppToastOnChange(
  message: string | null | undefined,
  intent: AppToastIntent = "success",
): void {
  const context = useContext(AppToastContext);
  const previous = useRef<string | null>(null);

  useEffect(() => {
    if (!message) {
      previous.current = null;
      return;
    }
    if (previous.current === message) return;
    previous.current = message;
    context?.showToast(message, intent);
  }, [context, intent, message]);
}

export function useAppRouteToast(
  message: string | null | undefined,
  intent: AppToastIntent = "success",
): void {
  const context = useContext(AppToastContext);
  const location = useLocation();
  const navigate = useNavigate();
  const consumedLocationKey = useRef<string | undefined>(undefined);

  useEffect(() => {
    if (!message || consumedLocationKey.current === location.key) return;
    consumedLocationKey.current = location.key;
    context?.showToast(message, intent);
    void navigate(
      {
        pathname: location.pathname,
        search: location.search,
        hash: location.hash,
      },
      { replace: true, state: null },
    );
  }, [
    context,
    intent,
    location.hash,
    location.key,
    location.pathname,
    location.search,
    message,
    navigate,
  ]);
}
