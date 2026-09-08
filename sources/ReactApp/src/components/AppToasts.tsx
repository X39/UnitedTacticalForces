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

  return (
    <AppToastContext.Provider value={value}>
      {children}
      <div className="app-toast-viewport">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className="app-toast"
            data-intent={toast.intent}
            role={toast.intent === "error" ? "alert" : "status"}
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
        ))}
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
