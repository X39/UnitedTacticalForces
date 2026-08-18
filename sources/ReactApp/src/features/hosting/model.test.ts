import { describe, expect, it } from "vitest";
import { formatBytes } from "@/features/hosting/model";

describe("formatBytes", () => {
  it.each([
    [null, "—"],
    [undefined, "—"],
    [0, "0 B"],
    [842, "842 B"],
    [1023, "1,023 B"],
    [1024, "1 KiB"],
    [1024 * 1.5, "1.5 KiB"],
    [1024 ** 2, "1 MiB"],
    [1024 ** 3, "1 GiB"],
    [1024 ** 4, "1 TiB"],
  ])("formats %s as %s", (bytes, expected) => {
    expect(formatBytes(bytes)).toBe(expected);
  });
});
