import type { GdprContent } from "@/pages/GdprPage";

const defaultContent: GdprContent = {
  intro: {
    kicker: "Living privacy notice",
    title: "How UTF handles community data",
    description:
      "This page explains the practical data flows behind accounts, events, servers, content, and association administration.",
    lastReviewedLabel: "Last reviewed",
    lastReviewedDate: "June 2026",
    processNote:
      "When a workflow changes, publish a new revision so members can see the current operating practice.",
  },
  openingCards: [
    {
      kicker: "Responsibility",
      title: "Community operators",
      paragraphs: [
        "United Tactical Forces operates the website, command-center workflows, event planning, wiki content, server orchestration, and Verein administration features for the community.",
        "Administrators keep this notice aligned with real operational practice and handle member privacy requests.",
      ],
    },
    {
      kicker: "Legal basis",
      title: "Community operations and obligations",
      paragraphs: [
        "Data is processed to authenticate members, authorize access, coordinate events, run infrastructure, attribute costs, protect the platform, and document association administration where applicable.",
        "Content submitted by members or editors is processed because it was provided for community use.",
      ],
    },
  ],
  lifecycle: {
    kicker: "What happens when",
    title: "Data lifecycle",
    steps: [
      {
        title: "Visit",
        paragraphs: [
          "Public pages can be read without signing in.",
          "Browser-local preferences are client-side and are not required to identify you.",
        ],
      },
      {
        title: "Sign in",
        paragraphs: [
          "Steam or Discord redirects establish the external identity used for login.",
          "The application stores the identifiers needed to link that external account to the local UTF account.",
        ],
      },
      {
        title: "Participate",
        paragraphs: [
          "Events, slots, roles, wiki edits, field reports, and membership workflows create operational records so the community can coordinate across sessions.",
        ],
      },
      {
        title: "Operate servers",
        paragraphs: [
          "Server lifecycle actions create jobs, worker records, audit entries, and cost attribution records.",
          "Credentials and provider tokens stay server-side and are not exposed to browser clients.",
        ],
      },
      {
        title: "Request changes",
        paragraphs: [
          "Members can ask an administrator for access, correction, export, or deletion.",
          "Some audit, billing, security, and association records may need to be retained or anonymized instead of deleted outright.",
        ],
      },
    ],
  },
  dataMap: {
    kicker: "What is stored and why",
    title: "Data map",
    cards: [
      {
        title: "Account and identity",
        dataLabel: "Data",
        data: "Local user ID, nickname, optional email, avatar, Steam ID64, Discord ID and username, roles, claims, verification, ban state, deleted marker.",
        purposeLabel: "Purpose",
        purpose:
          "Authentication, authorization, profile display, moderation, role-based access, and account support.",
        whereLabel: "Where",
        where: "Stored in the API database. Authentication state is reflected in a browser cookie.",
      },
      {
        title: "Events and participation",
        dataLabel: "Data",
        data: "Event owners and hosts, attendance choices, slot assignment, timestamps, terrain and mod-pack references, event images and descriptions.",
        purposeLabel: "Purpose",
        purpose:
          "Planning operations, showing who is attending, organizing slots, and preserving event history.",
        whereLabel: "Where",
        where:
          "Stored in the API database and shown through operations pages according to permissions and event visibility.",
      },
      {
        title: "Content and assets",
        dataLabel: "Data",
        data: "Wiki pages, revisions, audits, field reports, terrain entries, mod-pack definitions, and configured media references.",
        purposeLabel: "Purpose",
        purpose:
          "Publishing community information, maintaining review history, attributing edits, and recovering from mistakes.",
        whereLabel: "Where",
        where: "Stored in the API database and configured server or WebApp asset storage.",
      },
      {
        title: "Game-server operations",
        dataLabel: "Data",
        data: "Server records, configuration entries, lifecycle events, jobs, worker registrations, heartbeats, logs, failure summaries, and audit records.",
        purposeLabel: "Purpose",
        purpose:
          "Running servers reliably, recovering from API restarts, tracing requested actions, diagnosing failures, and protecting secrets.",
        whereLabel: "Where",
        where:
          "Stored in the API database. Workers receive only the data needed to execute assigned lifecycle jobs.",
      },
      {
        title: "Cloud and billing attribution",
        dataLabel: "Data",
        data: "Billing accounts, owners, server assignment intervals, cloud plans, cost estimates, provider metadata, inventory, policy checks, approvals, imports, reconciliation, and adjustments.",
        purposeLabel: "Purpose",
        purpose:
          "Ensuring every cost-generating session has an owner, keeping reviewable cost records, and preventing accidental or unauthorized spend.",
        whereLabel: "Where",
        where:
          "Stored in the API database. Provider access tokens are not exposed to frontend clients.",
      },
      {
        title: "Technical logs and diagnostics",
        dataLabel: "Data",
        data: "HTTP access data, application logs, game-server logs, worker failure messages, provider verification status, and operational timestamps.",
        purposeLabel: "Purpose",
        purpose:
          "Security monitoring, troubleshooting, abuse prevention, service reliability, and accountability.",
        whereLabel: "Where",
        where: "Stored in server logs, the API database, and the configured hosting environment.",
      },
    ],
  },
  visibility: {
    kicker: "Who can see what",
    title: "Visibility",
    cards: [
      {
        title: "Public",
        body: "Only intentionally published pages, event summaries, wiki content, and other public community information are visible without member access.",
      },
      {
        title: "Members",
        body: "Authenticated members can see community workflows according to their roles and claims.",
      },
      {
        title: "Administrators",
        body: "Authorized administrators can manage records, review audits, manage billing attribution, inspect logs, and resolve incidents.",
      },
    ],
  },
  partners: {
    kicker: "Third parties and processors",
    title: "External services",
    cards: [
      {
        title: "Steam",
        body: "Steam sign-in confirms identity and the API stores the Steam ID64 plus public profile information needed for the local account.",
      },
      {
        title: "Discord",
        body: "Discord sign-in and bot integrations use Discord account identifiers and operate under Discord's own terms.",
      },
      {
        title: "Hosting providers",
        body: "Cloud and hosting providers may process infrastructure metadata for planning, operation, reconciliation, and cost attribution.",
      },
    ],
  },
  policyCards: [
    {
      kicker: "Retention",
      title: "Kept only while useful or required",
      paragraphs: [
        "Operational records are retained while needed for community administration, security, billing attribution, or legal documentation.",
        "Retention can differ by workflow because event history, audit records, billing documents, and association records serve different purposes.",
      ],
    },
    {
      kicker: "Deletion",
      title: "Deletion and anonymization",
      paragraphs: [
        "Members can request deletion of personal data through an administrator.",
        "Some records may be anonymized or retained where audit, accounting, security, or legal duties require a durable record.",
      ],
    },
    {
      kicker: "Security",
      title: "Secret handling",
      paragraphs: [
        "Steam credentials, provider tokens, worker credentials, and similar secrets are server-side only.",
        "Secrets must not be exposed to frontend clients, public pages, screenshots, or unredacted logs.",
      ],
    },
    {
      kicker: "Member rights",
      title: "Access, correction, export",
      paragraphs: [
        "Members can ask for access to their personal data, correction of inaccurate data, export where practical, or restriction of processing.",
        "Administrators should document the request and outcome so privacy handling remains auditable.",
      ],
    },
  ],
};

