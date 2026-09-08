import type { FormEventHandler, ReactNode } from "react";
import { ActionButton } from "@/components/ActionControl";

export interface CommandListToolbarProps {
  ariaLabel: string;
  children?: ReactNode;
  className?: string;
  onSearchChange(value: string): void;
  onSubmit: FormEventHandler<HTMLFormElement>;
  searchLabel: string;
  searchPlaceholder?: string;
  searchValue: string;
  submitDisabled?: boolean;
  submitLabel: string;
}

export function CommandListToolbar({
  ariaLabel,
  children,
  className,
  onSearchChange,
  onSubmit,
  searchLabel,
  searchPlaceholder = searchLabel,
  searchValue,
  submitDisabled = false,
  submitLabel,
}: CommandListToolbarProps) {
  return (
    <form
      className={["toolbar", "command-list-toolbar", className].filter(Boolean).join(" ")}
      aria-label={ariaLabel}
      onSubmit={onSubmit}
    >
      <label className="search-field">
        <span className="sr-only">{searchLabel}</span>
        <input
          value={searchValue}
          placeholder={searchPlaceholder}
          onChange={(event) => onSearchChange(event.target.value)}
        />
      </label>
      <ActionButton variant="ghost" type="submit" disabled={submitDisabled}>
        {submitLabel}
      </ActionButton>
      {children}
    </form>
  );
}
