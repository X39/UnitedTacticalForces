using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
using X39.UnitedTacticalForces.Database.Authority;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Claim-controlled operational finance mutations for cloud-hosting cost owners.
/// </summary>
[ApiController]
[Authorize]
[Route(Constants.Routes.CostOwners)]
public sealed class CostOwnersController(
    DatabaseContext dbContext,
    IHostingFinanceService financeService,
    IHostingFinancePolicyService financePolicyService,
    IHostingAuthorizationService authorizationService,
    IHostingAuditWriter auditWriter) : ControllerBase
{
    /// <summary>
    /// Lists a filtered page of cost owners and their current hosting-finance positions.
    /// </summary>
    [HttpGet]
    [Authorize(Claims.Finance.ViewAll)]
    [ProducesResponseType<HostingCostOwnerPageDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] string? search,
        [FromQuery] ECostOwnerKind? kind,
        [FromQuery] bool? isFrozen,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 50);
        var query = dbContext.CostOwners.Include(q => q.User).AsNoTracking();
        if (kind.HasValue)
            query = query.Where(q => q.Kind == kind.Value);
        if (isFrozen.HasValue)
            query = query.Where(q => q.IsFrozen == isFrozen.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(q => (q.User != null ? q.User.Nickname : q.DisplayName)
                .ToLower().Contains(normalizedSearch.ToLower()));
        }

        var totalCount = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var owners = await query
            .AsNoTracking()
            .OrderBy(q => q.DisplayName)
            .ThenBy(q => q.PrimaryKey)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = new List<HostingCostOwnerDto>(owners.Length);
        foreach (var owner in owners)
        {
            result.Add(await ToCostOwnerDtoAsync(owner, cancellationToken).ConfigureAwait(false));
        }
        return Ok(new HostingCostOwnerPageDto { Owners = result.ToArray(), TotalCount = totalCount });
    }

    /// <summary>
    /// Returns one cost owner's current hosting-finance position when the caller may inspect it.
    /// </summary>
    [HttpGet("{costOwnerId:guid}")]
    [ProducesResponseType<HostingCostOwnerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid costOwnerId, CancellationToken cancellationToken)
    {
        var owner = await dbContext.CostOwners
            .Include(q => q.User)
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.PrimaryKey == costOwnerId, cancellationToken)
            .ConfigureAwait(false);
        if (owner is null)
            return NotFound();
        if (!await authorizationService.CanViewCostOwnerAsync(User, owner.PrimaryKey, cancellationToken)
                .ConfigureAwait(false))
            return Forbid();
        return Ok(await ToCostOwnerDtoAsync(owner, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Returns the hosting-finance position belonging to one user.
    /// </summary>
    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType<HostingCostOwnerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var owner = await dbContext.CostOwners
            .Include(q => q.User)
            .AsNoTracking()
            .SingleOrDefaultAsync(q => q.UserFk == userId, cancellationToken)
            .ConfigureAwait(false);
        if (owner is null)
            return NotFound();
        if (!await authorizationService.CanViewCostOwnerAsync(User, owner.PrimaryKey, cancellationToken)
                .ConfigureAwait(false))
            return Forbid();
        return Ok(await ToCostOwnerDtoAsync(owner, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lists active users who do not yet have a hosting-finance cost owner.
    /// </summary>
    [HttpGet("user-candidates")]
    [Authorize(Claims.Finance.ManageCostOwners)]
    [ProducesResponseType<HostingCostOwnerUserCandidatePageDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserCandidatesAsync(
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 50);
        var query = dbContext.Users
            .AsNoTracking()
            .Where(q => !q.IsDeleted && !dbContext.CostOwners.Any(owner => owner.UserFk == q.PrimaryKey));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(q => q.Nickname.ToLower().Contains(normalizedSearch));
        }

        var totalCount = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var users = await query
            .OrderBy(q => q.Nickname)
            .ThenBy(q => q.PrimaryKey)
            .Skip(skip)
            .Take(take)
            .Select(q => new HostingCostOwnerUserCandidateDto
            {
                UserId = q.PrimaryKey,
                DisplayName = q.Nickname,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(new HostingCostOwnerUserCandidatePageDto { Users = users, TotalCount = totalCount });
    }

    /// <summary>
    /// Creates a user, Verein, or external-organization cost owner.
    /// </summary>
    [HttpPost]
    [Authorize(Claims.Finance.ManageCostOwners)]
    [ProducesResponseType<HostingCostOwnerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CostOwnerCreationPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();

        User? representedUser = null;
        string displayName;
        switch (payload.Kind)
        {
            case ECostOwnerKind.User:
                if (!payload.UserId.HasValue || !string.IsNullOrWhiteSpace(payload.DisplayName))
                    return BadRequest(new { error = "A user cost owner requires only a user identifier." });
                representedUser = await dbContext.Users
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        q => q.PrimaryKey == payload.UserId.Value && !q.IsDeleted,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (representedUser is null)
                    return NotFound(new { error = "The selected active user does not exist." });
                if (await dbContext.CostOwners.AnyAsync(q => q.UserFk == representedUser.PrimaryKey, cancellationToken)
                        .ConfigureAwait(false))
                    return Conflict(new { error = "The selected user already has a cost owner." });
                displayName = representedUser.Nickname;
                break;
            case ECostOwnerKind.Verein:
                if (payload.UserId.HasValue || !string.IsNullOrWhiteSpace(payload.DisplayName))
                    return BadRequest(new { error = "The Verein cost owner does not accept a user or custom name." });
                if (await dbContext.CostOwners.AnyAsync(q => q.Kind == ECostOwnerKind.Verein, cancellationToken)
                        .ConfigureAwait(false))
                    return Conflict(new { error = "The Verein cost owner already exists." });
                displayName = "Verein";
                break;
            case ECostOwnerKind.ExternalOrganization:
                if (payload.UserId.HasValue)
                    return BadRequest(new { error = "An external organization cannot reference an app user." });
                displayName = payload.DisplayName?.Trim() ?? string.Empty;
                if (displayName.Length is < 1 or > 256)
                    return BadRequest(new { error = "An external organization name must contain 1 to 256 characters." });
                break;
            default:
                return BadRequest(new { error = "The cost-owner kind is invalid." });
        }

        var now = NodaSystemClock.Instance.GetCurrentInstant();
        var owner = new CostOwner
        {
            PrimaryKey = Guid.NewGuid(),
            Kind = payload.Kind,
            UserFk = representedUser?.PrimaryKey,
            DisplayName = displayName,
            Currency = "EUR",
            TimeStampCreated = now,
            TimeStampUpdated = now,
        };
        dbContext.CostOwners.Add(owner);
        auditWriter.Add(
            dbContext,
            EHostingAuditEventKind.CostOwnerCreated,
            "Hosting-finance cost owner created.",
            actorId: actorId,
            costOwnerId: owner.PrimaryKey,
            details: $"Kind={owner.Kind}; User={owner.UserFk}; DisplayName={owner.DisplayName}");
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "A matching cost owner was created concurrently." });
        }

        return CreatedAtAction(nameof(GetAsync), new { costOwnerId = owner.PrimaryKey },
            await ToCostOwnerDtoAsync(owner, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Renames an external-organization cost owner.
    /// </summary>
    [HttpPut("{costOwnerId:guid}/display-name")]
    [Authorize(Claims.Finance.ManageCostOwners)]
    [ProducesResponseType<HostingCostOwnerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenameAsync(
        Guid costOwnerId,
        [FromBody] CostOwnerDisplayNameUpdatePayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        var owner = await dbContext.CostOwners
            .SingleOrDefaultAsync(q => q.PrimaryKey == costOwnerId, cancellationToken)
            .ConfigureAwait(false);
        if (owner is null)
            return NotFound();
        if (owner.Kind != ECostOwnerKind.ExternalOrganization)
            return BadRequest(new { error = "Only external-organization cost owners can be renamed." });
        var displayName = payload.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is < 1 or > 256)
            return BadRequest(new { error = "An external organization name must contain 1 to 256 characters." });
        if (!string.Equals(owner.DisplayName, displayName, StringComparison.Ordinal))
        {
            var previousName = owner.DisplayName;
            owner.DisplayName = displayName;
            owner.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            auditWriter.Add(
                dbContext,
                EHostingAuditEventKind.CostOwnerRenamed,
                "External-organization cost owner renamed.",
                actorId: actorId,
                costOwnerId: owner.PrimaryKey,
                details: $"Previous={previousName}; Current={displayName}");
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return Ok(await ToCostOwnerDtoAsync(owner, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lists the append-only finance protocol for one cost owner.
    /// </summary>
    [HttpGet("{costOwnerId:guid}/ledger-entries")]
    [Authorize(Claims.Finance.ViewAll)]
    [ProducesResponseType<HostingFinanceProtocolPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProtocolAsync(
        Guid costOwnerId,
        [FromQuery] string? search,
        [FromQuery] EHostingLedgerEntryKind? kind,
        [FromQuery] Instant? from,
        [FromQuery] Instant? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.CostOwners.AnyAsync(q => q.PrimaryKey == costOwnerId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();
        return Ok(await QueryProtocolAsync(
                costOwnerId,
                search,
                kind,
                from,
                to,
                skip,
                take,
                cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>
    /// Lists the installation-wide append-only hosting-finance protocol.
    /// </summary>
    [HttpGet("ledger-entries")]
    [Authorize(Claims.Finance.ViewAll)]
    [ProducesResponseType<HostingFinanceProtocolPageDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGlobalProtocolAsync(
        [FromQuery] Guid? costOwnerId,
        [FromQuery] string? search,
        [FromQuery] EHostingLedgerEntryKind? kind,
        [FromQuery] Instant? from,
        [FromQuery] Instant? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => Ok(await QueryProtocolAsync(costOwnerId, search, kind, from, to, skip, take, cancellationToken)
            .ConfigureAwait(false));

    /// <summary>
    /// Lists completed deployments whose remaining provider cost may be corrected to another owner.
    /// </summary>
    [HttpGet("{costOwnerId:guid}/correctable-deployments")]
    [Authorize(Claims.Finance.CorrectLedger)]
    [ProducesResponseType<HostingCorrectableDeploymentDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCorrectableDeploymentsAsync(
        Guid costOwnerId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.CostOwners.AnyAsync(q => q.PrimaryKey == costOwnerId, cancellationToken)
                .ConfigureAwait(false))
            return NotFound();
        var deployments = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Include(q => q.GameServer)
            .Include(q => q.CostLedgerEntries)
            .Where(q => q.CostOwnerFk == costOwnerId && q.TimeStampResolved != null && !q.PossibleCost)
            .OrderByDescending(q => q.TimeStampResolved)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = deployments
            .Select(deployment => new
            {
                Deployment = deployment,
                Remaining = deployment.CostLedgerEntries
                    .Where(q => q.Kind == EHostingLedgerEntryKind.ProviderCostSlice
                                || q.Kind == EHostingLedgerEntryKind.DeploymentCostCorrectionCredit
                                || q.Kind == EHostingLedgerEntryKind.LedgerReversal)
                    .Sum(q => -q.Amount),
            })
            .Where(q => q.Remaining > 0)
            .Select(q => new HostingCorrectableDeploymentDto
            {
                DeploymentId = q.Deployment.PrimaryKey,
                ServerId = q.Deployment.GameServerFk,
                ServerTitle = q.Deployment.GameServer?.Title ?? q.Deployment.GameServerFk.ToString(),
                RemainingCorrectableAmount = q.Remaining,
                Currency = "EUR",
                TimeStampResolved = q.Deployment.TimeStampResolved!.Value,
            })
            .ToArray();
        return Ok(result);
    }

    /// <summary>
    /// Freezes or unfreezes a cost owner for future spending.
    /// </summary>
    [HttpPut("{costOwnerId:guid}/frozen")]
    [Authorize(Claims.Finance.FreezeCostOwner)]
    [ProducesResponseType<HostingCostOwnerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetFrozenAsync(
        Guid costOwnerId,
        [FromBody] CostOwnerFreezeUpdatePayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        var owner = await dbContext.CostOwners
            .Include(q => q.User)
            .SingleOrDefaultAsync(q => q.PrimaryKey == costOwnerId, cancellationToken)
            .ConfigureAwait(false);
        if (owner is null)
            return NotFound();
        if (owner.IsFrozen != payload.IsFrozen)
        {
            owner.IsFrozen = payload.IsFrozen;
            owner.TimeStampUpdated = NodaSystemClock.Instance.GetCurrentInstant();
            auditWriter.Add(
                dbContext,
                payload.IsFrozen ? EHostingAuditEventKind.CostOwnerFrozen : EHostingAuditEventKind.CostOwnerUnfrozen,
                payload.IsFrozen ? "Hosting-finance cost owner frozen." : "Hosting-finance cost owner unfrozen.",
                actorId: actorId,
                costOwnerId: owner.PrimaryKey);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return Ok(await ToCostOwnerDtoAsync(owner, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Creates the hosting-finance position for a user before their first server is created.
    /// </summary>
    [HttpPost("users/{userId:guid}")]
    [ProducesResponseType<HostingCostOwnerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnsureForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!CanInitializeCostOwner())
            return Forbid();

        var owner = await dbContext.CostOwners
            .SingleOrDefaultAsync(q => q.UserFk == userId, cancellationToken)
            .ConfigureAwait(false);
        if (owner is null)
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(q => q.PrimaryKey == userId && !q.IsDeleted, cancellationToken)
                .ConfigureAwait(false);
            if (user is null)
                return NotFound();
            var now = NodaSystemClock.Instance.GetCurrentInstant();
            owner = new CostOwner
            {
                PrimaryKey = Guid.NewGuid(),
                Kind = ECostOwnerKind.User,
                UserFk = user.PrimaryKey,
                DisplayName = user.Nickname,
                Currency = "EUR",
                TimeStampCreated = now,
                TimeStampUpdated = now,
            };
            dbContext.CostOwners.Add(owner);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return Ok(await ToCostOwnerDtoAsync(owner, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Returns the effective installation-wide affordability policy.
    /// </summary>
    [HttpGet("policy")]
    [Authorize(Claims.Finance.ViewAll)]
    [ProducesResponseType<HostingFinancePolicyDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicyAsync(CancellationToken cancellationToken)
        => Ok(ToDto(await financePolicyService.GetAsync(cancellationToken).ConfigureAwait(false)));

    /// <summary>
    /// Updates the persisted installation-wide affordability policy.
    /// </summary>
    [HttpPut("policy")]
    [Authorize(Claims.Finance.ManagePolicy)]
    [ProducesResponseType<HostingFinancePolicyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePolicyAsync(
        [FromBody] HostingFinancePolicyUpdatePayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        try
        {
            return Ok(ToDto(await financePolicyService.UpdateAsync(
                    payload.MinimumSafetyRangeEur,
                    payload.ReservationSliceMinutes,
                    actorId,
                    cancellationToken)
                .ConfigureAwait(false)));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Records a bank transfer already received by the Verein and credits the selected cost owner.
    /// </summary>
    [HttpPost("{costOwnerId:guid}/payments")]
    [Authorize(Claims.Finance.RecordPayment)]
    [ProducesResponseType<HostingLedgerEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordPaymentAsync(
        [FromRoute] Guid costOwnerId,
        [FromBody] CostOwnerPaymentPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!payload.TimeStampEffective.HasValue)
            return BadRequest(new { error = "An effective payment time is required." });
        try
        {
            var entry = await financeService.RecordBankTransferPaymentAsync(
                    costOwnerId,
                    payload.RecordIdentifier,
                    payload.Amount,
                    payload.Currency,
                    payload.TimeStampEffective.Value,
                    payload.ExternalReference,
                    payload.Reason,
                    actorId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(ToDto(entry));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Grants, changes, or revokes a finite allowance without changing the cost owner's prepaid balance.
    /// </summary>
    [HttpPut("{costOwnerId:guid}/allowance")]
    [Authorize(Claims.Finance.ManageAllowance)]
    [ProducesResponseType<HostingLedgerEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeAllowanceAsync(
        [FromRoute] Guid costOwnerId,
        [FromBody] CostOwnerAllowancePayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        try
        {
            var entry = await financeService.ChangeAllowanceAsync(
                    costOwnerId,
                    payload.RecordIdentifier,
                    payload.Allowance,
                    payload.Reason,
                    actorId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(ToDto(entry));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Records a refund or withdrawal after the outgoing bank transfer has completed externally.
    /// </summary>
    [HttpPost("{costOwnerId:guid}/refunds")]
    [Authorize(Claims.Finance.RecordRefund)]
    [ProducesResponseType<HostingLedgerEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordRefundAsync(
        [FromRoute] Guid costOwnerId,
        [FromBody] CostOwnerRefundPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!payload.TimeStampEffective.HasValue)
            return BadRequest(new { error = "An effective refund time is required." });
        try
        {
            var entry = await financeService.RecordExternalRefundAsync(
                    costOwnerId,
                    payload.RecordIdentifier,
                    payload.Amount,
                    payload.Currency,
                    payload.TimeStampEffective.Value,
                    payload.ExternalReference,
                    payload.Reason,
                    actorId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(ToDto(entry));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Corrects provider cost on a fully resolved deployment from its immutable original owner to the correct owner.
    /// </summary>
    [HttpPost("{costOwnerId:guid}/deployments/{deploymentId:guid}/corrections")]
    [Authorize(Claims.Finance.CorrectLedger)]
    [ProducesResponseType<HostingFinanceOperationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CorrectDeploymentCostOwnerAsync(
        [FromRoute] Guid costOwnerId,
        [FromRoute] Guid deploymentId,
        [FromBody] DeploymentCostOwnerCorrectionPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!payload.TimeStampEffective.HasValue)
            return BadRequest(new { error = "An effective correction time is required." });
        try
        {
            var operation = await financeService.CorrectCompletedDeploymentCostOwnerAsync(
                    costOwnerId,
                    deploymentId,
                    payload.OperationIdentifier,
                    payload.CorrectCostOwnerId,
                    payload.Amount,
                    payload.Currency,
                    payload.TimeStampEffective.Value,
                    payload.Reason,
                    actorId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(ToDto(operation));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    /// <summary>
    /// Appends a linked reversal for a mistaken payment, refund, correction, or prior reversal.
    /// </summary>
    [HttpPost("{costOwnerId:guid}/ledger-entries/{ledgerEntryId:guid}/reversals")]
    [Authorize(Claims.Finance.CorrectLedger)]
    [ProducesResponseType<HostingFinanceOperationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReverseLedgerEntryAsync(
        [FromRoute] Guid costOwnerId,
        [FromRoute] Guid ledgerEntryId,
        [FromBody] HostingLedgerReversalPayload payload,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var actorId))
            return Unauthorized();
        if (!payload.TimeStampEffective.HasValue)
            return BadRequest(new { error = "An effective reversal time is required." });
        try
        {
            var operation = await financeService.ReverseLedgerEntryAsync(
                    costOwnerId,
                    ledgerEntryId,
                    payload.OperationIdentifier,
                    payload.TimeStampEffective.Value,
                    payload.Reason,
                    actorId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(ToDto(operation));
        }
        catch (Exception ex) when (TryMapException(ex, out var result))
        {
            return result;
        }
    }

    private static HostingLedgerEntryDto ToDto(HostingCostLedgerEntry entry)
        => new(
            entry.PrimaryKey,
            entry.CostOwnerFk,
            entry.DeploymentFk,
            entry.Kind,
            entry.Amount,
            entry.BalanceAfter,
            entry.PreviousAllowance,
            entry.NewAllowance,
            entry.Currency,
            entry.TimeStampEffective,
            entry.TimeStampCreated,
            entry.RecordedByFk,
            entry.RecordIdentifier,
            entry.FinanceOperationFk,
            entry.ReversedEntryFk,
            entry.ExternalReference,
            entry.Reason);

    private async Task<HostingFinanceProtocolPageDto> QueryProtocolAsync(
        Guid? costOwnerId,
        string? search,
        EHostingLedgerEntryKind? kind,
        Instant? from,
        Instant? to,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 100);
        var query = dbContext.HostingCostLedgerEntries.AsNoTracking();
        if (costOwnerId.HasValue)
            query = query.Where(q => q.CostOwnerFk == costOwnerId.Value);
        if (kind.HasValue)
            query = query.Where(q => q.Kind == kind.Value);
        if (from.HasValue)
            query = query.Where(q => q.TimeStampEffective >= from.Value);
        if (to.HasValue)
            query = query.Where(q => q.TimeStampEffective < to.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLower();
            var identifier = Guid.TryParse(normalized, out var parsedIdentifier) ? parsedIdentifier : (Guid?)null;
            query = query.Where(q =>
                (q.ExternalReference != null && q.ExternalReference.ToLower().Contains(normalized))
                || (q.Reason != null && q.Reason.ToLower().Contains(normalized))
                || (q.ProviderIdentifier != null && q.ProviderIdentifier.ToLower().Contains(normalized))
                || (q.CostOwner != null
                    && (q.CostOwner.User != null ? q.CostOwner.User.Nickname : q.CostOwner.DisplayName)
                    .ToLower().Contains(normalized))
                || (q.Deployment != null && q.Deployment.GameServer != null
                    && q.Deployment.GameServer.Title.ToLower().Contains(normalized))
                || (identifier.HasValue
                    && (q.PrimaryKey == identifier.Value
                        || q.CostOwnerFk == identifier.Value
                        || q.DeploymentFk == identifier.Value
                        || q.RecordIdentifier == identifier.Value
                        || q.FinanceOperationFk == identifier.Value)));
        }

        var totalCount = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var entries = await query
            .Include(q => q.CostOwner)
            .ThenInclude(q => q!.User)
            .Include(q => q.Deployment)
            .ThenInclude(q => q!.GameServer)
            .Include(q => q.RecordedBy)
            .Include(q => q.ReversalEntries)
            .Include(q => q.FinanceOperation)
            .ThenInclude(q => q!.SourceCostOwner)
            .ThenInclude(q => q!.User)
            .Include(q => q.FinanceOperation)
            .ThenInclude(q => q!.DestinationCostOwner)
            .ThenInclude(q => q!.User)
            .AsSplitQuery()
            .OrderByDescending(q => q.TimeStampEffective)
            .ThenByDescending(q => q.TimeStampCreated)
            .ThenByDescending(q => q.PrimaryKey)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new HostingFinanceProtocolPageDto
        {
            TotalCount = totalCount,
            Entries = entries.Select(ToProtocolDto).ToArray(),
        };
    }

    private static HostingFinanceProtocolEntryDto ToProtocolDto(HostingCostLedgerEntry entry)
    {
        var reversalEntry = entry.ReversalEntries
            .OrderBy(q => q.TimeStampCreated)
            .ThenBy(q => q.PrimaryKey)
            .FirstOrDefault();
        var counterparty = entry.FinanceOperation is not { } operation
            ? null
            : operation.SourceCostOwnerFk == entry.CostOwnerFk
                ? operation.DestinationCostOwner
                : operation.SourceCostOwner;
        return new HostingFinanceProtocolEntryDto
        {
            Id = entry.PrimaryKey,
            CostOwnerId = entry.CostOwnerFk,
            CostOwnerDisplayName = entry.CostOwner is null
                ? entry.CostOwnerFk.ToString()
                : GetOwnerDisplayName(entry.CostOwner),
            CostOwnerKind = entry.CostOwner?.Kind ?? ECostOwnerKind.ExternalOrganization,
            DeploymentId = entry.DeploymentFk,
            ServerId = entry.Deployment?.GameServerFk,
            ServerTitle = entry.Deployment?.GameServer?.Title,
            Kind = entry.Kind,
            Amount = entry.Amount,
            BalanceAfter = entry.BalanceAfter,
            Currency = entry.Currency,
            TimeStampEffective = entry.TimeStampEffective,
            TimeStampRecorded = entry.TimeStampCreated,
            RecordedById = entry.RecordedByFk,
            RecordedByDisplayName = entry.RecordedBy?.Nickname,
            RecordIdentifier = entry.RecordIdentifier,
            FinanceOperationId = entry.FinanceOperationFk,
            ReversedEntryId = entry.ReversedEntryFk,
            ReversalEntryId = reversalEntry?.PrimaryKey,
            IsReversible = entry.Kind is not EHostingLedgerEntryKind.ProviderCostSlice
                and not EHostingLedgerEntryKind.AllowanceChanged
                && reversalEntry is null,
            ExternalReference = entry.ExternalReference,
            Reason = entry.Reason,
            ProviderIdentifier = entry.ProviderIdentifier,
            EstimatedHourlyRate = entry.EstimatedHourlyRate,
            SliceStartsAt = entry.SliceStartsAt,
            SliceEndsAt = entry.SliceEndsAt,
            PreviousAllowance = entry.PreviousAllowance,
            NewAllowance = entry.NewAllowance,
            CounterpartyCostOwnerId = counterparty?.PrimaryKey,
            CounterpartyDisplayName = counterparty is null ? null : GetOwnerDisplayName(counterparty),
        };
    }

    private async Task<HostingCostOwnerDto> ToCostOwnerDtoAsync(
        CostOwner owner,
        CancellationToken cancellationToken)
    {
        var reservations = await dbContext.GameServerDeployments
            .AsNoTracking()
            .Where(q => q.CostOwnerFk == owner.PrimaryKey
                        && q.TimeStampResolved == null
                        && q.ReservedProviderCostEur != null)
            .Select(q => q.ReservedProviderCostEur!.Value)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var reserved = reservations.Sum();
        var ledger = await dbContext.HostingCostLedgerEntries
            .AsNoTracking()
            .Where(q => q.CostOwnerFk == owner.PrimaryKey)
            .OrderByDescending(q => q.TimeStampCreated)
            .Take(20)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new HostingCostOwnerDto(
            owner.PrimaryKey,
            owner.Kind,
            owner.UserFk,
            GetOwnerDisplayName(owner),
            owner.IsFrozen,
            owner.Balance,
            owner.Allowance,
            reserved,
            owner.Balance + (owner.Allowance ?? 0m) - reserved,
            owner.Currency,
            owner.TimeStampUpdated,
            ledger.Select(ToDto).ToArray());
    }

    private static string GetOwnerDisplayName(CostOwner owner)
        => owner.Kind == ECostOwnerKind.User && owner.User is not null
            ? owner.User.Nickname
            : owner.DisplayName;

    private static HostingFinancePolicyDto ToDto(EffectiveHostingFinancePolicy policy)
        => new(
            policy.MinimumSafetyRangeEur,
            (int)policy.ReservationSlice.TotalMinutes,
            policy.TimeStampUpdated,
            policy.UpdatedById);

    private bool CanInitializeCostOwner()
        => User.HasAnyEmptyClaim(
            Claims.Administrative.All,
            Claims.Administrative.Hosting,
            Claims.Administrative.Server,
            Claims.Finance.All,
            Claims.Finance.RecordPayment,
            Claims.Finance.ManageAllowance);

    private static HostingFinanceOperationDto ToDto(HostingFinanceOperation operation)
        => new(
            operation.PrimaryKey,
            operation.Kind,
            operation.DeploymentFk,
            operation.SourceCostOwnerFk,
            operation.DestinationCostOwnerFk,
            operation.TargetLedgerEntryFk,
            operation.TargetFinanceOperationFk,
            operation.Amount,
            operation.Currency,
            operation.TimeStampEffective,
            operation.TimeStampCreated,
            operation.RecordedByFk,
            operation.Reason,
            operation.AffectedLedgerEntries
                .OrderBy(q => q.AffectedLedgerEntryFk)
                .Select(q => new HostingFinanceAffectedLedgerEntryDto(q.AffectedLedgerEntryFk, q.Amount))
                .ToArray(),
            operation.Entries.OrderBy(q => q.PrimaryKey).Select(ToDto).ToArray());

    private static bool TryMapException(Exception exception, out IActionResult result)
    {
        result = exception switch
        {
            HostingNotFoundException => new NotFoundObjectResult(new { error = exception.Message }),
            HostingValidationException => new BadRequestObjectResult(new { error = exception.Message }),
            HostingConflictException => new ConflictObjectResult(new { error = exception.Message }),
            DbUpdateException => new ConflictObjectResult(new
            {
                error = "The finance position changed concurrently; retry with the same record or operation identifier.",
            }),
            _ => null!,
        };
        return result is not null;
    }
}
