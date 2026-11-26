import { describe, expect, it } from "vitest";
import { assertLocalUatTarget, defaultUatBaseUrl } from "../uat/support/uatTarget";

describe("assertLocalUatTarget (T-69-38)", () => {
  it("accepts the default UAT base URL", () => {
    expect(() => assertLocalUatTarget(defaultUatBaseUrl)).not.toThrow();
  });

  it("accepts localhost and 127.0.0.1", () => {
    expect(() => assertLocalUatTarget("http://localhost:5173/next/")).not.toThrow();
    expect(() => assertLocalUatTarget("http://127.0.0.1:5173/next/")).not.toThrow();
  });

  it("rejects a non-local host and names it", () => {
    expect(() => assertLocalUatTarget("https://example.org/next/")).toThrow(/example\.org/);
  });

  it("rejects hosts that only look local", () => {
    expect(() => assertLocalUatTarget("http://localhost.example.org/next/")).toThrow();
    expect(() => assertLocalUatTarget("http://127.0.0.1.example.org/next/")).toThrow();
    expect(() => assertLocalUatTarget("http://localhost@example.org/next/")).toThrow(
      /example\.org/,
    );
    expect(() => assertLocalUatTarget("http://192.168.0.10:5173/next/")).toThrow();
  });

  it("rejects a value that is not a URL", () => {
    expect(() => assertLocalUatTarget("")).toThrow(/not a valid URL/);
    expect(() => assertLocalUatTarget("localhost:5173")).toThrow();
  });
});
