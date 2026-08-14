import { forwardRef, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { formatResource } from "@/app/i18n";
import { StatusBadge } from "@/components/primitives";
import { StructuredContent, type GdprContent } from "@/pages/GdprPage";

type PolicyCard = GdprContent["openingCards"][number];
type CardSection = GdprContent["visibility"];

function TemplateField({
  label,
  context,
  value,
  multiline = false,
  onChange,
}: {
  label: string;
  context: string;
  value: string;
  multiline?: boolean;
  onChange(value: string): void;
}) {
  return (
    <label className={`gdpr-editor-field${multiline ? " gdpr-editor-field--wide" : ""}`}>
      <span>{label}</span>
      {multiline ? (
        <textarea
          rows={4}
          value={value}
          aria-label={`${context}: ${label}`}
          onChange={(event) => onChange(event.target.value)}
        />
      ) : (
        <input
          value={value}
          aria-label={`${context}: ${label}`}
          onChange={(event) => onChange(event.target.value)}
        />
      )}
    </label>
  );
}

function SectionHeader({
  kicker,
  title,
  description,
}: {
  kicker: string;
  title: string;
  description: string;
}) {
  return (
    <header className="gdpr-editor-section__header">
      <div>
        <span className="kicker">{kicker}</span>
        <h3>{title}</h3>
        <p>{description}</p>
      </div>
    </header>
  );
}

function RepeaterItem({
  index,
  label,
  children,
}: {
  index: number;
  label: string;
  children: ReactNode;
}) {
  return (
    <article className="gdpr-editor-item">
      <header>
        <span>{String(index + 1).padStart(2, "0")}</span>
        <h4>{label}</h4>
      </header>
      <div className="gdpr-editor-grid">{children}</div>
    </article>
  );
}

function PolicyCardsEditor({
  id,
  kicker,
  title,
  description,
  cards,
  onChange,
}: {
  id: string;
  kicker: string;
  title: string;
  description: string;
  cards: PolicyCard[];
  onChange(index: number, card: PolicyCard): void;
}) {
  const { t } = useTranslation();
  return (
    <section id={id} className="command-panel gdpr-editor-section">
      <SectionHeader kicker={kicker} title={title} description={description} />
      <div className="gdpr-editor-repeater">
        {cards.map((card, index) => {
          const context = formatResource(t("Pages_Command_Content_Gdpr_CardNumber"), index + 1);
          return (
            <RepeaterItem key={index} index={index} label={context}>
              <TemplateField
                label={t("Pages_Command_Content_Gdpr_Kicker")}
                context={context}
                value={card.kicker}
                onChange={(value) => onChange(index, { ...card, kicker: value })}
              />
              <TemplateField
                label={t("Pages_Command_Content_Gdpr_Title")}
                context={context}
                value={card.title}
                onChange={(value) => onChange(index, { ...card, title: value })}
              />
              {card.paragraphs.map((paragraph, paragraphIndex) => (
                <TemplateField
                  key={paragraphIndex}
                  multiline
                  label={formatResource(
                    t("Pages_Command_Content_Gdpr_ParagraphNumber"),
                    paragraphIndex + 1,
                  )}
                  context={context}
                  value={paragraph}
                  onChange={(value) => {
                    const paragraphs = [...card.paragraphs];
                    paragraphs[paragraphIndex] = value;
                    onChange(index, { ...card, paragraphs });
                  }}
                />
              ))}
            </RepeaterItem>
          );
        })}
      </div>
    </section>
  );
}

function CardSectionEditor({
  id,
  kicker,
  title,
  description,
  section,
  onChange,
}: {
  id: string;
  kicker: string;
  title: string;
  description: string;
  section: CardSection;
  onChange(section: CardSection): void;
}) {
  const { t } = useTranslation();
  return (
    <section id={id} className="command-panel gdpr-editor-section">
      <SectionHeader kicker={kicker} title={title} description={description} />
      <div className="gdpr-editor-grid gdpr-editor-section__fields">
        <TemplateField
          label={t("Pages_Command_Content_Gdpr_Kicker")}
          context={title}
          value={section.kicker}
          onChange={(value) => onChange({ ...section, kicker: value })}
        />
        <TemplateField
          label={t("Pages_Command_Content_Gdpr_Title")}
          context={title}
          value={section.title}
          onChange={(value) => onChange({ ...section, title: value })}
        />
      </div>
      <div className="gdpr-editor-repeater">
        {section.cards.map((card, index) => {
          const context = formatResource(t("Pages_Command_Content_Gdpr_CardNumber"), index + 1);
          return (
            <RepeaterItem key={index} index={index} label={context}>
              <TemplateField
                label={t("Pages_Command_Content_Gdpr_Title")}
                context={`${title} ${context}`}
                value={card.title}
                onChange={(value) => {
                  const cards = [...section.cards];
                  cards[index] = { ...card, title: value };
                  onChange({ ...section, cards });
                }}
              />
              <TemplateField
                multiline
                label={t("Pages_Command_Content_Gdpr_Body")}
                context={`${title} ${context}`}
                value={card.body}
                onChange={(value) => {
                  const cards = [...section.cards];
                  cards[index] = { ...card, body: value };
                  onChange({ ...section, cards });
                }}
              />
            </RepeaterItem>
          );
        })}
      </div>
    </section>
  );
}

export const GdprContentEditor = forwardRef<
  HTMLDivElement,
  {
    content: GdprContent;
    revisionLabel: string;
    invalid: boolean;
    onChange(content: GdprContent): void;
  }
>(function GdprContentEditor({ content, revisionLabel, invalid, onChange }, ref) {
  const { t } = useTranslation();
  const [showPreview, setShowPreview] = useState(false);
  const update = (mutator: (next: GdprContent) => void) => {
    const next = structuredClone(content);
    mutator(next);
    onChange(next);
  };
  const navItems = [
    ["gdpr-template-intro", t("Pages_Command_Content_Gdpr_IntroSection")],
    ["gdpr-template-opening", t("Pages_Command_Content_Gdpr_OpeningCards")],
    ["gdpr-template-lifecycle", t("Pages_Command_Content_Gdpr_LifecycleSection")],
    ["gdpr-template-data-map", t("Pages_Command_Content_Gdpr_DataMapSection")],
    ["gdpr-template-visibility", t("Pages_Command_Content_Gdpr_VisibilitySection")],
    ["gdpr-template-partners", t("Pages_Command_Content_Gdpr_PartnersSection")],
    ["gdpr-template-policy", t("Pages_Command_Content_Gdpr_PolicyCardsSection")],
  ] as const;

  return (
    <div
      ref={ref}
      id="public-page-structured-editor"
      className={`gdpr-editor${invalid ? " gdpr-editor--invalid" : ""}`}
      aria-invalid={invalid}
      tabIndex={-1}
    >
      <nav className="gdpr-editor-nav" aria-label={t("Pages_Command_Content_Gdpr_SectionNavAria")}>
        <span className="kicker">{t("Pages_Command_Content_Gdpr_SectionNavKicker")}</span>
        {navItems.map(([id, label]) => (
          <a key={id} href={`#${id}`}>
            {label}
          </a>
        ))}
      </nav>

      <section className="command-panel gdpr-editor-preview">
        <header className="gdpr-editor-preview__header">
          <div>
            <span className="kicker">{t("Pages_Command_Content_Gdpr_TemplatePreviewKicker")}</span>
            <h2>{t("Pages_Command_Content_Gdpr_TemplatePreviewTitle")}</h2>
            <p>{t("Pages_Command_Content_Gdpr_TemplatePreviewDescription")}</p>
          </div>
          <div>
            <StatusBadge>{revisionLabel}</StatusBadge>
            <button
              className="button button--ghost"
              type="button"
              aria-expanded={showPreview}
              onClick={() => setShowPreview((current) => !current)}
            >
              {t(
                showPreview
                  ? "Pages_Command_Content_Gdpr_TemplatePreviewHide"
                  : "Pages_Command_Content_Gdpr_TemplatePreviewShow",
              )}
            </button>
          </div>
        </header>
        {showPreview && (
          <div className="gdpr-editor-preview__content">
            <StructuredContent content={content} />
          </div>
        )}
      </section>

      <div className="gdpr-editor-form">
        <section id="gdpr-template-intro" className="command-panel gdpr-editor-section">
          <SectionHeader
            kicker={t("Pages_Command_Content_Gdpr_TemplateEditorKicker")}
            title={t("Pages_Command_Content_Gdpr_IntroSection")}
            description={t("Pages_Command_Content_Gdpr_IntroSectionDescription")}
          />
          <div className="gdpr-editor-grid gdpr-editor-section__fields">
            <TemplateField
              label={t("Pages_Command_Content_Gdpr_Kicker")}
              context={t("Pages_Command_Content_Gdpr_IntroSection")}
              value={content.intro.kicker}
              onChange={(value) => update((next) => (next.intro.kicker = value))}
            />
            <TemplateField
              label={t("Pages_Command_Content_Gdpr_Title")}
              context={t("Pages_Command_Content_Gdpr_IntroSection")}
              value={content.intro.title}
              onChange={(value) => update((next) => (next.intro.title = value))}
            />
            <TemplateField
              multiline
              label={t("Pages_Command_Content_Gdpr_Description")}
              context={t("Pages_Command_Content_Gdpr_IntroSection")}
              value={content.intro.description}
              onChange={(value) => update((next) => (next.intro.description = value))}
            />
            <TemplateField
              label={t("Pages_Command_Content_Gdpr_LastReviewedLabel")}
              context={t("Pages_Command_Content_Gdpr_IntroSection")}
              value={content.intro.lastReviewedLabel}
              onChange={(value) => update((next) => (next.intro.lastReviewedLabel = value))}
            />
            <TemplateField
              label={t("Pages_Command_Content_Gdpr_LastReviewedDate")}
              context={t("Pages_Command_Content_Gdpr_IntroSection")}
              value={content.intro.lastReviewedDate}
              onChange={(value) => update((next) => (next.intro.lastReviewedDate = value))}
            />
            <TemplateField
              multiline
              label={t("Pages_Command_Content_Gdpr_ProcessNote")}
              context={t("Pages_Command_Content_Gdpr_IntroSection")}
              value={content.intro.processNote}
              onChange={(value) => update((next) => (next.intro.processNote = value))}
            />
          </div>
        </section>

        <PolicyCardsEditor
          id="gdpr-template-opening"
          kicker={t("Pages_Command_Content_Gdpr_OpeningCardsKicker")}
          title={t("Pages_Command_Content_Gdpr_OpeningCards")}
          description={t("Pages_Command_Content_Gdpr_OpeningCardsDescription")}
          cards={content.openingCards}
          onChange={(index, card) => update((next) => (next.openingCards[index] = card))}
        />

        <section id="gdpr-template-lifecycle" className="command-panel gdpr-editor-section">
          <SectionHeader
            kicker={t("Pages_Command_Content_Gdpr_LifecycleKicker")}
            title={t("Pages_Command_Content_Gdpr_LifecycleSection")}
            description={t("Pages_Command_Content_Gdpr_LifecycleDescription")}
          />
          <div className="gdpr-editor-grid gdpr-editor-section__fields">
            <TemplateField
              label={t("Pages_Command_Content_Gdpr_Kicker")}
              context={t("Pages_Command_Content_Gdpr_LifecycleSection")}
              value={content.lifecycle.kicker}
              onChange={(value) => update((next) => (next.lifecycle.kicker = value))}
            />
            <TemplateField
              label={t("Pages_Command_Content_Gdpr_Title")}
              context={t("Pages_Command_Content_Gdpr_LifecycleSection")}
              value={content.lifecycle.title}
              onChange={(value) => update((next) => (next.lifecycle.title = value))}
            />
          </div>
          <div className="gdpr-editor-repeater">
            {content.lifecycle.steps.map((step, index) => {
              const context = formatResource(t("Pages_Command_Content_Gdpr_StepNumber"), index + 1);
              return (
                <RepeaterItem key={index} index={index} label={context}>
                  <TemplateField
                    label={t("Pages_Command_Content_Gdpr_Title")}
                    context={context}
                    value={step.title}
                    onChange={(value) =>
                      update((next) => {
                        const nextStep = next.lifecycle.steps[index];
                        if (nextStep) nextStep.title = value;
                      })
                    }
                  />
                  {step.paragraphs.map((paragraph, paragraphIndex) => (
                    <TemplateField
                      key={paragraphIndex}
                      multiline
                      label={formatResource(
                        t("Pages_Command_Content_Gdpr_ParagraphNumber"),
                        paragraphIndex + 1,
                      )}
                      context={context}
                      value={paragraph}
                      onChange={(value) =>
                        update((next) => {
                          const nextStep = next.lifecycle.steps[index];
                          if (nextStep) nextStep.paragraphs[paragraphIndex] = value;
                        })
                      }
                    />
                  ))}
                </RepeaterItem>
              );
            })}
          </div>
        </section>

        <section id="gdpr-template-data-map" className="command-panel gdpr-editor-section">
          <SectionHeader
            kicker={t("Pages_Command_Content_Gdpr_DataMapKicker")}
            title={t("Pages_Command_Content_Gdpr_DataMapSection")}
            description={t("Pages_Command_Content_Gdpr_DataMapDescription")}
          />
          <div className="gdpr-editor-grid gdpr-editor-section__fields">
            <TemplateField
              label={t("Pages_Command_Content_Gdpr_Kicker")}
              context={t("Pages_Command_Content_Gdpr_DataMapSection")}
              value={content.dataMap.kicker}
              onChange={(value) => update((next) => (next.dataMap.kicker = value))}
            />
            <TemplateField
              label={t("Pages_Command_Content_Gdpr_Title")}
              context={t("Pages_Command_Content_Gdpr_DataMapSection")}
              value={content.dataMap.title}
              onChange={(value) => update((next) => (next.dataMap.title = value))}
            />
          </div>
          <div className="gdpr-editor-repeater">
            {content.dataMap.cards.map((card, index) => {
              const context = formatResource(t("Pages_Command_Content_Gdpr_CardNumber"), index + 1);
              const updateCard = (changes: Partial<typeof card>) =>
                update((next) => {
                  next.dataMap.cards[index] = { ...card, ...changes };
                });
              return (
                <RepeaterItem key={index} index={index} label={context}>
                  <TemplateField
                    label={t("Pages_Command_Content_Gdpr_Title")}
                    context={context}
                    value={card.title}
                    onChange={(value) => updateCard({ title: value })}
                  />
                  <TemplateField
                    label={t("Pages_Command_Content_Gdpr_DataLabel")}
                    context={context}
                    value={card.dataLabel}
                    onChange={(value) => updateCard({ dataLabel: value })}
                  />
                  <TemplateField
                    multiline
                    label={t("Pages_Command_Content_Gdpr_Data")}
                    context={context}
                    value={card.data}
                    onChange={(value) => updateCard({ data: value })}
                  />
                  <TemplateField
                    label={t("Pages_Command_Content_Gdpr_PurposeLabel")}
                    context={context}
                    value={card.purposeLabel}
                    onChange={(value) => updateCard({ purposeLabel: value })}
                  />
                  <TemplateField
                    multiline
                    label={t("Pages_Command_Content_Gdpr_Purpose")}
                    context={context}
                    value={card.purpose}
                    onChange={(value) => updateCard({ purpose: value })}
                  />
                  <TemplateField
                    label={t("Pages_Command_Content_Gdpr_WhereLabel")}
                    context={context}
                    value={card.whereLabel}
                    onChange={(value) => updateCard({ whereLabel: value })}
                  />
                  <TemplateField
                    multiline
                    label={t("Pages_Command_Content_Gdpr_Where")}
                    context={context}
                    value={card.where}
                    onChange={(value) => updateCard({ where: value })}
                  />
                </RepeaterItem>
              );
            })}
          </div>
        </section>

        <CardSectionEditor
          id="gdpr-template-visibility"
          kicker={t("Pages_Command_Content_Gdpr_VisibilityKicker")}
          title={t("Pages_Command_Content_Gdpr_VisibilitySection")}
          description={t("Pages_Command_Content_Gdpr_VisibilityDescription")}
          section={content.visibility}
          onChange={(section) => update((next) => (next.visibility = section))}
        />
        <CardSectionEditor
          id="gdpr-template-partners"
          kicker={t("Pages_Command_Content_Gdpr_PartnersKicker")}
          title={t("Pages_Command_Content_Gdpr_PartnersSection")}
          description={t("Pages_Command_Content_Gdpr_PartnersDescription")}
          section={content.partners}
          onChange={(section) => update((next) => (next.partners = section))}
        />
        <PolicyCardsEditor
          id="gdpr-template-policy"
          kicker={t("Pages_Command_Content_Gdpr_PolicyKicker")}
          title={t("Pages_Command_Content_Gdpr_PolicyCardsSection")}
          description={t("Pages_Command_Content_Gdpr_PolicyCardsDescription")}
          cards={content.policyCards}
          onChange={(index, card) => update((next) => (next.policyCards[index] = card))}
        />
      </div>
    </div>
  );
});
