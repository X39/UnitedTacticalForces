import { Children, isValidElement, useId, type ReactNode } from "react";
import "@/components/server/fleet-card.css";

/** Props of {@link FleetSection}. */
export interface FleetSectionProps {
  /** The visible section heading, already localized and including any count. */
  title: string;
  /** Text shown instead of the list when the section has no cards; nothing is shown when omitted. */
  emptyText?: string | undefined;
  /** The cards of the section; each child becomes one list item. */
  children?: ReactNode;
}

/**
 * Groups fleet cards under a section heading.
 *
 * The section is labelled by its `h2`. Each child is wrapped in a list item of a grid list that keeps its list
 * semantics through `role="list"`. Without children the section shows the empty text instead of an empty list.
 *
 * @param props The heading, the optional empty text and the cards.
 * @returns The section element.
 */
export function FleetSection({ title, emptyText, children }: FleetSectionProps) {
  const titleId = useId();
  const items = Children.toArray(children);
  return (
    <section className="fleet-section" aria-labelledby={titleId}>
      <h2 id={titleId} className="fleet-section__title">
        {title}
      </h2>
      {items.length > 0 ? (
        <ul className="fleet-grid" role="list">
          {items.map((child, index) => (
            <li key={isValidElement(child) && child.key !== null ? child.key : index}>{child}</li>
          ))}
        </ul>
      ) : (
        emptyText !== undefined && <p className="fleet-section__empty">{emptyText}</p>
      )}
    </section>
  );
}
