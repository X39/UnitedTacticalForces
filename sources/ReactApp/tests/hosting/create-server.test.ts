import { initializeApiClient } from "@/api/client";
import type { PublicConfiguration } from "@/app/config";
import {
  combinedHourlyCost,
  createServerCreationSchema,
  isCostOwnerSelectionValid,
  isProviderAvailable,
  orderProviders,
} from "@/features/hosting/createServerModel";
import { hostingService } from "@/features/hosting/services";
import type {
  RuntimeProfile,
  RuntimeProvider,
  RuntimeStorageCapabilities,
  ServerCreation,
} from "@/features/hosting/types";

const configuration: PublicConfiguration = {
  Api: { BaseUrl: "/api/" },
  Urls: { TeamSpeak3: "", Discord: "" },
  Ui: { ReactVariantEnabled: true, ReactBasePath: "/next" },
};

const messages = {
  titleRequired: "name-required",
  titleTooLong: "name-too-long",
  selectionRequired: "selection-required",
  invalidCapacity: "capacity-invalid",
  invalidIdentifier: "identifier-invalid",
};

describe("server creation model", () => {
  it("trims the title and validates identifiers before any request", () => {
    const schema = createServerCreationSchema(messages);
    const parsed = schema.parse({
      title: "  UTF One  ",
      gameIdentifier: "arma3",
      runtimeProviderIdentifier: "hetzner",
      runtimeProfileIdentifier: "cx32",
      runtimeStorageStrategy: "AutomaticExternal",
      externalStorageSizeGiB: null,
      modPackDefinitionId: 42,
      configurationProfileId: "10000000-0000-4000-8000-000000000001",
      costOwnerId: "10000000-0000-4000-8000-000000000002",
      isLaunchDisabled: false,
    });

    expect(parsed.title).toBe("UTF One");
    expect(
      schema.safeParse({
        ...parsed,
        title: " ",
        costOwnerId: "not-a-guid",
      }).success,
    ).toBe(false);
  });

  it("excludes unavailable providers, sorts choices, and combines storage cost", () => {
    const unavailable = {
      identifier: "zulu",
      availabilityStatus: "configuration-required",
      profiles: [{}],
    } as RuntimeProvider;
    const available = {
      identifier: "alpha",
      availabilityStatus: "AVAILABLE",
      profiles: [{ estimatedHourlyCostEur: 0.1 }],
    } as RuntimeProvider;
    const storage = { estimatedHourlyCostPerGiBEur: 0.001 } as RuntimeStorageCapabilities;

    expect(isProviderAvailable(unavailable)).toBe(false);
    expect(isProviderAvailable(available)).toBe(true);
    expect(orderProviders([unavailable, available]).map((provider) => provider.identifier)).toEqual(
      ["alpha", "zulu"],
    );
    expect(
      combinedHourlyCost(
        available.profiles[0] as RuntimeProfile,
        50 * 1_073_741_824,
        "FixedExternal",
        storage,
      ),
    ).toBeCloseTo(0.15);
  });

  it("accepts only assignable, non-frozen cost owners", () => {
    const personal = {
      costOwnerId: "10000000-0000-4000-8000-000000000001",
      displayName: "Personal",
      isFrozen: false,
      isCurrentUser: true,
    };
    const frozen = {
      costOwnerId: "10000000-0000-4000-8000-000000000002",
      displayName: "Frozen",
      isFrozen: true,
      isCurrentUser: false,
    };

    expect(isCostOwnerSelectionValid(null, [personal, frozen])).toBe(true);
    expect(isCostOwnerSelectionValid(frozen.costOwnerId, [personal, frozen])).toBe(false);
    expect(isCostOwnerSelectionValid("10000000-0000-4000-8000-000000000099", [personal])).toBe(
      false,
    );
    expect(isCostOwnerSelectionValid(null, undefined)).toBe(false);
  });
});

describe("server creation service", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("loads only the scoped cost-owner choices required by the creation form", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify([
          {
            costOwnerId: "10000000-0000-4000-8000-000000000002",
            displayName: "Shared budget",
            isFrozen: false,
            isCurrentUser: false,
          },
        ]),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);

    const owners = await hostingService.creationCostOwners();

    expect(owners[0]?.displayName).toBe("Shared budget");
    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/game-servers/creation/cost-owner-options");
    expect(init.credentials).toBe("include");
    expect(new Headers(init.headers).get("X-UTF-UI-Variant")).toBe("react");
  });

  it("posts the complete creation payload without contacting a runtime provider", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          primaryKey: "10000000-0000-4000-8000-000000000003",
          actions: null,
          deployment: null,
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);
    initializeApiClient(configuration);
    const payload = {
      title: "UTF One",
      gameIdentifier: "arma3",
      runtimeProviderIdentifier: "hetzner",
      runtimeProfileIdentifier: "cx32",
      runtimeStorageStrategy: "FixedExternal",
      externalStorageSizeGiB: 80,
      modPackDefinitionId: 42,
      configurationProfileId: "10000000-0000-4000-8000-000000000001",
      configurationProfileRevisionId: null,
      costOwnerId: "10000000-0000-4000-8000-000000000002",
      isLaunchDisabled: true,
    } as ServerCreation;

    await hostingService.create(payload);

    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(url.pathname).toBe("/api/game-servers");
    expect(init.method).toBe("POST");
    expect(JSON.parse(String(init.body))).toEqual(payload);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
