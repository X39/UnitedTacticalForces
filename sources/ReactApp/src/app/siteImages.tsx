import { useQuery } from "@tanstack/react-query";
import { createContext, useContext, useMemo, type ReactNode } from "react";
import { resolveImageUrl } from "@/api/client";
import { queryKeys, siteImageService } from "@/api/services";

const SiteImagesContext = createContext<Record<string, string>>({});

export function SiteImagesProvider({ children }: { children: ReactNode }) {
  const query = useQuery({
    queryKey: queryKeys.siteImages,
    queryFn: ({ signal }) => siteImageService.list(signal),
    staleTime: 5 * 60 * 1000,
  });
  const images = useMemo(
    () =>
      Object.fromEntries(
        (query.data ?? [])
          .filter((setting) => setting.key && setting.imagePath)
          .map((setting) => [setting.key, setting.imagePath]),
      ),
    [query.data],
  );
  return <SiteImagesContext.Provider value={images}>{children}</SiteImagesContext.Provider>;
}

export function useSiteImage(key: string, fallback: string): string {
  return resolveSiteImage(useSiteImages(), key, fallback);
}

export function useSiteImages(): Readonly<Record<string, string>> {
  return useContext(SiteImagesContext);
}

export function resolveSiteImage(
  images: Readonly<Record<string, string>>,
  key: string,
  fallback: string,
): string {
  return resolveImageUrl(images[key]) ?? fallback;
}
