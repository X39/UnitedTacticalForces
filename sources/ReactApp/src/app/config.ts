import { z } from "zod";

const publicConfigurationSchema = z.object({
  Api: z.object({
    BaseUrl: z
      .string()
      .trim()
      .min(1)
      .refine((value) => {
        try {
          return ["http:", "https:"].includes(
            new URL(value, "https://configuration.invalid").protocol,
          );
        } catch {
          return false;
        }
      }, "Api:BaseUrl must be a relative or HTTP(S) URL."),
  }),
  Urls: z
    .object({
      TeamSpeak3: z.string().optional().default(""),
      Discord: z.string().optional().default(""),
    })
    .optional()
    .default({ TeamSpeak3: "", Discord: "" }),
  Ui: z
    .object({
      ReactVariantEnabled: z.boolean().optional().default(false),
      ReactBasePath: z
        .string()
        .refine((value) => {
          if (value === "/next" || value === "/") return true;
          try {
            const url = new URL(value);
            return (
              ["http:", "https:"].includes(url.protocol) &&
              url.pathname.replace(/\/$/, "") === "/next"
            );
          } catch {
            return false;
          }
        })
        .optional()
        .default(import.meta.env.BASE_URL === "/" ? "/" : "/next"),
    })
    .optional()
    .default({
      ReactVariantEnabled: false,
      ReactBasePath: import.meta.env.BASE_URL === "/" ? "/" : "/next",
    }),
});

export type PublicConfiguration = z.infer<typeof publicConfigurationSchema>;

let configurationPromise: Promise<PublicConfiguration> | undefined;

async function fetchPublicConfiguration(url: string): Promise<PublicConfiguration> {
  const response = await fetch(url, {
    cache: "no-store",
    credentials: "same-origin",
  });
  if (!response.ok) throw new Error(`Public configuration returned HTTP ${response.status}.`);
  return publicConfigurationSchema.parse(await response.json());
}

export function resolveReactConfigurationUrl(baseUrl = import.meta.env.BASE_URL): string {
  return `${baseUrl.endsWith("/") ? baseUrl : `${baseUrl}/`}appsettings.json`;
}

export function loadPublicConfiguration(): Promise<PublicConfiguration> {
  configurationPromise ??= fetchPublicConfiguration("/appsettings.json")
    .catch((error: unknown) => {
      if (!["localhost", "127.0.0.1"].includes(window.location.hostname)) throw error;
      return fetchPublicConfiguration(resolveReactConfigurationUrl());
    })
    .catch((error: unknown) => {
      const reason = error instanceof Error ? error.message : "The response was invalid.";
      throw new Error(`United Tactical Forces could not start. ${reason}`);
    });
  return configurationPromise;
}

export function resolveApiBaseUrl(configuration: PublicConfiguration): URL {
  if (import.meta.env.DEV) return new URL("/api/", window.location.origin);
  const configured = configuration.Api.BaseUrl.trim();
  const resolved = new URL(configured, window.location.origin);
  if (!resolved.pathname.endsWith("/")) resolved.pathname += "/";
  return resolved;
}
