import type { CSSProperties, ReactNode } from "react";

interface PublicHeroBaseProps {
  kicker?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  className?: string;
  innerClassName?: string;
  copyClassName?: string;
  descriptionClassName?: string;
  actionsClassName?: string;
  style?: CSSProperties;
  compact?: boolean;
  singleColumn?: boolean;
  ariaLabel?: string;
}

interface PublicHeroWithAsideProps {
  aside: ReactNode;
  asideClassName?: string;
  asideLabel: string;
}

interface PublicHeroWithoutAsideProps {
  aside?: never;
  asideClassName?: never;
  asideLabel?: never;
}

export type PublicHeroProps = PublicHeroBaseProps &
  (PublicHeroWithAsideProps | PublicHeroWithoutAsideProps);

function joinClassNames(...classNames: (string | false | undefined)[]): string {
  return classNames.filter(Boolean).join(" ");
}

/**
 * The image hero of the public community pages and the only header kind with a marketing title scale.
 *
 * The title keeps its scale as the single documented type exception (E-01). The copy column renders its slots in
 * one fixed order: kicker, the `h1` title, the lede, then the actions. A page renders exactly one hero, so the
 * hero title is the page's only `h1`; a state view or section below it starts at `h2`.
 *
 * @param props.kicker Optional short label above the title.
 * @param props.title The page title, rendered as the `h1`.
 * @param props.description Optional lede below the title.
 * @param props.actions Optional action controls below the lede.
 * @param props.aside Optional side panel; requires `asideLabel`.
 */
export function PublicHero({
  kicker,
  title,
  description,
  actions,
  aside,
  asideClassName,
  asideLabel,
  className,
  innerClassName,
  copyClassName,
  descriptionClassName,
  actionsClassName,
  style,
  compact = false,
  singleColumn = false,
  ariaLabel,
}: PublicHeroProps) {
  return (
    <section
      className={joinClassNames("public-hero", compact && "public-hero--compact", className)}
      style={style}
      aria-label={ariaLabel}
    >
      <div
        className={joinClassNames(
          "public-hero__inner",
          singleColumn && "public-hero__inner--single",
          innerClassName,
        )}
      >
        <div className={joinClassNames("public-hero__copy", copyClassName)}>
          {kicker !== undefined && kicker !== null && <span className="kicker">{kicker}</span>}
          <h1>{title}</h1>
          {description !== undefined && description !== null && (
            <div className={joinClassNames("public-hero__description", descriptionClassName)}>
              {description}
            </div>
          )}
          {actions !== undefined && actions !== null && (
            <div className={joinClassNames("public-hero__actions", actionsClassName)}>
              {actions}
            </div>
          )}
        </div>
        {aside !== undefined && aside !== null && (
          <aside
            className={joinClassNames("public-hero__panel", asideClassName)}
            aria-label={asideLabel}
          >
            {aside}
          </aside>
        )}
      </div>
    </section>
  );
}
