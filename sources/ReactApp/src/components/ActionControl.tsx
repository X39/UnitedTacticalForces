import type { ComponentPropsWithoutRef, ReactNode } from "react";
import { Link, type LinkProps } from "react-router";

export type ActionControlVariant = "default" | "gold" | "primary" | "ghost" | "glass" | "danger";

export type ActionControlSize = "default" | "compact";

interface ActionControlVisualProps {
  variant?: ActionControlVariant;
  size?: ActionControlSize;
  fullWidth?: boolean;
  className?: string;
}

type ActionControlContentProps =
  | {
      iconOnly: true;
      "aria-label": string;
      children: ReactNode;
    }
  | {
      iconOnly?: false;
      "aria-label"?: string;
      children: ReactNode;
    };

export type ActionButtonProps = Omit<
  ComponentPropsWithoutRef<"button">,
  "aria-label" | "children" | "className"
> &
  ActionControlVisualProps &
  ActionControlContentProps;

export type ActionLinkProps = Omit<LinkProps, "aria-label" | "children" | "className"> &
  ActionControlVisualProps &
  ActionControlContentProps;

export type ActionAnchorProps = Omit<
  ComponentPropsWithoutRef<"a">,
  "aria-label" | "children" | "className" | "href"
> &
  ActionControlVisualProps &
  ActionControlContentProps & {
    href: string;
  };

const variantClassNames: Record<ActionControlVariant, string | undefined> = {
  default: undefined,
  gold: "button--gold",
  primary: "button--primary",
  ghost: "button--ghost",
  glass: "button--glass",
  danger: "button--danger",
};

function actionControlClassName({
  variant,
  size,
  fullWidth,
  iconOnly,
  className,
}: {
  variant: ActionControlVariant;
  size: ActionControlSize;
  fullWidth: boolean;
  iconOnly: boolean;
  className: string | undefined;
}): string {
  return [
    "button",
    variantClassNames[variant ?? "default"],
    size === "compact" ? "button--small" : undefined,
    fullWidth ? "button--full-width" : undefined,
    iconOnly ? "button--icon-only" : undefined,
    className,
  ]
    .filter(Boolean)
    .join(" ");
}

export function ActionButton({
  variant = "default",
  size = "default",
  fullWidth = false,
  iconOnly = false,
  type = "button",
  className,
  ...buttonProps
}: ActionButtonProps) {
  return (
    <button
      {...buttonProps}
      type={type}
      className={actionControlClassName({ variant, size, fullWidth, iconOnly, className })}
    />
  );
}

export function ActionLink({
  variant = "default",
  size = "default",
  fullWidth = false,
  iconOnly = false,
  className,
  ...linkProps
}: ActionLinkProps) {
  return (
    <Link
      {...linkProps}
      className={actionControlClassName({ variant, size, fullWidth, iconOnly, className })}
    />
  );
}

export function ActionAnchor({
  variant = "default",
  size = "default",
  fullWidth = false,
  iconOnly = false,
  className,
  ...anchorProps
}: ActionAnchorProps) {
  return (
    <a
      {...anchorProps}
      className={actionControlClassName({ variant, size, fullWidth, iconOnly, className })}
    />
  );
}
