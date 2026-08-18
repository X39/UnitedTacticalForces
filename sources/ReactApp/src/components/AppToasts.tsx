import {
  Toast,
  ToastBody,
  Toaster,
  useId,
  useToastController,
  type ToastIntent,
} from "@fluentui/react-components";
import { createContext, useCallback, useContext, useMemo, type ReactNode } from "react";

type AppToastIntent = Extract<ToastIntent, "success" | "warning" | "error" | "info">;

type AppToastContextValue = {
  showToast(message: string, intent?: AppToastIntent): void;
};

const AppToastContext = createContext<AppToastContextValue | null>(null);

export function AppToastProvider({ children }: { children: ReactNode }) {
  const toasterId = useId("utf-toaster");
  const { dispatchToast } = useToastController(toasterId);
  const showToast = useCallback(
    (message: string, intent: AppToastIntent = "info") => {
      dispatchToast(
        <Toast>
          <ToastBody>{message}</ToastBody>
        </Toast>,
        {
          intent,
          timeout: intent === "error" ? 8_000 : 5_000,
        },
      );
    },
    [dispatchToast],
  );
  const value = useMemo(() => ({ showToast }), [showToast]);

  return (
    <AppToastContext.Provider value={value}>
      {children}
      <Toaster toasterId={toasterId} position="top-end" pauseOnHover />
    </AppToastContext.Provider>
  );
}

export function useAppToast(): AppToastContextValue {
  const context = useContext(AppToastContext);
  if (!context) throw new Error("useAppToast must be used inside AppToastProvider.");
  return context;
}
