import { useQuery } from "@tanstack/react-query";
import { useMemo, type CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { z } from "zod";
import { resolveImageUrl } from "@/api/client";
import { publicPageService, queryKeys } from "@/api/services";
import { Markdown, PageHeader, StatePanel } from "@/components/primitives";
import { useSiteImage } from "@/app/siteImages";

const text = z.string().catch("");
const policyCardSchema = z.object({
  kicker: text,
  title: text,
  paragraphs: z.array(text).catch([]),
});
const simpleCardSchema = z.object({ title: text, body: text });
const cardSectionSchema = z.object({
  kicker: text,
  title: text,
  cards: z.array(simpleCardSchema).catch([]),
});
const gdprContentSchema = z.object({
  intro: z.object({
    kicker: text,
    title: text,
    description: text,
    lastReviewedLabel: text,
    lastReviewedDate: text,
    processNote: text,
  }),
  openingCards: z.array(policyCardSchema).catch([]),
  lifecycle: z.object({
    kicker: text,
    title: text,
    steps: z.array(z.object({ title: text, paragraphs: z.array(text).catch([]) })).catch([]),
  }),
  dataMap: z.object({
    kicker: text,
    title: text,
    cards: z
      .array(
        z.object({
          title: text,
          dataLabel: text,
          data: text,
          purposeLabel: text,
          purpose: text,
          whereLabel: text,
          where: text,
        }),
      )
      .catch([]),
  }),
  visibility: cardSectionSchema,
  partners: cardSectionSchema,
  policyCards: z.array(policyCardSchema).catch([]),
});

export type GdprContent = z.infer<typeof gdprContentSchema>;
type PolicyCard = z.infer<typeof policyCardSchema>;
type CardSection = z.infer<typeof cardSectionSchema>;

export function parseStructuredContent(value: string | undefined): GdprContent | undefined {
  if (!value) return undefined;
  try {
    const parsed = gdprContentSchema.safeParse(JSON.parse(value) as unknown);
    return parsed.success ? parsed.data : undefined;
  } catch {
    return undefined;
  }
}

function PolicyCards({ cards }: { cards: PolicyCard[] }) {
  if (!cards.length) return null;
  return (
    <section className="rights-grid">
      {cards.map((card, index) => (
        <article key={`${card.title}-${index}`}>
          {card.kicker && <span className="kicker">{card.kicker}</span>}
          <h2>{card.title}</h2>
          {card.paragraphs.filter(Boolean).map((paragraph, paragraphIndex) => (
            <Markdown key={paragraphIndex}>{paragraph}</Markdown>
          ))}
        </article>
      ))}
    </section>
  );
}

function CardSectionView({ section }: { section: CardSection }) {
  if (!section.cards.length) return null;
  return (
    <section className="legal-band">
      <PageHeader kicker={section.kicker} title={section.title} />
      <div className="visibility-grid">
        {section.cards.map((card, index) => (
          <article key={`${card.title}-${index}`}>
            <h2>{card.title}</h2>
            <Markdown>{card.body}</Markdown>
          </article>
        ))}
      </div>
    </section>
  );
}

export function StructuredContent({ content }: { content: GdprContent }) {
  return (
    <>
      <section className="legal-intro">
        <PageHeader
          kicker={content.intro.kicker}
          title={content.intro.title}
          description={content.intro.description}
        />
        <div className="legal-note">
          <strong>{content.intro.lastReviewedLabel}</strong>
          <span>{content.intro.lastReviewedDate}</span>
          <Markdown>{content.intro.processNote}</Markdown>
        </div>
      </section>
      <PolicyCards cards={content.openingCards} />
      {content.lifecycle.steps.length > 0 && (
        <section>
          <PageHeader kicker={content.lifecycle.kicker} title={content.lifecycle.title} />
          <ol className="lifecycle">
            {content.lifecycle.steps.map((step, index) => (
              <li key={`${step.title}-${index}`}>
                <span>{String(index + 1).padStart(2, "0")}</span>
                <div>
                  <h2>{step.title}</h2>
                  {step.paragraphs.filter(Boolean).map((paragraph, paragraphIndex) => (
                    <Markdown key={paragraphIndex}>{paragraph}</Markdown>
                  ))}
                </div>
              </li>
            ))}
          </ol>
        </section>
      )}
      {content.dataMap.cards.length > 0 && (
        <section>
          <PageHeader kicker={content.dataMap.kicker} title={content.dataMap.title} />
          <div className="data-map">
            {content.dataMap.cards.map((card, index) => (
              <article key={`${card.title}-${index}`}>
                <h2>{card.title}</h2>
                <dl>
                  <dt>{card.dataLabel}</dt>
                  <dd>
                    <Markdown>{card.data}</Markdown>
                  </dd>
                  <dt>{card.purposeLabel}</dt>
                  <dd>
                    <Markdown>{card.purpose}</Markdown>
                  </dd>
                  <dt>{card.whereLabel}</dt>
                  <dd>
                    <Markdown>{card.where}</Markdown>
                  </dd>
                </dl>
              </article>
            ))}
          </div>
        </section>
      )}
      <CardSectionView section={content.visibility} />
      <CardSectionView section={content.partners} />
      <PolicyCards cards={content.policyCards} />
    </>
  );
}

export function Component() {
  const { t } = useTranslation();
  const fallbackHeroImage = useSiteImage(
    "gdpr-hero",
    "/media/generated/mission-overwatch-banner.jpg",
  );
  const query = useQuery({
    queryKey: queryKeys.publicPage("gdpr"),
    queryFn: ({ signal }) => publicPageService.published("gdpr", signal),
    retry: false,
  });
  const revision = query.data?.revision;
  const content = useMemo(
    () => parseStructuredContent(revision?.contentJson),
    [revision?.contentJson],
  );
  const heroImage = resolveImageUrl(revision?.heroImagePath) ?? fallbackHeroImage;

  return (
    <div className="gdpr-page page-wrap">
      <section
        className="gdpr-hero"
        style={{ "--hero-image": `url("${heroImage.replaceAll('"', "%22")}")` } as CSSProperties}
      >
        <div>
          <span className="kicker">
            {revision?.heroKicker || t("Pages_Public_GDPR_Hero_Kicker")}
          </span>
          <h1>{revision?.heroTitle || t("Pages_Public_GDPR_Hero_Title")}</h1>
          <p>{revision?.heroDescription || t("Pages_Public_GDPR_Hero_Description")}</p>
          <a
            className="button button--glass"
            href="https://github.com/X39/UnitedTacticalForces"
            rel="noreferrer"
          >
            {t("Pages_Public_GDPR_Hero_SourceCode")}
          </a>
        </div>
      </section>

      {query.isLoading && <StatePanel kind="loading">{t("React_Gdpr_Loading")}</StatePanel>}
      {query.isError && (
        <StatePanel kind="error" title={t("React_Gdpr_ErrorTitle")}>
          {t("React_Gdpr_ErrorBody")}
        </StatePanel>
      )}
      {!query.isLoading && !query.isError && !revision && (
        <StatePanel kind="empty" title={t("React_Gdpr_UnavailableTitle")}>
          {t("React_Gdpr_UnavailableBody")}
        </StatePanel>
      )}
      {revision &&
        (content ? (
          <StructuredContent content={content} />
        ) : (
          <Markdown>{revision.markdown}</Markdown>
        ))}

      {revision && (
        <section className="legal-note">
          <span className="kicker">{t("Pages_Public_GDPR_Reference_Kicker")}</span>
          <h2>{t("Pages_Public_GDPR_Reference_Title")}</h2>
          <p>{t("Pages_Public_GDPR_Reference_Body")}</p>
          <div className="header-actions">
            <a
              className="button button--ghost"
              href="https://commission.europa.eu/law/law-topic/data-protection_en"
              rel="noreferrer"
            >
              {t("Pages_Public_GDPR_Reference_EuropeanCommission")}
            </a>
            <a
              className="button button--ghost"
              href="https://www.edpb.europa.eu/sme-data-protection-guide/respect-individuals-rights_en"
              rel="noreferrer"
            >
              {t("Pages_Public_GDPR_Reference_EDPB")}
            </a>
          </div>
        </section>
      )}
      <section className="cookie-notice">
        <h2>{t("Pages_Public_GDPR_FunctionalCookies_Title")}</h2>
        <p>{t("Pages_Public_GDPR_FunctionalCookies_Body")}</p>
        <code>utf_ui_variant</code>
      </section>
      <Link className="button button--ghost" to="/">
        {t("Pages_Public_GDPR_BackToUtf")}
      </Link>
    </div>
  );
}
