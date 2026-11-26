import { Document20Regular } from "@fluentui/react-icons";
import type { DragEvent, DragEventHandler, ReactNode, Ref } from "react";
import "@/components/server/files.css";

/** Props of {@link FileUploadDropzone}. */
export interface FileUploadDropzoneProps {
  /** The bold title line, for example "Drop files here". */
  title: string;
  /** The muted line under the title that points to the choose controls. */
  body: string;
  /** The control that chooses files; it renders as the first of the two controls. */
  chooseFiles: ReactNode;
  /** The control that chooses a whole folder; it renders beside the first control. */
  chooseFolder: ReactNode;
  /**
   * Called when files or folders are dropped on the zone. The zone has already prevented the browser default, so
   * the handler only has to read the dropped items.
   *
   * @param event The drop event of the zone.
   */
  onDrop: DragEventHandler<HTMLDivElement>;
  /** Makes the zone ignore drops, for example while the queue uploads. */
  disabled?: boolean | undefined;
  /** Receives the zone element, so a dialog can move focus to its first control after it opens. */
  ref?: Ref<HTMLDivElement> | undefined;
}

/**
 * The drop zone of the upload dialog.
 *
 * A dashed control border with a document icon, a title and a muted line, and the two choose controls side by side
 * under them (stacked at 520px and below). The zone accepts dropped files and folders through `onDrop`; it
 * decides nothing about the files, so the queueing, preflight and collision approval stay with the panel. The
 * zone carries no gold: the dashed border uses the control border token.
 *
 * @param props The texts, the choose controls and the drop handler.
 * @returns The drop zone element.
 */
export function FileUploadDropzone({
  title,
  body,
  chooseFiles,
  chooseFolder,
  onDrop,
  disabled = false,
  ref,
}: FileUploadDropzoneProps) {
  function handleDragOver(event: DragEvent<HTMLDivElement>): void {
    // Always prevent the default so a drop never navigates the page to the dropped file; a disabled zone shows that
    // it takes nothing.
    event.preventDefault();
    event.dataTransfer.dropEffect = disabled ? "none" : "copy";
  }

  function handleDrop(event: DragEvent<HTMLDivElement>): void {
    event.preventDefault();
    if (disabled) return;
    onDrop(event);
  }

  return (
    <div
      ref={ref}
      className="file-dropzone"
      data-disabled={disabled ? "true" : undefined}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
    >
      <Document20Regular aria-hidden="true" />
      <strong className="file-dropzone__title">{title}</strong>
      <span className="file-dropzone__body">{body}</span>
      <div className="file-dropzone__choices">
        {chooseFiles}
        {chooseFolder}
      </div>
    </div>
  );
}
