import type { FormEventHandler, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ActionButton } from "@/components/ActionControl";
import { SearchField } from "@/components/SearchField";

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

/**
 * The list-page toolbar with a submitted search: the shared {@link SearchField}, a submit button and the
 * page-specific controls passed as children.
 *
 * Pressing Enter in the field or the submit button reports the form submit. The field keeps its clear control, which
 * reports an empty value without submitting.
 */
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
  const { t } = useTranslation();
  return (
    <form
      className={["toolbar", "command-list-toolbar", className].filter(Boolean).join(" ")}
      aria-label={ariaLabel}
      onSubmit={onSubmit}
    >
      <SearchField
        label={searchLabel}
        placeholder={searchPlaceholder}
        clearLabel={t("Common_SearchClear")}
        value={searchValue}
        onChange={onSearchChange}
      />
      <ActionButton variant="ghost" type="submit" disabled={submitDisabled}>
        {submitLabel}
      </ActionButton>
      {children}
    </form>
  );
}
