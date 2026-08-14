import { useEffect, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router";
import { currentClassicUrl, matchRoute, routeManifest } from "@/app/routes";

export function shouldFallBackToClassic(pathname: string): boolean {
  if (routeManifest.reactBasePath === "/") return false;
  return matchRoute(pathname)?.certified === false;
}

export function RouteCertificationGate({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const location = useLocation();
  const shouldFallBack = shouldFallBackToClassic(location.pathname);

  useEffect(() => {
    if (!shouldFallBack) return;
    window.location.replace(currentClassicUrl());
  }, [shouldFallBack]);

  if (!shouldFallBack) return children;

  return (
    <main className="route-certification-fallback" role="status" aria-live="polite">
      <img src="/Insignia-48px.png" alt="" width="48" height="48" />
      <p>{t("Common_LoadingData")}</p>
    </main>
  );
}
