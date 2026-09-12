import { gameLabel } from "@/features/hosting/ConfigurationProfileFields";

describe("hosting game labels", () => {
  it("formats registered game identifiers", () => {
    expect(gameLabel("arma3")).toBe("Arma 3");
    expect(gameLabel("aska")).toBe("ASKA");
    expect(gameLabel("cs2")).toBe("Counter-Strike 2");
  });

  it("preserves unknown identifiers", () => {
    expect(gameLabel("future-game")).toBe("future-game");
  });
});
