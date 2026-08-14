import { ApiError } from "@/api/client";
import type { TranslationKey } from "@/app/localizationTypes";

export type EventFailureContext = "attendance" | "download" | "load" | "slot";

const contextKeys: Record<EventFailureContext, TranslationKey> = {
  attendance: "Pages_Events_Detail_Error_Attendance",
  download: "Pages_Events_Detail_Error_Download",
  load: "Pages_Events_Detail_Error_Load",
  slot: "Pages_Events_Detail_Error_Slot",
};

const statusKeys: Partial<Record<ApiError["code"], TranslationKey>> = {
  unauthorized: "Pages_Events_Detail_Error_SessionExpired",
  forbidden: "Pages_Events_Detail_Error_Forbidden",
  "not-found": "Pages_Events_Detail_Error_NotFound",
  conflict: "Pages_Events_Detail_Error_Conflict",
  "rate-limited": "Pages_Events_Detail_Error_RateLimited",
};

export function eventFailureResourceKey(
  error: unknown,
  context: EventFailureContext,
): TranslationKey {
  if (error instanceof ApiError) {
    const statusKey = statusKeys[error.code];
    if (statusKey) return statusKey;
  }
  return contextKeys[context];
}
