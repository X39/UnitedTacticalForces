import type { components } from "@/generated/api";

type Schema<Name extends keyof components["schemas"]> = components["schemas"][Name];
type WithRequired<T, Keys extends keyof T> = T & Required<Pick<T, Keys>>;

export type AuthenticationProviders = Required<Schema<"AuthenticationProvidersDto">>;
export type Claim = WithRequired<Schema<"PlainClaimDto">, "identifier" | "value">;
export type Role = Required<Schema<"RoleDto">>;
export type PlainUser = WithRequired<Schema<"PlainUserDto">, "primaryKey" | "nickname">;
type FullUserSchema = WithRequired<
  Schema<"FullUserDto">,
  "primaryKey" | "nickname" | "isBanned" | "isVerified" | "isDeleted" | "isVereinMember"
>;
export type FullUser = Omit<FullUserSchema, "claims" | "roles"> & {
  claims: Claim[];
  roles: Role[];
};

type UpcomingEventSchema = Required<Schema<"UpcomingEventDto">>;
export type UpcomingEvent = Omit<UpcomingEventSchema, "hostedBy" | "metaAcceptance"> & {
  hostedBy: PlainUser;
  metaAcceptance: UpcomingEventSchema["metaAcceptance"] | -1 | 0 | 1;
};
export type FullEvent = Omit<Required<Schema<"FullEventDto">>, "hostedBy" | "owner"> & {
  hostedBy: PlainUser;
  owner: PlainUser;
};

export type FieldReportSummary = Required<Schema<"FieldReportSummaryDto">>;
export type FieldReport = Required<Schema<"FieldReportDto">>;
export type WikiPageHeader = Required<Schema<"WikiPageHeader">>;
export type WikiPageRevision = Schema<"WikiPageRevisionDto">;
export type WikiPageRevisionHeader = Required<Schema<"WikiPageRevisionHeader">>;
export type WikiPage = Omit<Required<Schema<"WikiPageDto">>, "revisions"> & {
  revisions: WikiPageRevision[] | null;
};
export type VereinSettings = Required<Schema<"VereinSettingsDto">>;
export type ProfileSettings = Required<Schema<"ProfileSettingsDto">>;

type PublishedPublicPageRevision = Required<Schema<"PublishedPublicPageRevisionDto">>;
export type PublishedPublicPage = Omit<Required<Schema<"PublishedPublicPageDto">>, "revision"> & {
  revision: PublishedPublicPageRevision | null;
};
export type SiteImageSetting = Required<Schema<"SiteImageSettingDto">>;

type PublishedRevision = Required<Schema<"PublishedVereinDocumentRevisionDto">>;
export type VereinDocument = Omit<Required<Schema<"PublishedVereinDocumentDto">>, "revision"> & {
  revision: PublishedRevision | null;
};

type VereinMembershipRequestSchema = Required<Schema<"VereinMembershipRequestDto">>;
export type VereinMembershipRequest = Omit<VereinMembershipRequestSchema, "status"> & {
  status: VereinMembershipRequestSchema["status"] | 0 | 1 | 2 | 3 | 4;
};
export type VereinMembershipRequestPayload = Required<Schema<"VereinMembershipRequestPayload">>;

export type EventAcceptance = "accept" | "maybe" | "reject";
