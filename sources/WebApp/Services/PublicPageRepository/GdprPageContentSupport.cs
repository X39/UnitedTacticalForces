using X39.UnitedTacticalForces.Common.PublicPages;


namespace X39.UnitedTacticalForces.WebApp.Services.PublicPageRepository;

internal static class GdprPageContentSupport
{
    public static string Serialize(GdprPageContent content)
        => GdprPageContentJson.Serialize(content);

    public static bool TryDeserialize(string? contentJson, out GdprPageContent content)
        => GdprPageContentJson.TryDeserialize(contentJson, out content);

    public static GdprPageContent CreateDefaultContent()
        => new()
        {
            Intro = new GdprIntroSection
            {
                Kicker            = "Living privacy notice",
                Title             = "How UTF handles community data",
                Description       = "This page explains the practical data flows behind accounts, events, servers, content, and association administration.",
                LastReviewedLabel = "Last reviewed",
                LastReviewedDate  = "June 2026",
                ProcessNote       = "When a workflow changes, publish a new revision so members can see the current operating practice.",
            },
            OpeningCards =
            [
                new()
                {
                    Kicker = "Responsibility",
                    Title  = "Community operators",
                    Paragraphs =
                    [
                        "United Tactical Forces operates the website, command-center workflows, event planning, wiki content, server orchestration, and Verein administration features for the community.",
                        "Administrators keep this notice aligned with real operational practice and handle member privacy requests.",
                    ],
                },
                new()
                {
                    Kicker = "Legal basis",
                    Title  = "Community operations and obligations",
                    Paragraphs =
                    [
                        "Data is processed to authenticate members, authorize access, coordinate events, run infrastructure, attribute costs, protect the platform, and document association administration where applicable.",
                        "Content submitted by members or editors is processed because it was provided for community use.",
                    ],
                },
            ],
            Lifecycle = new GdprTimelineSection
            {
                Kicker = "What happens when",
                Title  = "Data lifecycle",
                Steps =
                [
                    new()
                    {
                        Title      = "Visit",
                        Paragraphs =
                        [
                            "Public pages can be read without signing in.",
                            "Browser-local preferences are client-side and are not required to identify you.",
                        ],
                    },
                    new()
                    {
                        Title      = "Sign in",
                        Paragraphs =
                        [
                            "Steam or Discord redirects establish the external identity used for login.",
                            "The application stores the identifiers needed to link that external account to the local UTF account.",
                        ],
                    },
                    new()
                    {
                        Title      = "Participate",
                        Paragraphs =
                        [
                            "Events, slots, roles, wiki edits, field reports, and membership workflows create operational records so the community can coordinate across sessions.",
                        ],
                    },
                    new()
                    {
                        Title      = "Operate servers",
                        Paragraphs =
                        [
                            "Server lifecycle actions create jobs, worker records, audit entries, and cost attribution records.",
                            "Credentials and provider tokens stay server-side and are not exposed to browser clients.",
                        ],
                    },
                    new()
                    {
                        Title      = "Request changes",
                        Paragraphs =
                        [
                            "Members can ask an administrator for access, correction, export, or deletion.",
                            "Some audit, billing, security, and association records may need to be retained or anonymized instead of deleted outright.",
                        ],
                    },
                ],
            },
            DataMap = new GdprDataMapSection
            {
                Kicker = "What is stored and why",
                Title  = "Data map",
                Cards =
                [
                    new()
                    {
                        Title        = "Account and identity",
                        DataLabel    = "Data",
                        Data         = "Local user ID, nickname, optional email, avatar, Steam ID64, Discord ID and username, roles, claims, verification, ban state, deleted marker.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Authentication, authorization, profile display, moderation, role-based access, and account support.",
                        WhereLabel   = "Where",
                        Where        = "Stored in the API database. Authentication state is reflected in a browser cookie.",
                    },
                    new()
                    {
                        Title        = "Events and participation",
                        DataLabel    = "Data",
                        Data         = "Event owners and hosts, attendance choices, slot assignment, timestamps, terrain and mod-pack references, event images and descriptions.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Planning operations, showing who is attending, organizing slots, and preserving event history.",
                        WhereLabel   = "Where",
                        Where        = "Stored in the API database and shown through operations pages according to permissions and event visibility.",
                    },
                    new()
                    {
                        Title        = "Content and assets",
                        DataLabel    = "Data",
                        Data         = "Wiki pages, revisions, audits, field reports, terrain entries, mod-pack definitions, and configured media references.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Publishing community information, maintaining review history, attributing edits, and recovering from mistakes.",
                        WhereLabel   = "Where",
                        Where        = "Stored in the API database and configured server or WebApp asset storage.",
                    },
                    new()
                    {
                        Title        = "Game-server operations",
                        DataLabel    = "Data",
                        Data         = "Server records, configuration entries, lifecycle events, jobs, worker registrations, heartbeats, logs, failure summaries, and audit records.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Running servers reliably, recovering from API restarts, tracing requested actions, diagnosing failures, and protecting secrets.",
                        WhereLabel   = "Where",
                        Where        = "Stored in the API database. Workers receive only the data needed to execute assigned lifecycle jobs.",
                    },
                    new()
                    {
                        Title        = "Cloud and billing attribution",
                        DataLabel    = "Data",
                        Data         = "Billing accounts, owners, server assignment intervals, cloud plans, cost estimates, provider metadata, inventory, policy checks, approvals, imports, reconciliation, and adjustments.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Ensuring every cost-generating session has an owner, keeping reviewable cost records, and preventing accidental or unauthorized spend.",
                        WhereLabel   = "Where",
                        Where        = "Stored in the API database. Provider access tokens are not exposed to frontend clients.",
                    },
                    new()
                    {
                        Title        = "Technical logs and diagnostics",
                        DataLabel    = "Data",
                        Data         = "HTTP access data, application logs, game-server logs, worker failure messages, provider verification status, and operational timestamps.",
                        PurposeLabel = "Purpose",
                        Purpose      = "Security monitoring, troubleshooting, abuse prevention, service reliability, and accountability.",
                        WhereLabel   = "Where",
                        Where        = "Stored in server logs, the API database, and the configured hosting environment.",
                    },
                ],
            },
            Visibility = new GdprCardSection
            {
                Kicker = "Who can see what",
                Title  = "Visibility",
                Cards =
                [
                    new()
                    {
                        Title = "Public",
                        Body  = "Only intentionally published pages, event summaries, wiki content, and other public community information are visible without member access.",
                    },
                    new()
                    {
                        Title = "Members",
                        Body  = "Authenticated members can see community workflows according to their roles and claims.",
                    },
                    new()
                    {
                        Title = "Administrators",
                        Body  = "Authorized administrators can manage records, review audits, manage billing attribution, inspect logs, and resolve incidents.",
                    },
                ],
            },
            Partners = new GdprCardSection
            {
                Kicker = "Third parties and processors",
                Title  = "External services",
                Cards =
                [
                    new()
                    {
                        Title = "Steam",
                        Body  = "Steam sign-in confirms identity and the API stores the Steam ID64 plus public profile information needed for the local account.",
                    },
                    new()
                    {
                        Title = "Discord",
                        Body  = "Discord sign-in and bot integrations use Discord account identifiers and operate under Discord's own terms.",
                    },
                    new()
                    {
                        Title = "Hosting providers",
                        Body  = "Cloud and hosting providers may process infrastructure metadata for planning, operation, reconciliation, and cost attribution.",
                    },
                ],
            },
            PolicyCards =
            [
                new()
                {
                    Kicker = "Retention",
                    Title  = "Kept only while useful or required",
                    Paragraphs =
                    [
                        "Operational records are retained while needed for community administration, security, billing attribution, or legal documentation.",
                        "Retention can differ by workflow because event history, audit records, billing documents, and association records serve different purposes.",
                    ],
                },
                new()
                {
                    Kicker = "Deletion",
                    Title  = "Deletion and anonymization",
                    Paragraphs =
                    [
                        "Members can request deletion of personal data through an administrator.",
                        "Some records may be anonymized or retained where audit, accounting, security, or legal duties require a durable record.",
                    ],
                },
                new()
                {
                    Kicker = "Security",
                    Title  = "Secret handling",
                    Paragraphs =
                    [
                        "Steam credentials, provider tokens, worker credentials, and similar secrets are server-side only.",
                        "Secrets must not be exposed to frontend clients, public pages, screenshots, or unredacted logs.",
                    ],
                },
                new()
                {
                    Kicker = "Member rights",
                    Title  = "Access, correction, export",
                    Paragraphs =
                    [
                        "Members can ask for access to their personal data, correction of inaccurate data, export where practical, or restriction of processing.",
                        "Administrators should document the request and outcome so privacy handling remains auditable.",
                    ],
                },
            ],
        };

}
