# Localization Page Audit Tracker

Created: 2026-06-24

This checklist tracks every Razor file under `sources/WebApp/Pages` that can contribute user-facing page text. Route-only pages list their `@page` routes; nested page helper components are listed separately.

## Routed Pages

- [x] `sources\WebApp\Pages\Authentication\_Action\Index.razor` - `/authentication/{action}`
- [x] `sources\WebApp\Pages\Command\Audit\Index.razor` - `/command/audit`
- [x] `sources\WebApp\Pages\Command\Billing\Index.razor` - `/command/billing`
- [x] `sources\WebApp\Pages\Command\Claims\Index.razor` - `/command/claims`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\Index.razor` - `/command/cloud/configuration`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\Missions\_MissionReferenceVersionId\Edit.razor` - `/command/cloud/configuration/missions/{MissionReferenceVersionId:long}/edit`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\Missions\_MissionReferenceVersionId\Index.razor` - `/command/cloud/configuration/missions/{MissionReferenceVersionId:long}`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\Missions\Create.razor` - `/command/cloud/configuration/missions/create`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\Missions\Index.razor` - `/command/cloud/configuration/missions`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\RuntimeProfiles\_RuntimeProfileVersionId\Edit.razor` - `/command/cloud/configuration/runtime-profiles/{RuntimeProfileVersionId:long}/edit`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\RuntimeProfiles\_RuntimeProfileVersionId\Index.razor` - `/command/cloud/configuration/runtime-profiles/{RuntimeProfileVersionId:long}`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\RuntimeProfiles\Create.razor` - `/command/cloud/configuration/runtime-profiles/create`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\RuntimeProfiles\Index.razor` - `/command/cloud/configuration/runtime-profiles`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\Templates\_TemplateVersionId\Edit.razor` - `/command/cloud/configuration/templates/{TemplateVersionId:long}/edit`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\Templates\_TemplateVersionId\Index.razor` - `/command/cloud/configuration/templates/{TemplateVersionId:long}`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\Templates\Create.razor` - `/command/cloud/configuration/templates/create`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\Templates\Index.razor` - `/command/cloud/configuration/templates`
- [x] `sources\WebApp\Pages\Command\Cloud\Costs\Estimates\_EstimateId\Index.razor` - `/command/cloud/costs/estimates/{EstimateId:long}`
- [x] `sources\WebApp\Pages\Command\Cloud\Costs\Index.razor` - `/command/cloud/costs`
- [x] `sources\WebApp\Pages\Command\Cloud\Index.razor` - `/command/cloud`
- [x] `sources\WebApp\Pages\Command\Cloud\Metadata\Index.razor` - `/command/cloud/metadata`
- [x] `sources\WebApp\Pages\Command\Cloud\Plans\_PlanId\Index.razor` - `/command/cloud/plans/{PlanId:long}`
- [x] `sources\WebApp\Pages\Command\Cloud\Plans\Index.razor` - `/command/cloud/plans`
- [x] `sources\WebApp\Pages\Command\Cloud\Policies\_ScopeKind\_ScopeId\Index.razor` - `/command/cloud/policies/{ScopeKind}/{ScopeId}`
- [x] `sources\WebApp\Pages\Command\Cloud\Policies\Index.razor` - `/command/cloud/policies`
- [x] `sources\WebApp\Pages\Command\Cloud\Reconciliation\_RunId\Index.razor` - `/command/cloud/reconciliation/{RunId:long}`
- [x] `sources\WebApp\Pages\Command\Cloud\Reconciliation\Index.razor` - `/command/cloud/reconciliation`
- [x] `sources\WebApp\Pages\Command\Cloud\Resources\_ResourceId\Index.razor` - `/command/cloud/resources/{ResourceId:long}`
- [x] `sources\WebApp\Pages\Command\Cloud\Resources\Index.razor` - `/command/cloud/resources`
- [x] `sources\WebApp\Pages\Command\Cloud\Sessions\_SessionId\Index.razor` - `/command/cloud/sessions/{SessionId:long}`
- [x] `sources\WebApp\Pages\Command\Cloud\Sessions\Index.razor` - `/command/cloud/sessions`
- [x] `sources\WebApp\Pages\Command\Content\Pages\_Slug\Edit.razor` - `/command/content/pages/{Slug}/edit`
- [x] `sources\WebApp\Pages\Command\Content\Pages\_Slug\Index.razor` - `/command/content/pages/{Slug}`
- [x] `sources\WebApp\Pages\Command\Content\Pages\Index.razor` - `/command/content/pages`
- [x] `sources\WebApp\Pages\Command\FieldReports\Edit.razor` - `/command/field-reports/create`, `/command/field-reports/{ReportId:guid}`
- [x] `sources\WebApp\Pages\Command\FieldReports\Index.razor` - `/command/field-reports`, `/command/wiki-media`
- [x] `sources\WebApp\Pages\Command\Index.razor` - `/command`
- [x] `sources\WebApp\Pages\Command\Members\Details.razor` - `/command/members/{UserId:guid}`
- [x] `sources\WebApp\Pages\Command\Members\Index.razor` - `/command/members`
- [x] `sources\WebApp\Pages\Command\Review\Index.razor` - `/command/review`
- [x] `sources\WebApp\Pages\Command\Roles\Index.razor` - `/command/roles`
- [x] `sources\WebApp\Pages\Command\Settings\Index.razor` - `/command/settings`
- [x] `sources\WebApp\Pages\Command\Verein\Documents\_DocumentId\Edit.razor` - `/command/verein/documents/{DocumentId:long}/edit`
- [x] `sources\WebApp\Pages\Command\Verein\Documents\_DocumentId\Index.razor` - `/command/verein/documents/{DocumentId:long}`
- [x] `sources\WebApp\Pages\Command\Verein\Documents\Create.razor` - `/command/verein/documents/create`
- [x] `sources\WebApp\Pages\Command\Verein\Documents\Index.razor` - `/command/verein/documents`
- [x] `sources\WebApp\Pages\Command\Verein\Kassenbuch\_EntryId\Edit.razor` - `/command/verein/kassenbuch/{EntryId:long}/edit`
- [x] `sources\WebApp\Pages\Command\Verein\Kassenbuch\Create.razor` - `/command/verein/kassenbuch/create`
- [x] `sources\WebApp\Pages\Command\Verein\Kassenbuch\Index.razor` - `/command/verein/kassenbuch`
- [x] `sources\WebApp\Pages\Command\Verein\MembershipRequests\Index.razor` - `/command/verein/membership-requests`
- [x] `sources\WebApp\Pages\Command\Verein\Settings\Index.razor` - `/command/verein/settings`
- [x] `sources\WebApp\Pages\Command\WorkersJobs\Index.razor` - `/command/workers-jobs`
- [x] `sources\WebApp\Pages\Data\ModPacks\_ModPackId\Index.razor` - `/data/mod-packs/{ModPackId:long}`, `/command/mod-packs/{ModPackId:long}`, `/command/mod-packs/{ModPackId:long}/{Section}`
- [x] `sources\WebApp\Pages\Data\ModPacks\All\Index.razor` - `/data/mod-packs/all`, `/command/mod-packs`
- [x] `sources\WebApp\Pages\Data\ModPacks\Create\Index.razor` - `/data/mod-packs/create`, `/command/mod-packs/create`
- [x] `sources\WebApp\Pages\Data\Terrains\_TerrainId\Index.razor` - `/data/terrains/{TerrainId:long}`, `/command/terrains/{TerrainId:long}`
- [x] `sources\WebApp\Pages\Data\Terrains\All\Index.razor` - `/data/terrains/all`, `/command/terrains`
- [x] `sources\WebApp\Pages\Data\Terrains\Create\Index.razor` - `/data/terrains/create`, `/command/terrains/create`
- [x] `sources\WebApp\Pages\Events\_EventId\Index.razor` - `/events/{EventId:guid}`
- [x] `sources\WebApp\Pages\Events\All\Index.razor` - `/events/all`, `/command/events`
- [x] `sources\WebApp\Pages\Events\Create\Index.razor` - `/events/create`, `/command/events/create`
- [x] `sources\WebApp\Pages\Events\Edit\_EventId\Index.razor` - `/command/events/{EventId:guid}`
- [x] `sources\WebApp\Pages\Events\Upcoming\Index.razor` - `/events/upcoming`
- [x] `sources\WebApp\Pages\FieldReports\_Slug\Index.razor` - `/field-reports/{Slug}`, `/media/{Slug}`
- [x] `sources\WebApp\Pages\GDPR\Index.razor` - `/gdpr`
- [x] `sources\WebApp\Pages\Index.razor` - `/`
- [x] `sources\WebApp\Pages\Join\Index.razor` - `/join`
- [x] `sources\WebApp\Pages\Media\Index.razor` - `/field-reports`, `/media`
- [x] `sources\WebApp\Pages\Members\Index.razor` - `/members`
- [x] `sources\WebApp\Pages\Servers\Advanced.razor` - `/servers/advanced`, `/command/servers/advanced`
- [x] `sources\WebApp\Pages\Servers\Cloud.razor` - `/servers/cloud`
- [x] `sources\WebApp\Pages\Servers\Create.razor` - `/command/servers/create`
- [x] `sources\WebApp\Pages\Servers\Details.razor` - `/command/servers/{GameServerId:long}`, `/command/servers/{GameServerId:long}/{Section}`
- [x] `sources\WebApp\Pages\Servers\Index.razor` - `/command/servers`, `/servers`
- [x] `sources\WebApp\Pages\Settings\Index.razor` - `/settings`
- [x] `sources\WebApp\Pages\Unit\Beschluesse\_DocumentId\Index.razor` - `/unit/beschluesse/{DocumentId:long}`, `/verein/beschluesse/{DocumentId:long}`
- [x] `sources\WebApp\Pages\Unit\Beschluesse\Index.razor` - `/unit/beschluesse`, `/verein/beschluesse`
- [x] `sources\WebApp\Pages\Unit\Index.razor` - `/unit`
- [x] `sources\WebApp\Pages\Unit\Kassenbuch\Index.razor` - `/unit/kassenbuch`, `/verein/kassenbuch`
- [x] `sources\WebApp\Pages\Unit\Satzung\Index.razor` - `/unit/satzung`, `/verein/satzung`
- [x] `sources\WebApp\Pages\Verein\Index.razor` - `/verein`
- [x] `sources\WebApp\Pages\Wiki\Index.razor` - `/wiki`, `/wiki/{PageId:guid}`

## Page Helper Components

- [x] `sources\WebApp\Pages\Command\Cloud\CloudOperationReviewDialog.razor`
- [x] `sources\WebApp\Pages\Command\Cloud\CloudStatusStrip.razor`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\CloudServerTemplateFormFields.razor`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\MissionReferenceFormFields.razor`
- [x] `sources\WebApp\Pages\Command\Cloud\Configuration\RuntimeProfileFormFields.razor`
- [x] `sources\WebApp\Pages\Command\WikiMedia\Index.razor`
- [x] `sources\WebApp\Pages\Wiki\WikiTreeNode.razor`
## Verification Summary

- Source audit completed across all tracked routed pages and page helper components on 2026-06-24.
- Converted static localization calls to `nameof(Language.Identifier)` so missing resource properties fail at compile time.
- Remaining uncertain source text is recorded per touched source file under `.planning/localization-audit/unresolved/`.
- Verification commands completed: `dotnet build X39.UnitedTacticalForces.slnx`, string-literal localizer sweep, missing `nameof` resource-key sweep, and German `.resx` UTF-8/mojibake marker sweep.
