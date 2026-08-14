import { ApiError } from "@/api/client";
import { eventFailureResourceKey } from "@/features/events-member/eventErrors";

describe("event detail failure mapping", () => {
  it.each([
    [401, "unauthorized", "Pages_Events_Detail_Error_SessionExpired"],
    [403, "forbidden", "Pages_Events_Detail_Error_Forbidden"],
    [404, "not-found", "Pages_Events_Detail_Error_NotFound"],
    [409, "conflict", "Pages_Events_Detail_Error_Conflict"],
    [429, "rate-limited", "Pages_Events_Detail_Error_RateLimited"],
  ] as const)("maps HTTP %s to a safe localized state", (status, code, expectedKey) => {
    expect(eventFailureResourceKey(new ApiError("server detail", status, code), "slot")).toBe(
      expectedKey,
    );
  });

  it("uses action-specific messages for network and unknown failures", () => {
    expect(eventFailureResourceKey(new Error("offline"), "attendance")).toBe(
      "Pages_Events_Detail_Error_Attendance",
    );
    expect(eventFailureResourceKey(new Error("offline"), "download")).toBe(
      "Pages_Events_Detail_Error_Download",
    );
    expect(eventFailureResourceKey(new Error("offline"), "load")).toBe(
      "Pages_Events_Detail_Error_Load",
    );
  });
});
