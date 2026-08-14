import { getApiClient } from "@/api/client";
import { apiMethod, apiPath, apiPathWithQuery, fillApiPath } from "@/api/contract";
import type { ProfileSettings } from "@/api/types";
import type { EventAssignee, EventSlot, KassenbuchPage, ProfileSettingsUpdate } from "./types";

export const eventsMemberQueryKeys = {
  slots: (eventId: string) => ["events", eventId, "slots"] as const,
  assignees: (eventId: string, userIds: string[]) =>
    ["events", eventId, "assignees", [...userIds].sort().join(",")] as const,
  profileSettings: ["users", "me", "settings"] as const,
  vereinLedger: ["verein", "kassenbuch"] as const,
};

async function loadAssignees(userIds: string[], signal?: AbortSignal) {
  const uniqueIds = [...new Set(userIds)];
  const users = await Promise.all(
    uniqueIds.map(async (userId) => {
      try {
        return await getApiClient().request<EventAssignee>(
          fillApiPath("/users/{userId}", { userId }),
          { signal, expectedStatuses: [204, 404] },
        );
      } catch {
        return undefined;
      }
    }),
  );
  return new Map(
    users
      .filter((user): user is EventAssignee => Boolean(user?.primaryKey))
      .map((user) => [user.primaryKey, user]),
  );
}

export const eventsMemberService = {
  slots: async (eventId: string, signal?: AbortSignal): Promise<EventSlot[]> =>
    (await getApiClient().request<EventSlot[]>(
      fillApiPath("/events/{eventId}/slotting/all", { eventId }),
      { signal, expectedStatuses: [204] },
    )) ?? [],

  assignees: loadAssignees,

  assignSlot: (eventId: string, slotNumber: number, signal?: AbortSignal) => {
    const path = "/events/{eventId}/slotting/{slotNumber}/assign" as const;
    return getApiClient().request<void>(fillApiPath(path, { eventId, slotNumber }), {
      method: apiMethod(path, "post"),
      signal,
    });
  },

  updateProfileSettings: (payload: ProfileSettingsUpdate, signal?: AbortSignal) => {
    const path = "/users/me/settings" as const;
    return getApiClient().request<ProfileSettings>(apiPath(path), {
      method: apiMethod(path, "post"),
      body: payload,
      signal,
    });
  },

  downloadModPack: (path: string, signal?: AbortSignal) => getApiClient().download(path, signal),

  vereinLedger: async (signal?: AbortSignal): Promise<KassenbuchPage> => {
    const result = await getApiClient().request<KassenbuchPage>(
      apiPathWithQuery("/verein/kassenbuch", "skip=0&take=100"),
      { signal },
    );
    return {
      entries: result?.entries ?? [],
      totalCount: result?.totalCount ?? 0,
      incomeTotal: result?.incomeTotal ?? 0,
      expenseTotal: result?.expenseTotal ?? 0,
      balance: result?.balance ?? 0,
      currency: result?.currency ?? "EUR",
    };
  },
};

export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.hidden = true;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

export function submitAccountDeletion(returnUrl: string): void {
  const target = getApiClient().url("users/me/delete");
  target.searchParams.set("returnUrl", returnUrl);
  const form = document.createElement("form");
  form.method = "POST";
  form.action = target.toString();
  form.hidden = true;
  document.body.append(form);
  form.submit();
}
