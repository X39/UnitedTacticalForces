import { useQuery } from "@tanstack/react-query";
import { createContext, useContext, type ReactNode } from "react";
import { getApiClient } from "@/api/client";
import { identityService, queryKeys } from "@/api/services";
import type { AuthenticationProviders, FullUser } from "@/api/types";
import { isAuthenticated } from "@/auth/claims";
import { routeManifest } from "@/app/routes";

interface AuthContextValue {
  user: FullUser | undefined;
  providers: AuthenticationProviders;
  providersLoading: boolean;
  isLoading: boolean;
  isAuthenticated: boolean;
  login(provider: "steam" | "discord", returnUrl?: string): void;
  logout(returnUrl?: string): void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function submitNavigation(action: string): void {
  const form = document.createElement("form");
  form.method = "POST";
  form.action = action;
  form.hidden = true;
  document.body.append(form);
  form.submit();
}

function currentLocalPath(): string {
  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

export function localReactReturnPath(value?: string | null, fallback = currentLocalPath()): string {
  if (!value) return fallback;
  try {
    const candidate = new URL(value, window.location.origin);
    if (candidate.origin !== window.location.origin) return fallback;
    const base = routeManifest.reactBasePath;
    if (base !== "/" && candidate.pathname !== base && !candidate.pathname.startsWith(`${base}/`))
      return fallback;
    return `${candidate.pathname}${candidate.search}${candidate.hash}`;
  } catch {
    return fallback;
  }
}

export function localReturnUrl(apiUrl?: URL, requestedReturnUrl?: string): string {
  const path = localReactReturnPath(requestedReturnUrl);
  return apiUrl && apiUrl.origin !== window.location.origin
    ? new URL(path, window.location.origin).toString()
    : path;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const me = useQuery({
    queryKey: queryKeys.me,
    queryFn: async ({ signal }) => (await identityService.me(signal)) ?? null,
    retry: false,
  });
  const providers = useQuery({
    queryKey: queryKeys.authenticationProviders,
    queryFn: ({ signal }) => identityService.providers(signal),
    staleTime: 5 * 60 * 1000,
  });
  const value: AuthContextValue = {
    user: me.data ?? undefined,
    providers: providers.data ?? { steam: false, discord: false },
    providersLoading: providers.isLoading,
    isLoading: me.isLoading,
    isAuthenticated: isAuthenticated(me.data ?? undefined),
    login(provider, returnUrl) {
      const target = getApiClient().url(`users/login/${provider}`);
      target.searchParams.set("returnUrl", localReturnUrl(target, returnUrl));
      window.location.assign(target);
    },
    logout(returnUrl) {
      const target = getApiClient().url("users/logout");
      target.searchParams.set("returnUrl", localReturnUrl(target, returnUrl));
      submitNavigation(target.toString());
    },
  };
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used inside AuthProvider.");
  return context;
}
