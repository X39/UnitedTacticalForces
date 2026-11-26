import { eventService } from "@/api/services";
import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import { eventsMemberService } from "@/features/events-member/services";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const eventId = "83d25ddd-9ff5-4d09-99ab-4f49d91a7c3f";

describe("event detail API service", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("treats missing detail and an empty public slot list as non-exceptional", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await expect(eventService.detail(eventId)).resolves.toBeNull();
    await expect(eventsMemberService.slots(eventId)).resolves.toEqual([]);
  });

  it("uses only the self-assignment and attendance endpoints exposed by the classic workflow", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    await eventsMemberService.assignSlot(eventId, 7);
    await eventService.setAcceptance(eventId, "maybe");

    const [assignUrl, assignInit] = fetchMock.mock.calls[0] as [URL, RequestInit];
    const [attendanceUrl, attendanceInit] = fetchMock.mock.calls[1] as [URL, RequestInit];
    expect(assignUrl.pathname).toBe(`/api/events/${eventId}/slotting/7/assign`);
    expect(assignInit.method).toBe("POST");
    expect(attendanceUrl.pathname).toBe(`/api/events/${eventId}/maybe`);
    expect(attendanceInit.method).toBe("POST");
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
