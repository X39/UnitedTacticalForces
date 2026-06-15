using Xunit;

namespace Api.Tests.WebApp;

public class EventAuthoringBoundaryTests
{
    [Fact]
    public void EventAdministration_RoutesRowsToEditorAndKeepsSlotsActionPublic()
    {
        const string file = "sources/WebApp/Pages/Events/All/Index.razor";
        var source = ReadRepositoryFile(file);
        var failures = new List<string>();

        AssertContains(source, "NavigateToEventEditor(row.PlainEvent.PrimaryKey)", file, "admin row editor route", failures);
        AssertContains(source, "ResolveSelfUrl($\"command/events/{eventId}\")", file, "admin row editor route", failures);
        AssertContains(source, "NavigateToEventSlots(row.PlainEvent.PrimaryKey)", file, "slot action route", failures);
        AssertContains(source, "ResolveSelfUrl($\"events/{eventId}\")", file, "slot action route", failures);
        AssertContains(source, "Pages_Events_All_ViewSlots", file, "slot action label", failures);

        AssertNoFailures(failures);
    }

    [Fact]
    public void EventCreate_UsesFullPageReviewStepperAndCreatesSlotDrafts()
    {
        const string file = "sources/WebApp/Pages/Events/Create/Index.razor";
        var source = ReadRepositoryFile(file);
        var failures = new List<string>();

        foreach (var marker in new[]
                 {
                     "private const int FinalStep = 6",
                     "EventAuthoringModel.CreateDefault()",
                     "List<EventSlotDraftModel>",
                     "Auto_ReviewYourOperationPackage_09d725e7",
                     "CreateSlotAsync(createdEvent.PrimaryKey.Value",
                     "ResolveSelfUrl(route)",
                     "?slotSetup=failed",
                 })
        {
            AssertContains(source, marker, file, "create event authoring flow", failures);
        }

        AssertDoesNotContain(source, "event-create-preview", file, "old side preview removal", failures);

        AssertNoFailures(failures);
    }

    [Fact]
    public void EventEditor_UsesExistingEventAndSlotRepositories()
    {
        const string file = "sources/WebApp/Pages/Events/Edit/_EventId/Index.razor";
        var source = ReadRepositoryFile(file);
        var failures = new List<string>();

        foreach (var marker in new[]
                 {
                     "@page \"/command/events/{EventId:guid}\"",
                     "EventRepository.GetEventAsync(EventId)",
                     "EventRepository.ModifyEventAsync(EventId, _model.ToUpdatePayload())",
                     "SlotRepository.AllSlotsAsync(EventId)",
                     "SlotRepository.CreateSlotAsync(EventId",
                     "SlotRepository.UpdateSlotAsync(EventId",
                     "SlotRepository.DeleteSlotAsync(EventId",
                     "SlotRepository.AssignSlotAsync(EventId",
                     "SlotRepository.UnassignSlotAsync(EventId",
                     "ScheduledForOriginal",
                 })
        {
            AssertContains(source, marker, file, "event editor repository flow", failures);
        }

        AssertNoFailures(failures);
    }

    [Fact]
    public void EventAuthoringModel_UsesMediaAssetReferencesForImages()
    {
        const string file = "sources/WebApp/Data/EventAuthoringModel.cs";
        var source = ReadRepositoryFile(file);
        var failures = new List<string>();

        AssertContains(source, "public Guid? ImageAssetFk", file, "media asset image reference", failures);
        AssertContains(source, "public string? ImagePath", file, "computed image render path", failures);
        AssertContains(source, "ImageAssetFk      = eventItem.ImageAssetFk", file, "media asset image reference", failures);
        AssertContains(source, "ImageAssetFk      = ImageAssetFk ?? default", file, "creation media asset payload", failures);
        AssertContains(source, "ImageAssetFk      = ImageAssetFk", file, "update media asset payload", failures);
        AssertDoesNotContain(source, "ImageMimeType", file, "raw image field removal marker", failures);
        AssertDoesNotContain(source, "byte[]", file, "raw image field removal marker", failures);
        AssertDoesNotContain(source, "OwnerFk", file, "server-owned field absence marker", failures);
        AssertDoesNotContain(source, "ScheduledForOriginal", file, "server-owned field absence marker", failures);

        AssertNoFailures(failures);
    }

    [Fact]
    public void CommandSearch_CanKeepTokenizedInputStylingInsideCommandContent()
    {
        const string file = "sources/WebApp/wwwroot/css/app.css";
        var source = ReadRepositoryFile(file);
        var failures = new List<string>();

        AssertContains(source, ".utf-command-toolbar", file, "command toolbar", failures);
        AssertContains(source, "align-items: flex-end", file, "command toolbar", failures);
        AssertContains(source, ".utf-command-content .utf-command-input", file, "command input override", failures);
        AssertContains(source, ".utf-command-content .utf-command-search", file, "command input override", failures);

        AssertNoFailures(failures);
    }

    private static string ReadRepositoryFile(string repositoryRelativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot, repositoryRelativePath));

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "X39.UnitedTacticalForces.slnx")))
                directory = directory.Parent;

            return directory?.FullName
                   ?? throw new InvalidOperationException("Could not find repository root.");
        }
    }

    private static void AssertContains(
        string source,
        string marker,
        string file,
        string memberName,
        ICollection<string> failures)
    {
        if (!source.Contains(marker, StringComparison.Ordinal))
            failures.Add($"{file}: member '{memberName}' does not contain '{marker}'.");
    }

    private static void AssertDoesNotContain(
        string source,
        string marker,
        string file,
        string memberName,
        ICollection<string> failures)
    {
        if (source.Contains(marker, StringComparison.Ordinal))
            failures.Add($"{file}: member '{memberName}' should not contain '{marker}'.");
    }

    private static void AssertNoFailures(IEnumerable<string> failures)
    {
        var failureList = failures.ToArray();
        Assert.True(failureList.Length == 0, string.Join(Environment.NewLine, failureList));
    }
}
