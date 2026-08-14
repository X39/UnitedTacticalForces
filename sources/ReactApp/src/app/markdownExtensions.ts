import { mdast2hastGridTablesHandler } from "@adobe/mdast-util-gridtables";
import type { Nodes, Root, RootContent, Text } from "mdast";
import type { Options as RemarkRehypeOptions } from "remark-rehype";
import { visit } from "unist-util-visit";

type MarkdownNode = RootContent & {
  data?: {
    hName?: string;
    hProperties?: Record<string, unknown>;
  };
};

const protectedParentTypes = new Set(["code", "inlineCode", "link", "linkReference"]);

function textNode(value: string): Text {
  return { type: "text", value };
}

function elementNode(
  tagName: string,
  value: string,
  properties?: Record<string, unknown>,
): MarkdownNode {
  return {
    type: "emphasis",
    children: [textNode(value)],
    data: { hName: tagName, hProperties: properties },
  } as MarkdownNode;
}

function replaceInlineText(
  tree: Root,
  transform: (value: string) => MarkdownNode[] | undefined,
): void {
  visit(tree, "text", (node: Text, index, parent) => {
    if (index === undefined || !parent || protectedParentTypes.has(parent.type)) return;
    const replacement = transform(node.value);
    if (!replacement) return;
    parent.children.splice(index, 1, ...replacement);
    return index + replacement.length;
  });
}

function splitByPattern(
  value: string,
  pattern: RegExp,
  createNode: (match: RegExpExecArray) => MarkdownNode,
): MarkdownNode[] | undefined {
  const nodes: MarkdownNode[] = [];
  let cursor = 0;
  let matched = false;
  pattern.lastIndex = 0;
  for (let match = pattern.exec(value); match; match = pattern.exec(value)) {
    matched = true;
    if (match.index > cursor)
      nodes.push(textNode(value.slice(cursor, match.index)) as MarkdownNode);
    nodes.push(createNode(match));
    cursor = match.index + match[0].length;
  }
  if (!matched) return undefined;
  if (cursor < value.length) nodes.push(textNode(value.slice(cursor)) as MarkdownNode);
  return nodes;
}

export function remarkMarkdigInlineExtensions() {
  return (tree: Root) => {
    replaceInlineText(tree, (value) =>
      splitByPattern(value, /(?<!\\)""([^"\n]+)""/g, (match) =>
        elementNode("cite", match[1] ?? ""),
      ),
    );
    replaceInlineText(tree, (value) =>
      splitByPattern(value, /(?<!\+)\+\+([^+\n]+)\+\+(?!\+)/g, (match) =>
        elementNode("ins", match[1] ?? ""),
      ),
    );
    replaceInlineText(tree, (value) =>
      splitByPattern(value, /(?<![~\\])~([^~\s][^~\n]*?)~(?!~)/g, (match) =>
        elementNode("sub", match[1] ?? ""),
      ),
    );
    replaceInlineText(tree, (value) =>
      splitByPattern(value, /(?<!\\)\^([^\s^][^\n^]*?)\^/g, (match) =>
        elementNode("sup", match[1] ?? ""),
      ),
    );
  };
}

export function remarkMarkdigAbbreviations() {
  return (tree: Root) => {
    const abbreviations = new Map<string, string>();
    for (let index = tree.children.length - 1; index >= 0; index -= 1) {
      const node = tree.children[index];
      if (node?.type !== "paragraph" || node.children.length !== 1) continue;
      const child = node.children[0];
      if (child?.type !== "text") continue;
      const match = /^\*\[([^\]]+)\]:\s*(.+)$/.exec(child.value);
      if (!match?.[1] || !match[2]) continue;
      abbreviations.set(match[1], match[2]);
      tree.children.splice(index, 1);
    }
    const labels = [...abbreviations.keys()].sort((left, right) => right.length - left.length);
    if (!labels.length) return;
    const escaped = labels.map((label) => label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"));
    const pattern = new RegExp(
      `(?<![\\p{L}\\p{N}])(${escaped.join("|")})(?=$|[^\\p{L}\\p{N}])`,
      "gu",
    );
    replaceInlineText(tree, (value) =>
      splitByPattern(value, pattern, (match) => {
        const label = match[1] ?? "";
        return elementNode("abbr", label, { title: abbreviations.get(label) });
      }),
    );
  };
}

export function preprocessMarkdigBlocks(markdown: string): string {
  const lines = markdown.replace(/\r\n?/g, "\n").split("\n");
  const output: string[] = [];
  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index] ?? "";
    if (/^\s*\^\^\^\s*$/.test(line)) {
      const body: string[] = [];
      let caption = "";
      index += 1;
      while (index < lines.length && !/^\s*\^\^\^/.test(lines[index] ?? "")) {
        body.push(lines[index] ?? "");
        index += 1;
      }
      caption = (lines[index] ?? "").replace(/^\s*\^\^\^\s*/, "");
      output.push("::::figure", ...body);
      if (caption) output.push(":::figcaption", caption, ":::");
      output.push("::::");
      continue;
    }
    if (/^\s*\^\^\s+/.test(line)) {
      const footer: string[] = [];
      while (index < lines.length && /^\s*\^\^\s?/.test(lines[index] ?? "")) {
        footer.push((lines[index] ?? "").replace(/^\s*\^\^\s?/, ""));
        index += 1;
      }
      output.push(":::footer", ...footer, ":::");
      index -= 1;
      continue;
    }
    output.push(line);
  }
  return output.join("\n");
}

export function remarkMarkdigContainers() {
  return (tree: Root) => {
    visit(tree, (node: Nodes) => {
      if (
        node.type === "containerDirective" &&
        ["figure", "figcaption", "footer"].includes(node.name)
      ) {
        const directive = node as MarkdownNode & { name: string };
        directive.data = { ...(directive.data ?? {}), hName: directive.name };
      }
    });
  };
}

export const markdownRehypeOptions = {
  handlers: { gridTable: mdast2hastGridTablesHandler() },
} as unknown as RemarkRehypeOptions;
