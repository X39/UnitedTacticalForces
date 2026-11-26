import type { PublicConfiguration } from "@/app/config";
import { resolveApiBaseUrl } from "@/app/config";

interface ProblemDetails {
  error?: string;
  title?: string;
  detail?: string;
  status?: number;
  traceId?: string;
  errors?: Record<string, string[]>;
  fields?: unknown;
}

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code:
      | "unauthorized"
      | "forbidden"
      | "not-found"
      | "conflict"
      | "rate-limited"
      | "network"
      | "server"
      | "invalid",
    readonly traceId?: string,
    readonly fieldErrors?: Record<string, string[]>,
    /** The names of the invalid fields of a hosting 400 answer (`{ error, fields }`), when the API lists them. */
    readonly fields?: string[],
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export interface RequestOptions extends Omit<RequestInit, "body" | "signal"> {
  body?: unknown;
  signal?: AbortSignal | undefined;
  expectedStatuses?: number[];
}

function statusCode(status: number): ApiError["code"] {
  if (status === 401) return "unauthorized";
  if (status === 403) return "forbidden";
  if (status === 404) return "not-found";
  if (status === 409) return "conflict";
  if (status === 429) return "rate-limited";
  if (status >= 500) return "server";
  return "invalid";
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) return undefined;
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return undefined;
  }
}

async function responseError(response: Response, fallback?: string): Promise<ApiError> {
  const problem = await readProblem(response);
  return new ApiError(
    problem?.detail ??
      problem?.error ??
      problem?.title ??
      fallback ??
      `Request failed with HTTP ${response.status}.`,
    response.status,
    statusCode(response.status),
    problem?.traceId,
    problem?.errors,
    Array.isArray(problem?.fields)
      ? problem.fields.filter((field): field is string => typeof field === "string")
      : undefined,
  );
}

function networkError(error: unknown): ApiError {
  return new ApiError(
    error instanceof Error ? error.message : "Network request failed.",
    0,
    "network",
  );
}

export class ApiClient {
  readonly baseUrl: URL;

  constructor(configuration: PublicConfiguration) {
    this.baseUrl = resolveApiBaseUrl(configuration);
  }

  url(path: string): URL {
    return new URL(path.replace(/^\//, ""), this.baseUrl);
  }

  async request<T>(path: string, options: RequestOptions = {}): Promise<T | undefined> {
    const { body: requestBody, expectedStatuses, signal, ...requestInit } = options;
    const headers = new Headers(requestInit.headers);
    headers.set("Accept", "application/json, text/plain;q=0.9");
    headers.set("X-UTF-UI-Variant", "react");
    let body: BodyInit | undefined;
    if (
      requestBody instanceof FormData ||
      requestBody instanceof Blob ||
      requestBody instanceof URLSearchParams ||
      requestBody instanceof ArrayBuffer ||
      typeof requestBody === "string"
    ) {
      body = requestBody;
    } else if (requestBody !== undefined) {
      headers.set("Content-Type", "application/json");
      body = JSON.stringify(requestBody);
    }

    let response: Response;
    try {
      response = await fetch(this.url(path), {
        ...requestInit,
        headers,
        credentials: "include",
        ...(body === undefined ? {} : { body }),
        ...(signal ? { signal } : {}),
      });
    } catch (error) {
      if (signal?.aborted) throw error;
      throw networkError(error);
    }

    if (expectedStatuses?.includes(response.status)) return undefined;
    if (!response.ok) throw await responseError(response);
    if (response.status === 204 || response.headers.get("content-length") === "0") return undefined;
    const contentType = response.headers.get("content-type") ?? "";
    const responseBody = await response.text();
    if (!responseBody) return undefined;
    return contentType.includes("json") ? (JSON.parse(responseBody) as T) : (responseBody as T);
  }

  async download(path: string, signal?: AbortSignal): Promise<Blob> {
    let response: Response;
    try {
      response = await fetch(this.url(path), {
        credentials: "include",
        headers: { "X-UTF-UI-Variant": "react" },
        ...(signal ? { signal } : {}),
      });
    } catch (error) {
      if (signal?.aborted) throw error;
      throw networkError(error);
    }
    if (!response.ok)
      throw await responseError(response, `Download failed with HTTP ${response.status}.`);
    return response.blob();
  }
}

let client: ApiClient | undefined;
export function initializeApiClient(configuration: PublicConfiguration): ApiClient {
  client = new ApiClient(configuration);
  return client;
}
export function getApiClient(): ApiClient {
  if (!client) throw new Error("API client was used before application bootstrap completed.");
  return client;
}

export function resolveImageUrl(path: string | null | undefined): string | undefined {
  if (!path) return undefined;
  if (/^https?:\/\//i.test(path)) return path;
  const normalized = path.replace(/^\//, "");
  if (normalized.startsWith("media-assets/") || normalized.startsWith("field-reports/media/"))
    return getApiClient().url(normalized).toString();
  return `/${normalized}`;
}
