import i18n, { i18nReady } from "@/app/i18n";

describe("typed plural keys", () => {
  beforeAll(async () => i18nReady);

  afterEach(async () => {
    await i18n.changeLanguage("en");
  });

  it("renders the English plural forms", async () => {
    await i18n.changeLanguage("en");

    expect(i18n.t("Server_Progress_MoreQueued", { count: 1 })).toBe("1 more change is waiting.");
    expect(i18n.t("Server_Progress_MoreQueued", { count: 2 })).toBe("2 more changes are waiting.");
  });

  it("renders the German plural forms", async () => {
    await i18n.changeLanguage("de");

    expect(i18n.t("Server_Progress_MoreQueued", { count: 1 })).toBe("1 weitere Änderung wartet.");
    expect(i18n.t("Server_Progress_MoreQueued", { count: 2 })).toBe("2 weitere Änderungen warten.");
  });
});
