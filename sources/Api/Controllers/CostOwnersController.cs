using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;
using X39.UnitedTacticalForces.Database;
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
    IHostingAuthorizationService authorizationService) : ControllerBase
{
    /// <summary>
    /// Lists every cost owner and their current hosting-finance position.
    /// </summary>
    [HttpGet]
    [Authorize(Claims.Finance.ViewAll)]
    [ProducesResponseType<IReadOnlyList<HostingCostOwnerDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var owners = await dbContext.CostOwners
            .AsNoTracking()
            .OrderBy(q => q.DisplayName)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = new List<HostingCostOwnerDto>(owners.Length);
        foreach (var owner in owners)
        {
            result.Add(await ToCostOwnerDtoAsync(owner, cancellationToken).ConfigureAwait(false));
        }
        return Ok(result);
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
            owner.DisplayName,
            owner.IsFrozen,
            owner.Balance,
            owner.Allowance,
            reserved,
            owner.Balance + (owner.Allowance ?? 0m) - reserved,
            owner.Currency,
            owner.TimeStampUpdated,
            ledger.Select(ToDto).ToArray());
    }

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
