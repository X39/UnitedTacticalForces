using NodaTime;
using X39.UnitedTacticalForces.Api.Services.Notifications;
using X39.UnitedTacticalForces.Hosting.Contracts;
using Xunit;

namespace Api.Tests.GameServerHosting;

/// <summary>
/// Proves that the cost alert renderer produces localized, control-character-free text from the resource files and
/// that locale selection falls back to English.
/// </summary>
public sealed class CostAlertMessageRendererTests
{
    private static readonly Instant CreatedAt = Instant.FromUtc(2026, 11, 21, 15, 4);

    private static CostAlertMessageModel CreateModel(string title = "Ops Night", string? link = null)
        => new(title, 5m, 5.0312m, "EUR", CreatedAt, link);

    /// <summary>
    /// A German locale renders German text with a comma decimal separator, the ISO currency code and a UTC suffix.
    /// </summary>
    [Fact]
    public void Render_GermanLocale_UsesGermanTextAndCommaSeparator()
    {
        var message = CostAlertMessageRenderer.Render(CreateModel(), "de-DE");

        Assert.Equal("Kostenwarnung: Ops Night hat 5,00 EUR erreicht", message.Subject);
        Assert.Contains("5,03 EUR", message.Body);
        Assert.Contains("UTC", message.Body);
        Assert.Contains("21.11.2026 15:04", message.Body);
    }

    /// <summary>
    /// A missing locale renders English with a point decimal separator.
    /// </summary>
    [Fact]
    public void Render_NullLocale_FallsBackToEnglish()
    {
        var message = CostAlertMessageRenderer.Render(CreateModel(), null);

        Assert.Equal("Cost alert: Ops Night reached 5.00 EUR", message.Subject);
        Assert.Contains("5.03 EUR", message.Body);
    }

    /// <summary>
    /// An unsupported locale renders English.
    /// </summary>
    [Fact]
    public void Render_UnsupportedLocale_FallsBackToEnglish()
    {
        var message = CostAlertMessageRenderer.Render(CreateModel(), "fr");

        Assert.Equal("Cost alert: Ops Night reached 5.00 EUR", message.Subject);
    }

    /// <summary>
    /// Carriage return, line feed and tab characters in a server title never reach the subject or body.
    /// </summary>
    [Fact]
    public void Render_TitleWithControlCharacters_ProducesNoControlCharacterInSubject()
    {
        var message = CostAlertMessageRenderer.Render(CreateModel("Ops\r\nBcc: x\tNight"), "en");

        Assert.DoesNotContain('\r', message.Subject);
        Assert.DoesNotContain('\n', message.Subject);
        Assert.DoesNotContain('\t', message.Subject);
        Assert.Equal("Cost alert: Ops Bcc: x Night reached 5.00 EUR", message.Subject);
    }

    /// <summary>
    /// The server link appears in the body only when it is known.
    /// </summary>
    [Fact]
    public void Render_ServerLink_AppearsOnlyWhenSet()
    {
        const string link = "https://utf.example/servers/1";

        var withLink = CostAlertMessageRenderer.Render(CreateModel(link: link), "en");
        var withoutLink = CostAlertMessageRenderer.Render(CreateModel(), "en");

        Assert.Contains(link, withLink.Body);
        Assert.DoesNotContain("https://", withoutLink.Body);
        Assert.Contains("Open the server in the app", withoutLink.Body);
    }

