import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import { formatDate } from "@/features/hosting/model";
import type {
  ConfigurationDefinition,
  ConfigurationProfileDetails,
} from "@/features/hosting/types";

export function ConfigurationProfileRevisionHistory({
  profile,
  definitions,
}: {
  profile: ConfigurationProfileDetails;
  definitions: ConfigurationDefinition[];
}) {
  const { t } = useTranslation();

  return (
    <section className="next-hosting-panel next-hosting-profile-history">
      <header className="next-hosting-section-heading">
        <div>
          <span className="kicker">{t("Hosting_ConfigurationProfiles_AuditTrail")}</span>
          <h2>{t("Hosting_ConfigurationProfiles_RevisionHistory")}</h2>
        </div>
        <p>
          {formatResource(
            t("Hosting_ConfigurationProfiles_RevisionCount"),
            profile.revisions.length,
          )}
        </p>
      </header>
      {profile.revisions
        .toSorted((left, right) => right.revisionNumber - left.revisionNumber)
        .map((revision) => (
          <details
            key={revision.primaryKey}
            open={revision.revisionNumber === profile.profile.latestRevisionNumber}
          >
            <summary>
              <span>
                <strong>
                  {t("Hosting_ConfigurationProfiles_Revision")} {revision.revisionNumber}
                </strong>
                <small>{formatDate(revision.timeStampCreated)}</small>
              </span>
              <span>
                {formatResource(
                  t("Hosting_ConfigurationProfiles_ValueCount"),
                  Object.keys(revision.values).length,
                )}
              </span>
            </summary>
            <dl className="next-hosting-facts next-hosting-facts--compact">
              {definitions
                .filter((definition) => definition.key in revision.values)
                .map((definition) => {
                  const value = revision.values[definition.key];
                  const redacted = revision.redactedKeys.includes(definition.key);
                  return (
                    <div key={definition.key}>
                      <dt>{definition.displayName}</dt>
                      <dd>
                        {redacted
                          ? t("Hosting_ConfigurationProfiles_StoredSecurely")
                          : definition.sensitive
                            ? "••••••••"
                            : Array.isArray(value)
                              ? value.join(", ")
                              : value === null || value === undefined
                                ? t("Hosting_ConfigurationProfiles_NotSet")
                                : String(value)}
                      </dd>
                    </div>
                  );
                })}
            </dl>
            {revision.redactedKeys.length > 0 && (
              <p className="next-hosting-muted">
                {t("Hosting_ConfigurationProfiles_RedactedNotice")}
              </p>
            )}
          </details>
        ))}
    </section>
  );
}
