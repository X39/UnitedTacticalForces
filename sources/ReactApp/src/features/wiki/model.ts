import type {
  FullUser,
  WikiPageHeader,
  WikiPageRevision,
  WikiPageRevisionHeader,
} from "@/api/types";
import { hasBaseClaim, hasClaim } from "@/auth/claims";

export interface WikiAccess {
  canCreate: boolean;
  canModify: boolean;
  canRename: boolean;
  canDelete: boolean;
  canRestore: boolean;
  canViewHistory: boolean;
}

export interface WikiTreeNode {
  name: string;
  path: string;
  children: WikiTreeNode[];
  pages: WikiPageHeader[];
}

export interface WikiEditorState {
  mode: "create" | "edit";
  pageId?: string;
  originalTitle: string;
  originalMarkdown: string;
  title: string;
  markdown: string;
  comment: string;
  preview: boolean;
}

export type WikiEditorError =
  | "title-required"
  | "markdown-required"
  | "comment-required"
  | "create-forbidden"
  | "no-changes"
  | "rename-forbidden"
  | "modify-forbidden";

function hasWikiAdmin(user: FullUser | undefined): boolean {
  return hasClaim(user, "adm", "adm:wiki");
}

export function getWikiAccess(user: FullUser | undefined): WikiAccess {
  const wikiAdmin = hasWikiAdmin(user);
  const canModify = wikiAdmin || hasBaseClaim(user, "wiki:modify");
  const canRename = wikiAdmin || hasBaseClaim(user, "wiki:rename");
  const canDelete = wikiAdmin || hasBaseClaim(user, "wiki:delete");
  return {
    canCreate: wikiAdmin || hasBaseClaim(user, "crt:wiki"),
    canModify,
    canRename,
    canDelete,
    canRestore: wikiAdmin,
    canViewHistory:
      wikiAdmin || hasBaseClaim(user, "wiki", "wiki:modify", "wiki:rename", "wiki:delete"),
  };
}

export function filterWikiHeaders(headers: WikiPageHeader[], search: string): WikiPageHeader[] {
  const query = search.trim().toLocaleLowerCase();
  return headers
    .filter((header) => !query || header.name.toLocaleLowerCase().includes(query))
    .toSorted((left, right) => left.name.localeCompare(right.name));
}

export function wikiLeafName(title: string | null | undefined, untitled: string): string {
  const parts = (title ?? "")
    .split("/")
    .map((part) => part.trim())
    .filter(Boolean);
  return parts.at(-1) ?? untitled;
}

export function wikiCategoryPath(title: string | null | undefined): string {
  const parts = (title ?? "")
    .split("/")
    .map((part) => part.trim())
    .filter(Boolean);
  return parts.length > 1 ? parts.slice(0, -1).join(" / ") : "";
}

export function buildWikiTree(headers: WikiPageHeader[]): WikiTreeNode {
  const root: WikiTreeNode = { name: "", path: "", children: [], pages: [] };
  for (const header of headers) {
    const parts = header.name
      .split("/")
      .map((part) => part.trim())
      .filter(Boolean);
    if (parts.length < 2) {
      root.pages.push(header);
      continue;
    }

    let current = root;
    for (const part of parts.slice(0, -1)) {
      const path = current.path ? `${current.path}/${part}` : part;
      let child = current.children.find((candidate) => candidate.name === part);
      if (!child) {
        child = { name: part, path, children: [], pages: [] };
        current.children.push(child);
      }
      current = child;
    }
    current.pages.push(header);
  }
  sortTree(root);
  return root;
}

function sortTree(node: WikiTreeNode): void {
  node.children.sort((left, right) => left.name.localeCompare(right.name));
  node.pages.sort((left, right) => left.name.localeCompare(right.name));
  node.children.forEach(sortTree);
}

export function latestWikiRevision(
  revisions: WikiPageRevision[] | null | undefined,
): WikiPageRevision | undefined {
  return [...(revisions ?? [])].sort((left, right) =>
    (right.timeStampCreated ?? "").localeCompare(left.timeStampCreated ?? ""),
  )[0];
}

export function wikiArticleSummary(markdown: string | null | undefined, fallback: string): string {
  if (!markdown?.trim()) return fallback;
  for (const rawLine of markdown.split("\n")) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) continue;
    return line.replace(/^[-*>\s]+/, "").trim() || fallback;
  }
  return fallback;
}

export function validateWikiEditor(
  editor: WikiEditorState,
  access: WikiAccess,
): WikiEditorError | undefined {
  if (!editor.title.trim()) return "title-required";
  if (!editor.markdown.trim()) return "markdown-required";
  if (!editor.comment.trim()) return "comment-required";
  if (editor.mode === "create") return access.canCreate ? undefined : "create-forbidden";

  const titleChanged = editor.originalTitle !== editor.title.trim();
  const markdownChanged = editor.originalMarkdown !== editor.markdown;
  if (!titleChanged && !markdownChanged) return "no-changes";
  if (titleChanged && !access.canRename) return "rename-forbidden";
  if (markdownChanged && !access.canModify) return "modify-forbidden";
  return undefined;
}

export function isWikiEditorDirty(editor: WikiEditorState | undefined): boolean {
  if (!editor) return false;
  if (editor.mode === "create")
    return Boolean(editor.title.trim() || editor.comment.trim() || editor.markdown.trim());
  return (
    editor.title !== editor.originalTitle ||
    editor.markdown !== editor.originalMarkdown ||
    Boolean(editor.comment.trim())
  );
}

export function revisionCountLabel(
  headers: WikiPageRevisionHeader[] | undefined,
  noRevisions: string,
  version: (count: number) => string,
): string {
  if (!headers?.length) return noRevisions;
  return version(headers.length);
}
