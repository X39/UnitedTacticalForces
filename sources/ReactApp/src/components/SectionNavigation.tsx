import { Link, type To } from "react-router";
import "@/components/server/navigation.css";

/**
 * One link of a {@link SectionNavigation}.
 *
 * @typeParam TId The string union of section identifiers.
 */
export interface SectionNavigationItem<TId extends string = string> {
  /** Stable identifier compared with the `currentId` of the navigation. */
  id: TId;
  /** Visible link label. */
  label: string;
  /** Router location the link navigates to. */
  to: To;
}

/**
 * Props of {@link SectionNavigation}.
 *
 * @typeParam TId The string union of section identifiers.
 */
export interface SectionNavigationProps<TId extends string = string> {
  /** Accessible name of the navigation landmark. */
  label: string;
  /** Links in display order. */
  items: readonly SectionNavigationItem<TId>[];
  /** Identifier of the current section; only its link carries `aria-current="page"`. */
  currentId: TId;
}

/**
 * A shared navigation landmark that moves between the sections of one page through ordinary router links.
 *
 * The component is a `nav` holding a list of links, not a tab widget: there is no `role="tab"`, no button and no
 * icon. The current section is announced with `aria-current="page"`. Every section has a real link target, so a
 * section can be opened in a new tab and shared as a URL.
 *
 * @typeParam TId The string union of section identifiers.
 */
export function SectionNavigation<TId extends string = string>({
  label,
  items,
  currentId,
}: SectionNavigationProps<TId>) {
  return (
    <nav aria-label={label} className="section-navigation">
      <ul className="section-navigation__list">
        {items.map((item) => (
          <li key={item.id} className="section-navigation__item">
            <Link
              to={item.to}
              className="section-navigation__link"
              {...(item.id === currentId ? { "aria-current": "page" as const } : {})}
            >
              {item.label}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
