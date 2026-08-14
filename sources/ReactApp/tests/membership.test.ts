import {
  isOpenMembershipStatus,
  membershipStatusTranslationKey,
  normalizeMembershipStatus,
} from "@/app/membership";
import { membershipApplicationSchema } from "@/pages/JoinPage";

const validApplication = {
  firstName: "Alex",
  lastName: "Example",
  dateOfBirth: "1990-01-01",
  email: "alex@example.test",
  phoneNumber: "+49 123 4567",
  addressLine1: "Example Street 1",
  addressLine2: "",
  postalCode: "12345",
  city: "Berlin",
  country: "Germany",
  motivation: "I want to support the community.",
  acknowledgesMembershipSeparation: true,
  acknowledgesDataProcessing: true,
  acknowledgesSatzung: true,
};

describe("Verein membership behavior", () => {
  it("validates the complete application and acknowledgements", () => {
    expect(membershipApplicationSchema.safeParse(validApplication).success).toBe(true);
    expect(
      membershipApplicationSchema.safeParse({
        ...validApplication,
        email: "invalid",
        acknowledgesSatzung: false,
      }).success,
    ).toBe(false);
  });

  it("normalizes legacy numeric statuses and permits reapplication after terminal states", () => {
    expect(normalizeMembershipStatus(1)).toBe("AcceptedForContact");
    expect(isOpenMembershipStatus(0)).toBe(true);
    expect(isOpenMembershipStatus(4)).toBe(false);
    expect(membershipStatusTranslationKey(3)).toBe("Pages_Public_Join_RequestStatus_Rejected");
  });
});
