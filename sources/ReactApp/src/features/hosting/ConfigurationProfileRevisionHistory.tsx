import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import { formatDate } from "@/features/hosting/model";
import type {
  ConfigurationDefinition,
  ConfigurationProfileDetails,
  ConfigurationProfileRevision,
} from "@/features/hosting/types";

const changeKeys = {
  Added: "React_ConfigurationProfiles_ChangeAdded",
  Changed: "React_ConfigurationProfiles_ChangeChanged",
  Removed: "React_ConfigurationProfiles_ChangeRemoved",
} as const;

export function ConfigurationProfileRevisionHistory({
  profile,
  definitions,
}: {
  profile: ConfigurationProfileDetails;
  definitions: ConfigurationDefinition[];
}) {
  const { t } = useTranslation();
  const definitionByKey = new Map(definitions.map((definition) => [definition.key, definition]));

  return (
    <section className="next-hosting-panel next-hosting-profile-history">
      <header className="next-hosting-section-heading">
        <div>
          <span className="kicker">{t("Hosting_ConfigurationProfiles_AuditTrail")}</span>
          <h2>{t("Hosting_ConfigurationProfiles_RevisionHistory")}</h2>
          <p>{t("React_ConfigurationProfiles_RevisionHistoryDescription")}</p>
        </div>
        <p>
          {formatResource(
            t("Hosting_ConfigurationProfiles_RevisionCount"),
            profile.revisions.length,
          )}
        </p>
      </header>
      <ol className="next-hosting-revision-timeline">
        {profile.revisions
          .toSorted((left, right) => right.revisionNumber - left.revisionNumber)
          .map((revision) => (
            <RevisionEntry
              key={revision.primaryKey}
              revision={revision}
              latest={revision.revisionNumber === profile.profile.latestRevisionNumber}
              definitions={definitions}
              definitionByKey={definitionByKey}
            />
          ))}
      </ol>
    </section>
  );
}

function RevisionEntry({
  revision,
  latest,
  definitions,
  definitionByKey,
}: {
  revision: ConfigurationProfileRevision;
  latest: boolean;
  definitions: ConfigurationDefinition[];
  definitionByKey: Map<string, ConfigurationDefinition>;
}) {
  const { t } = useTranslation();
  const changes = revision.changes ?? [];
  const groupedChanges = Object.entries(
    changes.reduce<Record<string, typeof changes>>((result, change) => {
      const definition = definitionByKey.get(change.key);
      const group = definition
        ? `${definition.targetPath} · ${definition.group}`
        : t("Hosting_ConfigurationProfiles_General");
      (result[group] ??= []).push(change);
      return result;
    }, {}),
  );
  const configuredSensitive = new Set(
    revision.sensitiveConfiguredKeys ?? revision.redactedKeys ?? [],
  );

  return (
    <li>
      <span className="next-hosting-revision-marker" aria-hidden="true" />
      <article>
        <header>
          <div>
            <span className="kicker">
              {revision.revisionNumber === 1
                ? t("React_ConfigurationProfiles_Baseline")
                : `${t("Hosting_ConfigurationProfiles_Revision")} ${revision.revisionNumber}`}
            </span>
            <h3>
              {t("Hosting_ConfigurationProfiles_Revision")} {revision.revisionNumber}
              {latest && <small>{t("React_ConfigurationProfiles_CurrentRevision")}</small>}
            </h3>
          </div>
          <strong>
            {formatResource(t("React_ConfigurationProfiles_RevisionChangeCount"), changes.length)}
          </strong>
        </header>
        <p className="next-hosting-revision-byline">
          {formatResource(
            t("React_ConfigurationProfiles_ChangedBy"),
            revision.createdByNickname || t("React_ConfigurationProfiles_UnknownAuthor"),
            formatDate(revision.timeStampCreated),
          )}
        </p>
        {groupedChanges.length === 0 ? (
          <p className="next-hosting-muted">{t("React_ConfigurationProfiles_NoRevisionChanges")}</p>
        ) : (
          <div className="next-hosting-revision-change-groups">
            {groupedChanges.map(([group, changes]) => (
              <section key={group}>
                <h4>{group}</h4>
                <dl>
                  {changes.map((change) => {
                    const definition = definitionByKey.get(change.key);
                    const value = revision.values[change.key];
                    const sensitive = Boolean(definition?.sensitive);
                    return (
                      <div key={change.key}>
                        <dt>
                          <span>{definition?.displayName ?? change.key}</span>
                          <small data-kind={change.kind.toLowerCase()}>
                            {t(changeKeys[change.kind])}
                          </small>
                        </dt>
                        <dd>
                          {change.kind === "Removed"
                            ? t("Hosting_ConfigurationProfiles_NotSet")
                            : sensitive
                              ? configuredSensitive.has(change.key)
                                ? t("React_ConfigurationProfiles_Configured")
                                : t("Hosting_ConfigurationProfiles_NotSet")
                              : formatValue(value)}
                        </dd>
                      </div>
                    );
                  })}
                </dl>
              </section>
            ))}
          </div>
        )}
        <details className="next-hosting-revision-snapshot">
          <summary>{t("React_ConfigurationProfiles_FullSnapshot")}</summary>
          <dl className="next-hosting-facts next-hosting-facts--compact">
            {definitions
              .filter(
                (definition) =>
                  definition.key in revision.values || configuredSensitive.has(definition.key),
              )
              .map((definition) => (
                <div key={definition.key}>
                  <dt>{definition.displayName}</dt>
                  <dd>
                    {definition.sensitive
                      ? t("React_ConfigurationProfiles_Configured")
                      : formatValue(revision.values[definition.key])}
                  </dd>
                </div>
              ))}
          </dl>
        </details>
      </article>
    </li>
  );
}

function formatValue(value: unknown): string {
  if (Array.isArray(value)) return value.join(", ");
  if (value === null || value === undefined || value === "") return "—";
  return String(value);
}
