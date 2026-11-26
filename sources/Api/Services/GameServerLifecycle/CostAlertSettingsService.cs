using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Updates;
using X39.UnitedTacticalForces.Api.Services.Notifications;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Hosting.Contracts;
using X39.Util;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

/// <summary>
/// Database-backed reading and saving of the cost alert configuration of a cost owner.
/// </summary>
/// <remarks>
/// Reading requires <see cref="IHostingAuthorizationService.CanViewCostOwnerAsync"/> or the right to manage the
/// owner, because a <c>co:use</c> holder is an alert recipient and must see what is configured. Saving requires
/// <see cref="IHostingAuthorizationService.CanUseCostOwnerAsync"/>. A save replaces the whole configuration inside
/// one transaction. The stored revision is claimed with a conditional update, so two saves based on the same
/// revision cannot both succeed even across API instances; the first save of an owner is guarded by the unique
/// index on the owner. No setting row is ever created by a read, so an owner that never saved has no alerts.
/// </remarks>
/// <param name="dbContext">The scoped database context the settings are stored in.</param>
/// <param name="authorizationService">The service that decides who may view or manage a cost owner.</param>
/// <param name="recipientResolver">The resolver that tells which people the alerts are addressed to.</param>
/// <param name="emailSender">The sender whose configuration state decides whether email can deliver.</param>
/// <param name="configuration">The configuration that tells whether the Discord integration is enabled.</param>
/// <param name="options">The limits on the number and size of alert amounts.</param>
/// <param name="logger">The logger that records saves by owner id, revision and user id only.</param>
[Scoped<CostAlertSettingsService, ICostAlertSettingsService>]
internal sealed class CostAlertSettingsService(
    DatabaseContext dbContext,
    IHostingAuthorizationService authorizationService,
    ICostAlertRecipientResolver recipientResolver,
    IEmailSender emailSender,
    IConfiguration configuration,
    IOptions<CostAlertOptions> options,
    ILogger<CostAlertSettingsService> logger) : ICostAlertSettingsService
{
    /// <inheritdoc />
    public async Task<CostAlertSettingsDto> GetAsync(
        Guid costOwnerId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var owner = await LoadOwnerAsync(costOwnerId, cancellationToken).ConfigureAwait(false);
        var canManage = await authorizationService.CanUseCostOwnerAsync(principal, costOwnerId, cancellationToken)
            .ConfigureAwait(false);
        if (!canManage
            && !await authorizationService.CanViewCostOwnerAsync(principal, costOwnerId, cancellationToken)
                .ConfigureAwait(false))
            throw new HostingForbiddenException("The cost owner may not be viewed.");
        return await BuildAsync(owner, canManage, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CostAlertSettingsDto?> GetPersonalAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!principal.TryGetUserId(out var userId))
            return null;
        var ownerId = await dbContext.CostOwners
            .AsNoTracking()
            .Where(q => q.UserFk == userId && q.Kind == ECostOwnerKind.User)
            .Select(q => (Guid?) q.PrimaryKey)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (ownerId is null)
            return null;
        return await GetAsync(ownerId.Value, principal, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CostAlertSettingsDto> UpdateAsync(
        Guid costOwnerId,
        CostAlertSettingsUpdate update,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(principal);
        var owner = await LoadOwnerAsync(costOwnerId, cancellationToken).ConfigureAwait(false);
        if (!await authorizationService.CanUseCostOwnerAsync(principal, costOwnerId, cancellationToken)
                .ConfigureAwait(false))
            throw new HostingForbiddenException("The cost owner's alert settings may not be changed.");

        var amounts = Validate(update, out var language);
        principal.TryGetUserId(out var userId);
        var revision = await SaveAsync(costOwnerId, update, amounts, language, userId, cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Cost alert settings of cost owner {CostOwnerId} saved as revision {Revision} by user {UserId}.",
            costOwnerId,
            revision,
            userId);
        // Reload the owner so the response carries the language just recorded; the caller was authorized to manage it.
        var reloaded = await LoadOwnerAsync(owner.PrimaryKey, cancellationToken).ConfigureAwait(false);
        return await BuildAsync(reloaded, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates an update against the configured limits.
    /// </summary>
    /// <param name="update">The update to validate.</param>
    /// <param name="language">The normalized language, or <see langword="null"/> when none was given.</param>
    /// <returns>The distinct amounts in ascending order.</returns>
    /// <exception cref="HostingValidationException">The update violates a rule; nothing may be stored.</exception>
    private decimal[] Validate(CostAlertSettingsUpdate update, out string? language)
    {
        var limits = options.Value;
        if (update.Thresholds is null)
            throw new HostingValidationException("The list of alert amounts is required.");
        if (update.Thresholds.Count > limits.MaxThresholds)
            throw new HostingValidationException($"At most {limits.MaxThresholds} alert amounts are allowed.");
        foreach (var amount in update.Thresholds)
        {
            if (amount <= 0m)
                throw new HostingValidationException("Every alert amount must be above zero.");
            if (amount > limits.MaxThresholdAmount)
            {
                throw new HostingValidationException(
                    $"An alert amount must not exceed {limits.MaxThresholdAmount}.");
            }

            if (decimal.Round(amount, 2) != amount)
                throw new HostingValidationException("An alert amount must not have more than two decimals.");
        }

        var amounts = update.Thresholds.Select(q => decimal.Round(q, 2)).Order().ToArray();
        if (amounts.Distinct().Count() != amounts.Length)
            throw new HostingValidationException("Alert amounts must not repeat.");

        language = null;
        if (update.Language is not null)
        {
            language = MessageLocales.Normalize(update.Language)
                       ?? throw new HostingValidationException("The language is not supported.");
        }

        return amounts;
    }

    /// <summary>
    /// Stores a validated update, and the saving user's language, in one transaction.
    /// </summary>
    /// <param name="costOwnerId">The cost owner whose setting is stored.</param>
    /// <param name="update">The update carrying the base revision and the channel flags.</param>
    /// <param name="amounts">The validated amounts, ascending and distinct.</param>
    /// <param name="language">The normalized language to record on the saving user, or <see langword="null"/>.</param>
    /// <param name="userId">The saving user, or <see cref="Guid.Empty"/> when the principal has no user id.</param>
    /// <param name="cancellationToken">A token that cancels the save.</param>
    /// <returns>The revision stored by the save.</returns>
    /// <exception cref="HostingConflictException">The revision is stale or a concurrent first save won.</exception>
    private async Task<int> SaveAsync(
        Guid costOwnerId,
        CostAlertSettingsUpdate update,
        decimal[] amounts,
        string? language,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var updatedBy = userId == Guid.Empty ? (Guid?) null : userId;
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var existing = await dbContext.CostAlertSettings
                .AsNoTracking()
                .Where(q => q.CostOwnerFk == costOwnerId)
                .Select(q => new { q.PrimaryKey, q.Revision })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                if (update.Revision != 0)
                    throw new HostingConflictException("The alert settings changed; reload them and try again.");
                var created = new CostAlertSetting
                {
                    PrimaryKey       = Guid.NewGuid(),
                    CostOwnerFk      = costOwnerId,
                    DiscordEnabled   = update.DiscordEnabled,
                    EmailEnabled     = update.EmailEnabled,
                    Revision         = 1,
                    UpdatedByFk      = updatedBy,
                    TimeStampCreated = now,
                    TimeStampUpdated = now,
                };
                foreach (var amount in amounts)
                {
                    created.Thresholds.Add(new CostAlertThreshold
                    {
                        PrimaryKey = Guid.NewGuid(),
                        SettingFk  = created.PrimaryKey,
                        Amount     = amount,
                    });
                }

                dbContext.CostAlertSettings.Add(created);
                await RecordLanguageAsync(updatedBy, language, cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return created.Revision;
            }

            if (update.Revision != existing.Revision)
                throw new HostingConflictException("The alert settings changed; reload them and try again.");
            // The conditional update claims the revision and takes the row lock, so a concurrent save waits and
            // then finds the revision it was based on gone.
            var claimed = await dbContext.CostAlertSettings
                .Where(q => q.PrimaryKey == existing.PrimaryKey && q.Revision == update.Revision)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(q => q.Revision, q => q.Revision + 1)
                        .SetProperty(q => q.DiscordEnabled, update.DiscordEnabled)
                        .SetProperty(q => q.EmailEnabled, update.EmailEnabled)
                        .SetProperty(q => q.UpdatedByFk, updatedBy)
                        .SetProperty(q => q.TimeStampUpdated, now),
                    cancellationToken)
                .ConfigureAwait(false);
            if (claimed == 0)
                throw new HostingConflictException("The alert settings changed; reload them and try again.");

            var stored = await dbContext.CostAlertThresholds
                .Where(q => q.SettingFk == existing.PrimaryKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            dbContext.CostAlertThresholds.RemoveRange(stored.Where(q => !amounts.Contains(q.Amount)));
            foreach (var amount in amounts.Where(q => stored.All(s => s.Amount != q)))
            {
                dbContext.CostAlertThresholds.Add(new CostAlertThreshold
                {
                    PrimaryKey = Guid.NewGuid(),
                    SettingFk  = existing.PrimaryKey,
                    Amount     = amount,
                });
            }

            await RecordLanguageAsync(updatedBy, language, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing.Revision + 1;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            throw new HostingConflictException("The alert settings changed; reload them and try again.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Records the language on the saving user when one was given.
    /// </summary>
    /// <param name="userId">The saving user, or <see langword="null"/> when the principal has no user id.</param>
    /// <param name="language">The normalized language, or <see langword="null"/> to change nothing.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>A task completing once the tracked user carries the language.</returns>
    private async Task RecordLanguageAsync(Guid? userId, string? language, CancellationToken cancellationToken)
    {
        if (language is null || userId is null)
            return;
        var user = await dbContext.Users
            .SingleOrDefaultAsync(q => q.PrimaryKey == userId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (user is not null)
            user.Locale = language;
    }

    /// <summary>
    /// Loads a cost owner with its user without tracking.
    /// </summary>
    /// <param name="costOwnerId">The cost owner to load.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>The cost owner with its user.</returns>
    /// <exception cref="HostingNotFoundException">The cost owner does not exist.</exception>
    private async Task<CostOwner> LoadOwnerAsync(Guid costOwnerId, CancellationToken cancellationToken)
        => await dbContext.CostOwners
               .AsNoTracking()
               .Include(q => q.User)
               .SingleOrDefaultAsync(q => q.PrimaryKey == costOwnerId, cancellationToken)
               .ConfigureAwait(false)
           ?? throw new HostingNotFoundException("The cost owner was not found.");

    /// <summary>
    /// Builds the settings contract of an already authorized cost owner.
    /// </summary>
    /// <param name="owner">The cost owner with its user.</param>
    /// <param name="canManage">Whether the viewer may save changes, as already evaluated by the caller.</param>
    /// <param name="cancellationToken">A token that cancels the lookups.</param>
    /// <returns>The settings contract.</returns>
    private async Task<CostAlertSettingsDto> BuildAsync(
        CostOwner owner,
        bool canManage,
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.CostAlertSettings
            .AsNoTracking()
            .Include(q => q.Thresholds)
            .SingleOrDefaultAsync(q => q.CostOwnerFk == owner.PrimaryKey, cancellationToken)
            .ConfigureAwait(false);
        var recipients = (await recipientResolver
                .ResolveAsync([owner.PrimaryKey], cancellationToken)
                .ConfigureAwait(false))
            .GetValueOrDefault(owner.PrimaryKey) ?? [];
        var discordEnabledOnInstallation = configuration[Constants.Configuration.Discord.Enabled]?.ToBoolean() ?? true;
        var isSelf = owner.Kind == ECostOwnerKind.User;
        var limits = options.Value;
        return new CostAlertSettingsDto
        {
            CostOwnerId        = owner.PrimaryKey,
            CostOwnerName      = owner.DisplayName,
            Revision           = setting?.Revision ?? 0,
            Thresholds         = (setting?.Thresholds.Select(q => q.Amount) ?? []).Order().ToArray(),
            DiscordEnabled     = setting?.DiscordEnabled ?? true,
            EmailEnabled       = setting?.EmailEnabled ?? true,
            Currency           = owner.Currency,
            MaxThresholds      = limits.MaxThresholds,
            MaxThresholdAmount = limits.MaxThresholdAmount,
            CanManage          = canManage,
            Audience           = isSelf ? ECostAlertAudience.Self : ECostAlertAudience.Group,
            Discord = !discordEnabledOnInstallation
                ? ECostAlertChannelAvailability.ChannelUnavailable
                : recipients.Any(q => q.HasDiscord)
                    ? ECostAlertChannelAvailability.Deliverable
                    : ECostAlertChannelAvailability.NotLinked,
            Email = !emailSender.IsConfigured
                ? ECostAlertChannelAvailability.ChannelUnavailable
                : recipients.Any(q => q.HasEmail)
                    ? ECostAlertChannelAvailability.Deliverable
                    : ECostAlertChannelAvailability.NoAddress,
            MessageLanguage = isSelf ? MessageLocales.Normalize(owner.User?.Locale) ?? MessageLocales.English : null,
        };
    }
}
