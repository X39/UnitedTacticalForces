import { afterEach, describe, expect, it } from "vitest";
import { localReactReturnPath, localReturnUrl } from "@/auth/auth";

describe("authentication return URLs", () => {
  afterEach(() => window.history.replaceState({}, "", "/next/"));

  it("keeps the complete React route local to the current origin", () => {
    window.history.replaceState({}, "", "/next/join?source=profile#application");

    expect(localReturnUrl(new URL(`${window.location.origin}/api/users/login/steam`))).toBe(
      "/next/join?source=profile#application",
    );
  });

  it("uses an allow-listable absolute return for a split API origin", () => {
    window.history.replaceState({}, "", "/next/join?source=profile#application");

    expect(localReturnUrl(new URL("https://api.example.test/users/login/steam"))).toBe(
      "http://localhost:3000/next/join?source=profile#application",
    );
  });

  it("rejects external and classic return targets", () => {
    window.history.replaceState({}, "", "/next/authentication/login");

    expect(localReactReturnPath("https://example.test/next/private")).toBe(
      "/next/authentication/login",
    );
    expect(localReactReturnPath("/command/servers")).toBe("/next/authentication/login");
    expect(localReactReturnPath("/next/command/servers?tab=jobs#latest")).toBe(
      "/next/command/servers?tab=jobs#latest",
    );
  });
});
