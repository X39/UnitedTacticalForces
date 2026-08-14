import { eventsMemberRoutes } from "@/features/events-member";

describe("events/member integration contract", () => {
  it("exports stable route ids and preserves the classic ledger alias", () => {
    expect(eventsMemberRoutes.map((route) => route.id)).toEqual([
      "event-detail",
      "account-settings",
      "verein-ledger",
    ]);
    expect(eventsMemberRoutes.find((route) => route.id === "verein-ledger")?.aliases).toContain(
      "/unit/kassenbuch",
    );
    expect(
      eventsMemberRoutes.find((route) => route.id === "event-detail")?.requiresAuthentication,
    ).toBe(false);
    expect(eventsMemberRoutes.find((route) => route.id === "event-detail")?.path).toBe(
      "/events/:eventId",
    );
  });
});
