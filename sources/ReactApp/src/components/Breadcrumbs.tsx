import { ChevronRight16Regular } from "@fluentui/react-icons";
import { Link, type To } from "react-router";
import "@/components/server/navigation.css";

/** One entry of a {@link Breadcrumbs} trail. */
export interface BreadcrumbItem {
  /** Visible text. It is rendered as a React text child and wraps instead of being truncated. */
  label: string;
  /** Router location of the entry. Ignored for the last entry, which is the current page. */
  to?: To;
  /**
   * Marks the label as user authored content, such as a server name. It is isolated in a `bdi` element so it keeps
   * its own text direction and is not treated as interface text, which a German page must not show in English.
   */
  userContent?: boolean;
}

/** Props of {@link Breadcrumbs}. */
export interface BreadcrumbsProps {
  /** Accessible name of the navigation landmark. */
  label: string;
  /** Trail entries from the outermost ancestor to the current page. */
  items: readonly BreadcrumbItem[];
}

/**
 * A shared breadcrumb trail rendered as a navigation landmark holding an ordered list.
 *
 * Every entry except the last is a plain link when it has a target. The last entry is the current page: plain text
 * carrying `aria-current="page"`. Decorative chevrons sit between entries only; they are hidden from assistive
 * technology and are not part of any entry's text.
 */
export function Breadcrumbs({ label, items }: BreadcrumbsProps) {
  const lastIndex = items.length - 1;
  const text = (item: BreadcrumbItem) => (item.userContent ? <bdi>{item.label}</bdi> : item.label);
  return (
    <nav aria-label={label} className="breadcrumbs">
      <ol className="breadcrumbs__list">
        {items.map((item, index) => (
          <li key={index} className="breadcrumbs__item">
            {index === lastIndex ? (
              <span className="breadcrumbs__current" aria-current="page">
                {text(item)}
              </span>
            ) : item.to !== undefined ? (
              <Link to={item.to} className="breadcrumbs__link">
                {text(item)}
              </Link>
            ) : (
              <span className="breadcrumbs__current">{text(item)}</span>
            )}
            {index < lastIndex && (
              <ChevronRight16Regular className="breadcrumbs__separator" aria-hidden="true" />
            )}
          </li>
        ))}
      </ol>
    </nav>
  );
}
