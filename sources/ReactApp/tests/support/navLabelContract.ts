/** One entry of the closed navigation label list (CON-03, D-04). */
export interface NavLabelContractEntry {
  /** The locale key that carries the label. */
  key: string;
  /** The required English value, in sentence case. */
  en: string;
  /** The required German value. */
  de: string;
  /**
   * Set when the destination page `h1` is allowed to differ from the label; states why. A destination without
   * this field has an `h1` that equals its label.
   */
  h1Exception?: string | undefined;
}

/**
 * The closed, sentence-case navigation label list of the command center and the community layout.
 *
 * Group labels are included. Adding a navigation label key to `commandNavigation.tsx` or `CommunityLayout.tsx`
 * without adding it here fails `tests/locales/nav-labels.test.ts`.
 */
export const navLabelContract: readonly NavLabelContractEntry[] = [
  {
    key: "Shared_CommandCenter_Nav_Overview",
    en: "Overview",
    de: "Übersicht",
    h1Exception: "The h1 may read the command center name.",
  },
  { key: "Hosting_Nav_Servers", en: "Servers", de: "Server" },
  {
    key: "Shared_CommandCenter_Nav_CloudConfiguration",
    en: "Configuration profiles",
    de: "Konfigurationsprofile",
  },
  { key: "Shared_CommandCenter_Nav_Events", en: "Events", de: "Veranstaltungen" },
  { key: "Shared_CommandCenter_Nav_HostingFinance", en: "Hosting finance", de: "Hosting-Finanzen" },
  { key: "Shared_CommandCenter_Nav_ModPacks", en: "Mod packs", de: "Mod-Pakete" },
  { key: "Shared_CommandCenter_Nav_Terrains", en: "Terrains", de: "Karten" },
  { key: "Shared_CommandCenter_Nav_FieldReports", en: "Field reports", de: "Feldberichte" },
  { key: "Shared_CommandCenter_Nav_Gallery", en: "Gallery", de: "Galerie" },
  {
    key: "Shared_CommandCenter_Nav_PublicImages",
    en: "Public images",
    de: "Öffentliche Bilder",
    h1Exception:
      "The h1 is the dynamic image count title; the kicker carries the label (D-AUTO-5).",
  },
  { key: "Shared_CommandCenter_Nav_Members", en: "Members", de: "Mitglieder" },
  { key: "Shared_CommandCenter_Nav_Roles", en: "Roles", de: "Rollen" },
  { key: "Shared_CommandCenter_Nav_Claims", en: "Claims", de: "Claims" },
  {
    key: "Shared_CommandCenter_Nav_ContentAndDocuments",
    en: "Content and documents",
    de: "Inhalte und Dokumente",
  },
  {
    key: "Shared_CommandCenter_Nav_VereinMembershipRequests",
    en: "Membership requests",
    de: "Mitgliedsanträge",
  },
  {
    key: "Shared_CommandCenter_Nav_VereinDocuments",
    en: "Verein documents",
    de: "Vereinsdokumente",
  },
  { key: "Shared_CommandCenter_Nav_VereinKassenbuch", en: "Kassenbuch", de: "Kassenbuch" },
  {
    key: "Shared_CommandCenter_Nav_VereinSettings",
    en: "Contribution settings",
    de: "Beitragseinstellungen",
    h1Exception: "The page h1 keeps the long form, Verein contribution settings.",
  },
  {
    key: "Shared_CommandCenter_Nav_Settings",
    en: "Operational settings",
    de: "Betriebseinstellungen",
  },
  { key: "Shared_CommandCenter_Group_CommandCenter", en: "Command Center", de: "Command Center" },
  { key: "Shared_CommandCenter_Group_Operations", en: "Operations", de: "Operationen" },
  { key: "Shared_CommandCenter_Group_Finance", en: "Finance", de: "Finanzen" },
  { key: "Shared_CommandCenter_Group_Assets", en: "Assets", de: "Material" },
  { key: "Shared_CommandCenter_Group_Community", en: "Community", de: "Community" },
  { key: "Shared_CommandCenter_Group_Governance", en: "Governance", de: "Governance" },
  { key: "Shared_CommunityNav_Home", en: "Home", de: "Startseite" },
  { key: "Shared_CommunityNav_Operations", en: "Operations", de: "Operationen" },
  { key: "Shared_CommunityNav_Unit", en: "Unit", de: "Einheit" },
  { key: "Shared_CommunityNav_Verein", en: "Verein", de: "Verein" },
  { key: "Shared_CommunityNav_Wiki", en: "Wiki", de: "Wiki" },
  { key: "Shared_CommunityNav_FieldReports", en: "Field reports", de: "Feldberichte" },
  { key: "Shared_CommunityNav_Join", en: "Join", de: "Beitreten" },
  { key: "Shared_CommunityNav_Login", en: "Sign in", de: "Anmelden" },
  { key: "Shared_CommunityNav_CommandCenter", en: "Command Center", de: "Command Center" },
];
