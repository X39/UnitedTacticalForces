import "@/components/action-control.css";
import { Info16Regular } from "@fluentui/react-icons";
import {
  useId,
  type ComponentPropsWithoutRef,
  type ComponentPropsWithRef,
  type ReactNode,
} from "react";
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
  ComponentPropsWithRef<"button">,
  "aria-label" | "children" | "className"
> &
  ActionControlVisualProps &
  ActionControlContentProps & {
    /**
     * Visible reason the action is unavailable. When set, the button keeps focus, exposes `aria-disabled`
     * and links this content as its description instead of using the native `disabled` attribute.
     */
    unavailableReason?: ReactNode | undefined;
  };

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

function hasReason(reason: ReactNode): boolean {
  return reason !== null && reason !== undefined && reason !== false && reason !== "";
}

/**
 * A button with the shared action styling.
 *
 * When `unavailableReason` is set the button stays focusable and uses `aria-disabled` instead of `disabled`,
 * a visible reason is linked through `aria-describedby`, and activation neither runs `onClick` nor submits an
 * enclosing form.
 */
export function ActionButton({
  variant = "default",
  size = "default",
  fullWidth = false,
  iconOnly = false,
  type = "button",
  className,
  unavailableReason,
  ...buttonProps
}: ActionButtonProps) {
  const reasonId = useId();
  const classNames = actionControlClassName({ variant, size, fullWidth, iconOnly, className });
  if (!hasReason(unavailableReason)) {
    return <button {...buttonProps} type={type} className={classNames} />;
  }
  const { "aria-describedby": describedBy, ...rest } = buttonProps;
  return (
    <span
      className={
        fullWidth ? "action-with-reason action-with-reason--full-width" : "action-with-reason"
      }
    >
      <button
        {...rest}
        type={type}
        className={classNames}
        aria-disabled="true"
        aria-describedby={describedBy ? `${describedBy} ${reasonId}` : reasonId}
        onClick={(event) => event.preventDefault()}
      />
      <span id={reasonId} className="action-reason">
        <Info16Regular aria-hidden="true" />
        {unavailableReason}
      </span>
    </span>
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
