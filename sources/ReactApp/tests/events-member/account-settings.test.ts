import type { ProfileSettings } from "@/api/types";
import {
  avatarFileToPayload,
  profileFormToPayload,
  profileSettingsSchema,
  profileSettingsToForm,
} from "@/features/events-member/AccountSettingsPage";

const settings: ProfileSettings = {
  primaryKey: "34b29531-739a-41eb-983c-49c105f3475e",
  nickname: "Player",
  eMail: null,
  avatar: "",
  avatarMimeType: "",
  isVerified: true,
  isVereinMember: false,
  vereinMembershipStatus: null,
  vereinMemberNumber: null,
  canDeleteAccount: true,
  accountDeletionBlockedReason: null,
  steamId64: null,
  discordId: null,
  discordUsername: null,
  firstName: null,
  lastName: null,
  dateOfBirth: null,
  phoneNumber: null,
  addressLine1: null,
  addressLine2: null,
  postalCode: null,
  city: null,
  country: null,
};

describe("account settings mapping", () => {
  it("normalizes nullable server fields to controlled form values", () => {
    expect(profileSettingsToForm(settings)).toEqual({
      nickname: "Player",
      eMail: "",
      firstName: "",
      lastName: "",
      dateOfBirth: "",
      phoneNumber: "",
      addressLine1: "",
      addressLine2: "",
      postalCode: "",
      city: "",
      country: "",
    });
  });

  it("requires a nickname and rejects invalid email and birth dates", () => {
    const valid = profileSettingsToForm(settings);
    expect(profileSettingsSchema.safeParse(valid).success).toBe(true);
    expect(profileSettingsSchema.safeParse({ ...valid, nickname: " " }).success).toBe(false);
    expect(profileSettingsSchema.safeParse({ ...valid, eMail: "invalid" }).success).toBe(false);
    expect(profileSettingsSchema.safeParse({ ...valid, dateOfBirth: "not-a-date" }).success).toBe(
      false,
    );
  });

  it("trims the nickname and includes an explicitly selected avatar", () => {
    const payload = profileFormToPayload(
      { ...profileSettingsToForm(settings), nickname: "  Player Two  " },
      { bytes: "AQID", mimeType: "image/png" },
    );
    expect(payload).toMatchObject({
      nickname: "Player Two",
      avatar: "AQID",
      avatarMimeType: "image/png",
      dateOfBirth: null,
    });
  });

  it("accepts the image formats supported by the classic client and API", async () => {
    for (const type of [
      "image/png",
      "image/jpeg",
      "image/webp",
      "image/avif",
      "image/bmp",
      "image/gif",
    ])
      expect(await avatarFileToPayload(testFile(type))).toMatchObject({ mimeType: type });
    expect(await avatarFileToPayload(testFile("image/svg+xml"))).toBeUndefined();
  });
});

function testFile(type: string): File {
  const file = new File([new Uint8Array([1, 2, 3])], "avatar", { type });
  Object.defineProperty(file, "arrayBuffer", {
    value: async () => new Uint8Array([1, 2, 3]).buffer,
  });
  return file;
}
