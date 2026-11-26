import { useEffect, useRef, type ChangeEvent } from "react";
import { ActionButton, type ActionControlVariant } from "@/components/ActionControl";

/** Props of {@link FileChooserButton}. */
export interface FileChooserButtonProps {
  /** Visible text of the button. */
  children: string;
  /** Accessible name of the underlying file input, shown by assistive technology and used to find it in tests. */
  inputLabel: string;
  /** Lets the user pick several files at once. */
  multiple?: boolean;
  /** Restricts the browser file chooser, for example to image types. Validation by the caller stays required. */
  accept?: string | undefined;
  /** Lets the user pick a whole folder; the files then carry their `webkitRelativePath`. */
  directory?: boolean;
  /** Visual variant of the button. Defaults to `ghost`. */
  variant?: ActionControlVariant;
  /** Makes the button inert. */
  disabled?: boolean;
  /**
   * Called with the files the user chose. Choosing nothing never calls it.
   *
   * @param files The chosen files in the order the browser reports them.
   */
  onFiles(files: File[]): void;
}

/**
 * A button that opens the browser file chooser.
 *
 * The native file input is the one control the browser requires for a chooser; it stays hidden and is opened by a
 * shared {@link ActionButton}, so the visible control is the same keyboard-reachable button as everywhere else. The
 * input is cleared after every choice, so choosing the same file twice still reports it.
 */
export function FileChooserButton({
  children,
  inputLabel,
  multiple = false,
  accept,
  directory = false,
  variant = "ghost",
  disabled = false,
  onFiles,
}: FileChooserButtonProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    // React has no typed prop for `webkitdirectory`, so the attribute is set on the element itself.
    if (directory) inputRef.current?.setAttribute("webkitdirectory", "");
    else inputRef.current?.removeAttribute("webkitdirectory");
  }, [directory]);

  function handleChange(event: ChangeEvent<HTMLInputElement>): void {
    const files = Array.from(event.target.files ?? []);
    event.target.value = "";
    if (files.length > 0) onFiles(files);
  }

  return (
    <>
      <ActionButton variant={variant} disabled={disabled} onClick={() => inputRef.current?.click()}>
        {children}
      </ActionButton>
      <input
        ref={inputRef}
        hidden
        type="file"
        accept={accept}
        multiple={multiple || directory}
        aria-label={inputLabel}
        onChange={handleChange}
      />
    </>
  );
}
