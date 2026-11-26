import type { DragEventHandler, ReactNode } from "react";
import "@/components/server/files.css";

/** Props of {@link FileBrowser}. */
export interface FileBrowserProps {
  /** Accessible name of the toolbar, which groups the path, the actions, the search and the refresh control. */
  toolbarLabel: string;
  /** The path navigation shown at the start of the first toolbar row. */
  breadcrumb: ReactNode;
  /** The action controls at the end of the first toolbar row, for example upload and create folder; omitted when none. */
  actions?: ReactNode;
  /** The icon-only refresh control, shown at the end of the search row. */
  refresh: ReactNode;
  /** The search field, shown at full width in the second toolbar row. */
  search: ReactNode;
  /** The content of the folder tree pane. */
  treePane: ReactNode;
  /** Whether the tree pane is open; the pane is hidden behind a toggle at 800px and below. */
  treeOpen: boolean;
  /** The list pane content: the folder heading, the polite announcer and then the table or a state. */
  listPane: ReactNode;
  /** The footer content: the item count and the note that file changes take effect immediately. */
  footer: ReactNode;
  /** Whether a drag is over the list pane, which draws the dashed drop outline. */
  dragActive: boolean;
  /** Called when a drag enters the list pane. */
  onDragEnter?: DragEventHandler<HTMLDivElement> | undefined;
  /** Called while a drag is over the list pane. */
  onDragOver?: DragEventHandler<HTMLDivElement> | undefined;
  /** Called when a drag leaves the list pane. */
  onDragLeave?: DragEventHandler<HTMLDivElement> | undefined;
  /** Called when something is dropped on the list pane. */
  onDrop?: DragEventHandler<HTMLDivElement> | undefined;
}

/**
 * The file browser card of the Files panel.
 *
 * One bordered, raised surface holds the toolbar, the explorer (the folder tree pane and the list pane) and the
 * footer, so the create and upload controls belong to the list they act on. The toolbar is a named toolbar bar of
 * two rows: the path with the actions, then the search with the refresh control. It is a plain Tab sequence. The
 * component draws layout only: every label, control and state arrives through the slots, and the file data logic
 * stays with the panel that owns it.
 * The list pane is a top-aligned column, so the table or an empty state always sits directly under its heading.
 * The `next-hosting-*` hook classes stay on the DOM for the existing tests and audits.
 *
 * @param props The slots and drag handlers of the card.
 * @returns The card element.
 */
export function FileBrowser({
  toolbarLabel,
  breadcrumb,
  actions,
  refresh,
  search,
  treePane,
  treeOpen,
  listPane,
  footer,
  dragActive,
  onDragEnter,
  onDragOver,
  onDragLeave,
  onDrop,
}: FileBrowserProps) {
  return (
    <div className="file-browser">
      <div
        className="file-browser__toolbar next-hosting-file-toolbar"
        role="toolbar"
        aria-label={toolbarLabel}
      >
        <div className="file-browser__toolbar-row">
          {breadcrumb}
          {actions ? <div className="file-browser__actions">{actions}</div> : null}
        </div>
        <div className="file-browser__toolbar-row file-browser__toolbar-row--search">
          {search}
          {refresh}
        </div>
      </div>
      <div
        className={`file-browser__explorer next-hosting-explorer${treeOpen ? " file-browser__explorer--tree-open next-hosting-explorer--tree-open" : ""}`}
      >
        {treePane}
        <div
          className={`file-browser__list next-hosting-file-content${dragActive ? " file-browser__list--drag next-hosting-file-content--drag" : ""}`}
          onDragEnter={onDragEnter}
          onDragOver={onDragOver}
          onDragLeave={onDragLeave}
          onDrop={onDrop}
        >
          {listPane}
        </div>
      </div>
      <footer className="file-browser__footer next-hosting-file-footer" aria-live="polite">
        {footer}
      </footer>
    </div>
  );
}
