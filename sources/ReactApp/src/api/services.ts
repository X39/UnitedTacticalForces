import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath, type ApiPath } from "@/api/contract";
import type {
  AuthenticationProviders,
  EventAcceptance,
  FieldReport,
  FieldReportSummary,
  FullEvent,
  FullUser,
  ProfileSettings,
  PublishedPublicPage,
  SiteImageSetting,
  UpcomingEvent,
  VereinDocument,
  VereinMembershipRequest,
  VereinMembershipRequestPayload,
  VereinSettings,
  WikiPage,
  WikiPageHeader,
} from "@/api/types";

export const queryKeys = {
  me: ["me"] as const,
  authenticationProviders: ["authentication-providers"] as const,
  upcomingEvents: ["events", "upcoming"] as const,
  event: (eventId: string) => ["events", eventId] as const,
  fieldReports: ["field-reports"] as const,
  fieldReport: (slug: string) => ["field-reports", slug] as const,
  wikiHeaders: ["wiki", "headers"] as const,
  wikiPage: (pageId: string) => ["wiki", pageId] as const,
  vereinSettings: ["verein", "settings"] as const,
  vereinStatutes: ["verein", "documents", "satzung"] as const,
  vereinMinutes: ["verein", "documents", "minutes"] as const,
  vereinMinute: (id: string) => ["verein", "documents", "minutes", id] as const,
  membershipRequest: ["verein", "membership-request", "me"] as const,
  publicPage: (slug: string) => ["public-pages", slug] as const,
  siteImages: ["site-image-settings"] as const,
  profileSettings: ["users", "me", "settings"] as const,
};

const acceptancePaths = {
  accept: "/events/{eventId}/accept",
  maybe: "/events/{eventId}/maybe",
  reject: "/events/{eventId}/reject",
} as const satisfies Record<EventAcceptance, ApiPath>;

export const identityService = {
  me: (signal?: AbortSignal) =>
    getApiClient().request<FullUser>(apiPath("/users/me"), {
      signal,
      expectedStatuses: [204, 401, 404],
    }),
  providers: (signal?: AbortSignal) =>
    getApiClient().request<AuthenticationProviders>(apiPath("/users/authentication-providers"), {
      signal,
    }),
  profileSettings: (signal?: AbortSignal) =>
    getApiClient().request<ProfileSettings>(apiPath("/users/me/settings"), { signal }),
  /**
   * Records the language the signed-in user reads messages in, such as Discord and email cost alerts.
   *
   * Callers that fire and forget must ignore a rejection themselves, for example with `.catch(() => undefined)`.
   *
   * @param language The interface language to record.
   */
  recordLocale: async (language: "en" | "de"): Promise<void> => {
    const path = "/users/me/locale" as const;
    await getApiClient().request<void>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: { locale: language },
    });
  },
};

// Every read that declares expected "none" statuses (204 or 404) maps that answer to null: TanStack Query treats a
// query function result of undefined as a failed load, which would show the error state instead of the empty state.
// Only the declared statuses become null; 401, 403 and 5xx keep rejecting with an ApiError.
export const eventService = {
  upcoming: (signal?: AbortSignal) =>
    getApiClient().request<UpcomingEvent[]>(apiPath("/events/upcoming"), { signal }),
  detail: async (eventId: string, signal?: AbortSignal) =>
    (await getApiClient().request<FullEvent>(fillApiPath("/events/{eventId}", { eventId }), {
      signal,
      expectedStatuses: [204, 404],
    })) ?? null,
  setAcceptance: (eventId: string, acceptance: EventAcceptance, signal?: AbortSignal) => {
    const path = acceptancePaths[acceptance];
    return getApiClient().request<void>(fillApiPath(path, { eventId }), {
      method: apiMethod(path, "post"),
      signal,
    });
  },
};

export const fieldReportService = {
  list: (signal?: AbortSignal) =>
    getApiClient().request<FieldReportSummary[]>(
      apiPathWithQuery("/field-reports", "skip=0&take=48"),
      { signal },
    ),
  detail: async (slug: string, signal?: AbortSignal) =>
    (await getApiClient().request<FieldReport>(fillApiPath("/field-reports/{slug}", { slug }), {
      signal,
      expectedStatuses: [204, 404],
    })) ?? null,
};

export const wikiService = {
  headers: (signal?: AbortSignal) =>
    getApiClient().request<WikiPageHeader[]>(apiPath("/wiki/all/header"), { signal }),
  page: (pageId: string, signal?: AbortSignal) =>
    getApiClient().request<WikiPage>(fillApiPath("/wiki/{pageKey}", { pageKey: pageId }), {
      signal,
    }),
};

export const publicPageService = {
  published: async (slug: string, signal?: AbortSignal) =>
    (await getApiClient().request<PublishedPublicPage>(
      fillApiPath("/public-pages/{slug}", { slug }),
      { signal, expectedStatuses: [204, 404] },
    )) ?? null,
};

export const siteImageService = {
  list: (signal?: AbortSignal) =>
    getApiClient().request<SiteImageSetting[]>(apiPath("/site-image-settings"), { signal }),
};

export const vereinService = {
  settings: (signal?: AbortSignal) =>
    getApiClient().request<VereinSettings>(apiPath("/verein/settings"), { signal }),
  // The three reads below map an expected "none" answer to null, as explained above `eventService`.
  statutes: async (signal?: AbortSignal) =>
    (await getApiClient().request<VereinDocument>(apiPath("/verein/documents/satzung"), {
      signal,
      expectedStatuses: [204, 404],
    })) ?? null,
  minutes: (signal?: AbortSignal) =>
    getApiClient().request<VereinDocument[]>(apiPath("/verein/documents/minutes"), { signal }),
  minute: async (documentId: string, signal?: AbortSignal) =>
    (await getApiClient().request<VereinDocument>(
      fillApiPath("/verein/documents/minutes/{documentId}", { documentId }),
      { signal, expectedStatuses: [204, 404] },
    )) ?? null,
  myMembershipRequest: async (signal?: AbortSignal) =>
    (await getApiClient().request<VereinMembershipRequest>(
      apiPath("/verein/membership-requests/me"),
      { signal, expectedStatuses: [204, 401, 404] },
    )) ?? null,
  createMembershipRequest: (payload: VereinMembershipRequestPayload, signal?: AbortSignal) =>
    getApiClient().request<VereinMembershipRequest>(apiPath("/verein/membership-requests"), {
      method: apiMethod("/verein/membership-requests", "post"),
      body: payload,
      signal,
    }),
  withdrawMembershipRequest: (requestId: number, signal?: AbortSignal) =>
    getApiClient().request<VereinMembershipRequest>(
      fillApiPath("/verein/membership-requests/{requestId}/withdraw", { requestId }),
      {
        method: apiMethod("/verein/membership-requests/{requestId}/withdraw", "post"),
        signal,
      },
    ),
};
