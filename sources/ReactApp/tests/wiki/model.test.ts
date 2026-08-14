import type { FullUser, WikiPageHeader } from "@/api/types";
import {
  buildWikiTree,
  filterWikiHeaders,
  getWikiAccess,
  latestWikiRevision,
  validateWikiEditor,
  wikiArticleSummary,
  wikiCategoryPath,
  wikiLeafName,
  type WikiEditorState,
} from "@/features/wiki/model";

function user(...claims: [identifier: string, value?: string | null][]): FullUser {
  return {
    primaryKey: "00000000-0000-0000-0000-000000000001",
    nickname: "Editor",
    isBanned: false,
    isDeleted: false,
    isVerified: true,
    isVereinMember: false,
    claims: claims.map(([identifier, value = null]) => ({ identifier, value })),
    roles: [],
  } as FullUser;
}

const headers: WikiPageHeader[] = [
  {
    primaryKey: "00000000-0000-0000-0000-000000000002",
    name: "Training/Radio/AN-PRC 152",
    lastModified: "2026-08-10T12:00:00Z",
  },
  {
    primaryKey: "00000000-0000-0000-0000-000000000003",
    name: "Training/Medical",
    lastModified: "2026-08-11T12:00:00Z",
  },
  {
    primaryKey: "00000000-0000-0000-0000-000000000004",
    name: "Welcome",
    lastModified: "2026-08-12T12:00:00Z",
  },
];

const editor: WikiEditorState = {
  mode: "edit",
  pageId: "00000000-0000-0000-0000-000000000002",
  originalTitle: "Training/Radio",
  originalMarkdown: "Old",
  title: "Training/Radio",
  markdown: "New",
  comment: "Updated procedure",
  preview: false,
};

describe("wiki authorization and model", () => {
  it("keeps public reading separate from privileged history and mutations", () => {
    expect(getWikiAccess(undefined)).toEqual({
      canCreate: false,
      canModify: false,
      canRename: false,
      canDelete: false,
      canRestore: false,
      canViewHistory: false,
    });
    expect(getWikiAccess(user(["wiki:modify", "page-id"]))).toEqual(getWikiAccess(undefined));
    expect(getWikiAccess(user(["wiki:modify"]))).toMatchObject({
      canModify: true,
      canViewHistory: true,
      canCreate: false,
      canRestore: false,
    });
    expect(getWikiAccess(user(["adm:wiki"]))).toEqual({
      canCreate: true,
      canModify: true,
      canRename: true,
      canDelete: true,
      canRestore: true,
      canViewHistory: true,
    });
    expect(getWikiAccess(user(["adm:wiki", "legacy-value"]))).toEqual(
      getWikiAccess(user(["adm:wiki"])),
    );
    expect(getWikiAccess(user(["adm", "legacy-value"]))).toEqual(getWikiAccess(user(["adm:wiki"])));
  });

  it("builds deterministic category navigation and searches full titles", () => {
    const tree = buildWikiTree(headers);
    expect(tree.pages.map((page) => page.name)).toEqual(["Welcome"]);
    expect(tree.children[0]?.name).toBe("Training");
    expect(tree.children[0]?.pages.map((page) => page.name)).toEqual(["Training/Medical"]);
    expect(tree.children[0]?.children[0]?.pages[0]?.name).toBe("Training/Radio/AN-PRC 152");
    expect(filterWikiHeaders(headers, "radio").map((page) => page.name)).toEqual([
      "Training/Radio/AN-PRC 152",
    ]);
    expect(wikiLeafName(" Training / Radio ", "Untitled")).toBe("Radio");
    expect(wikiCategoryPath(" Training / Radio ")).toBe("Training");
  });

  it("validates rename and content permissions independently", () => {
    expect(validateWikiEditor(editor, getWikiAccess(user(["wiki:rename"])))).toBe(
      "modify-forbidden",
    );
    expect(validateWikiEditor(editor, getWikiAccess(user(["wiki:modify"])))).toBeUndefined();
    expect(
      validateWikiEditor(
        { ...editor, markdown: editor.originalMarkdown, title: "Training/Comms" },
        getWikiAccess(user(["wiki:modify"])),
      ),
    ).toBe("rename-forbidden");
    expect(
      validateWikiEditor(
        { ...editor, markdown: editor.originalMarkdown, comment: "" },
        getWikiAccess(user(["wiki:modify"])),
      ),
    ).toBe("comment-required");
  });

  it("selects the latest revision and derives a plain summary", () => {
    const latest = latestWikiRevision([
      { timeStampCreated: "2026-01-01T00:00:00Z", markdown: "Old" },
      { timeStampCreated: "2026-02-01T00:00:00Z", markdown: "New" },
    ]);
    expect(latest?.markdown).toBe("New");
    expect(wikiArticleSummary("# Heading\n\n> Operational note", "Fallback")).toBe(
      "Operational note",
    );
  });
});
