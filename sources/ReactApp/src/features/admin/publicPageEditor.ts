import { z } from "zod";
import {
  createDefaultGdprContent,
  gdprContentToMarkdown,
  serializeGdprContent,
} from "@/features/admin/gdprContent";
import type {
  PublicPage,
  PublicPageRevision,
  PublicPageRevisionPayload,
} from "@/features/admin/publicPages";
import { parseStructuredContent, type GdprContent } from "@/pages/GdprPage";

const emptyGuid = "00000000-0000-0000-0000-000000000000";
export const maximumStructuredContentLength = 120_000;

export interface PublicPageEditorValues {
  title: string;
  summary: string;
  heroKicker: string;
  heroTitle: string;
  heroDescription: string;
  heroImageAssetFk: string;
  heroImagePath: string;
  markdown: string;
  comment: string;
  publish: boolean;
}

export interface PublicPageEditorDefaults {
  title: string;
  summary: string;
  heroKicker: string;
  heroTitle: string;
  heroDescription: string;
  markdown: string;
}

export interface PublicPageEditorValidationMessages {
  title: string;
  summary: string;
  heroKicker: string;
  heroTitle: string;
  heroDescription: string;
  heroImage: string;
  markdown: string;
  comment: string;
}

export function activePublicPageRevision(
  page: PublicPage | undefined,
): PublicPageRevision | undefined {
  return (
    page?.revision ??
    [...(page?.revisions ?? [])].sort((left, right) => right.version - left.version)[0]
  );
}

export function createPublicPageEditorValues(
  page: PublicPage | undefined,
  defaults: PublicPageEditorDefaults,
): PublicPageEditorValues {
  const revision = activePublicPageRevision(page);
  return {
    title: revision?.title || defaults.title,
    summary: revision?.summary ?? defaults.summary,
    heroKicker: revision?.heroKicker ?? defaults.heroKicker,
    heroTitle: revision?.heroTitle ?? defaults.heroTitle,
    heroDescription: revision?.heroDescription ?? defaults.heroDescription,
    heroImageAssetFk:
      revision?.heroImageAssetFk && revision.heroImageAssetFk !== emptyGuid
        ? revision.heroImageAssetFk
        : "",
    heroImagePath: revision?.heroImagePath ?? "",
    markdown: revision?.markdown || defaults.markdown,
    comment: "",
    publish: false,
  };
}

export function createPublicPageEditorSchema(messages: PublicPageEditorValidationMessages) {
  return z.object({
    title: z.string().trim().min(1, messages.title).max(180, messages.title),
    summary: z.string().trim().max(512, messages.summary),
    heroKicker: z.string().trim().max(96, messages.heroKicker),
    heroTitle: z.string().trim().min(1, messages.heroTitle).max(180, messages.heroTitle),
    heroDescription: z.string().trim().max(512, messages.heroDescription),
    heroImageAssetFk: z.uuid(messages.heroImage),
    heroImagePath: z.string(),
    markdown: z.string().trim().min(1, messages.markdown).max(60_000, messages.markdown),
    comment: z.string().trim().max(512, messages.comment),
    publish: z.boolean(),
  });
}

export interface GdprEditorState {
  content: GdprContent;
  preserveLegacyMarkdown: boolean;
}

export function createGdprEditorState(revision: PublicPageRevision | undefined): GdprEditorState {
  const content = parseStructuredContent(revision?.contentJson);
  return {
    content: content ?? createDefaultGdprContent(),
    preserveLegacyMarkdown: Boolean(revision?.markdown.trim() && !content),
  };
}

export function createPublicPageRevisionPayload(
  values: PublicPageEditorValues,
  isGdpr: boolean,
  gdprState: GdprEditorState,
  sourceRevision: PublicPageRevision | undefined,
): PublicPageRevisionPayload {
  const markdown = isGdpr
    ? gdprState.preserveLegacyMarkdown
      ? values.markdown
      : gdprContentToMarkdown(gdprState.content)
    : values.markdown;
  const contentJson = isGdpr
    ? gdprState.preserveLegacyMarkdown
      ? (sourceRevision?.contentJson ?? "")
      : serializeGdprContent(gdprState.content)
    : "";
  return {
    title: values.title.trim(),
    summary: values.summary.trim(),
    heroKicker: values.heroKicker.trim(),
    heroTitle: values.heroTitle.trim(),
    heroDescription: values.heroDescription.trim(),
    heroImageAssetFk: values.heroImageAssetFk,
    markdown: markdown.trim(),
    contentJson,
    comment: values.comment.trim(),
    isPublished: values.publish,
  };
}