export function createDefaultGdprContent(): GdprContent {
  return structuredClone(defaultContent);
}

export function serializeGdprContent(content: GdprContent): string {
  return JSON.stringify(content, null, 2);
}

function heading(level: number, value: string): string[] {
  const text = value.trim();
  return text ? [`${"#".repeat(level)} ${text}`, ""] : [];
}

function paragraph(value: string): string[] {
  const text = value.trim();
  return text ? [text, ""] : [];
}

export function gdprContentToMarkdown(content: GdprContent): string {
  const output: string[] = [
    ...heading(1, content.intro.title),
    ...paragraph(content.intro.description),
  ];
  for (const card of content.openingCards) {
    output.push(...heading(2, card.title));
    for (const value of card.paragraphs) output.push(...paragraph(value));
  }
  output.push(...heading(2, content.lifecycle.title));
  for (const step of content.lifecycle.steps) {
    output.push(...heading(3, step.title));
    for (const value of step.paragraphs) output.push(...paragraph(value));
  }
  output.push(...heading(2, content.dataMap.title));
  for (const card of content.dataMap.cards) {
    output.push(...heading(3, card.title));
    const entries: ReadonlyArray<readonly [string, string]> = [
      [card.dataLabel, card.data],
      [card.purposeLabel, card.purpose],
      [card.whereLabel, card.where],
    ];
    for (const [label, value] of entries) {
      if (value.trim()) output.push(`- **${label.trim() || "Value"}:** ${value.trim()}`);
    }
    output.push("");
  }
  for (const section of [content.visibility, content.partners]) {
    output.push(...heading(2, section.title));
    for (const card of section.cards) {
      output.push(...heading(3, card.title), ...paragraph(card.body));
    }
  }
  for (const card of content.policyCards) {
    output.push(...heading(2, card.title));
    for (const value of card.paragraphs) output.push(...paragraph(value));
  }
  return output.join("\n").trim();
}