    /// <summary>
    /// Locale values are reduced to a supported language, tolerating region suffixes and underscores.
    /// </summary>
    /// <param name="value">The recorded locale.</param>
    /// <param name="expected">The expected language code.</param>
    [Theory]
    [InlineData("de", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("DE_de", "de")]
    [InlineData(" en-GB ", "en")]
    [InlineData("fr", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalize_ReducesToSupportedLanguage(string? value, string? expected)
        => Assert.Equal(expected, MessageLocales.Normalize(value));

    /// <summary>
    /// The Discord text joins subject and body with a blank line.
    /// </summary>
    [Fact]
    public void ToDiscordText_JoinsSubjectAndBodyWithBlankLine()
    {
        var message = new CostAlertMessage("Subject", "Body");

        Assert.Equal("Subject\n\nBody", CostAlertMessageRenderer.ToDiscordText(message));
    }

    /// <summary>
    /// The Discord text never exceeds the 2000 character message limit.
    /// </summary>
    [Fact]
    public void ToDiscordText_TruncatesToDiscordLimit()
    {
        var message = new CostAlertMessage("Subject", new string('x', 5000));

        Assert.Equal(2000, CostAlertMessageRenderer.ToDiscordText(message).Length);
    }

    private static readonly Instant StopsAt = Instant.FromUtc(2026, 11, 21, 18, 30);

    private static AutoStopMessageModel CreateAutoStopModel(
        EAutoStopReason reason = EAutoStopReason.Idle,
        int? limit = 30,
        string title = "Ops Night",
        string? link = null)
        => new(title, link, reason, limit, StopsAt);

    /// <summary>
    /// An idle warning in English names the server, the stop time and the idle limit, says the server shuts down unless
    /// someone joins, then adds the consequence sentence.
    /// </summary>
    [Fact]
    public void RenderAutoStop_IdleEnglish_NamesServerLimitAndStopTime()
    {
        var message = CostAlertMessageRenderer.RenderAutoStop(CreateAutoStopModel(), "en");

        Assert.Equal("Automatic shutdown of Ops Night at 11/21/2026 6:30 PM UTC", message.Subject);
        Assert.Contains("Nobody has been on Ops Night for 30 minutes.", message.Body);
        Assert.Contains("shuts down at 11/21/2026 6:30 PM UTC unless someone joins.", message.Body);
        Assert.Contains("It saves its data, stops, and releases the cloud machine.", message.Body);
        Assert.DoesNotContain("https://", message.Body);
    }

    /// <summary>
    /// The maximum runtime and schedule reasons each produce their own body sentence.
    /// </summary>
    [Fact]
    public void RenderAutoStop_MaxRuntimeAndSchedule_UseTheirOwnSentences()
    {
        var maxRuntime = CostAlertMessageRenderer.RenderAutoStop(
            CreateAutoStopModel(EAutoStopReason.MaxRuntime, 6),
            "en");
        var schedule = CostAlertMessageRenderer.RenderAutoStop(
            CreateAutoStopModel(EAutoStopReason.Schedule, null),
            "en");

        Assert.Contains("reached its maximum runtime of 6 hours and shuts down at", maxRuntime.Body);
        Assert.Contains("A schedule shuts Ops Night down at", schedule.Body);
        Assert.DoesNotContain("minutes", schedule.Body);
    }

    /// <summary>
    /// German renders all three reasons with informal du wording and the terms Kostenträger and Cloud-Maschine where
    /// the text uses them.
    /// </summary>
    [Fact]
    public void RenderAutoStop_German_RendersAllReasonsInformally()
    {
        var idle = CostAlertMessageRenderer.RenderAutoStop(CreateAutoStopModel(), "de");
        var maxRuntime = CostAlertMessageRenderer.RenderAutoStop(
            CreateAutoStopModel(EAutoStopReason.MaxRuntime, 6),
            "de");
        var schedule = CostAlertMessageRenderer.RenderAutoStop(
            CreateAutoStopModel(EAutoStopReason.Schedule, null),
            "de");

        Assert.Equal("Automatisches Herunterfahren von Ops Night um 21.11.2026 18:30 UTC", idle.Subject);
        Assert.Contains("Seit 30 Minuten war niemand auf Ops Night.", idle.Body);
        Assert.Contains("maximale Laufzeit von 6 Stunden", maxRuntime.Body);
        Assert.Contains("Ein Zeitplan fährt Ops Night um 21.11.2026 18:30 UTC herunter.", schedule.Body);
        Assert.Contains("Cloud-Maschine", idle.Body);
        Assert.Contains("Du erhältst", idle.Body);
        Assert.Contains("Kostenträgers", idle.Body);
    }

    /// <summary>
    /// A server title with CR, LF and tab characters is sanitized in the AutoStop subject exactly as for threshold
    /// alerts.
    /// </summary>
    [Fact]
    public void RenderAutoStop_TitleWithControlCharacters_ProducesNoControlCharacterInSubject()
    {
        var message = CostAlertMessageRenderer.RenderAutoStop(
            CreateAutoStopModel(title: "Ops\r\nBcc: x\tNight"),
            "en");

        Assert.DoesNotContain('\r', message.Subject);
        Assert.DoesNotContain('\n', message.Subject);
        Assert.DoesNotContain('\t', message.Subject);
        Assert.StartsWith("Automatic shutdown of Ops Bcc: x Night at ", message.Subject);
    }

    /// <summary>
    /// The server link points to the automation panel and appears only when it is known.
    /// </summary>
    [Fact]
    public void RenderAutoStop_ServerLink_PointsToAutomationPanelOnlyWhenSet()
    {
        const string link = "https://utf.example/servers/1";

        var withLink = CostAlertMessageRenderer.RenderAutoStop(CreateAutoStopModel(link: link), "en");
        var withoutLink = CostAlertMessageRenderer.RenderAutoStop(CreateAutoStopModel(), "en");

        Assert.Contains("Change automatic shutdown: " + link + "?tab=settings#server-automation", withLink.Body);
        Assert.DoesNotContain("Change automatic shutdown", withoutLink.Body);
    }

    /// <summary>
    /// An unknown stop reason is rejected instead of rendering a wrong sentence.
    /// </summary>
    [Fact]
    public void RenderAutoStop_UnknownReason_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => CostAlertMessageRenderer.RenderAutoStop(CreateAutoStopModel((EAutoStopReason) 99), "en"));
}
