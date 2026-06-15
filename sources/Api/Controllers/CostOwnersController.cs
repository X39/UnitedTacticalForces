using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Database.Hosting;
using X39.UnitedTacticalForces.Api.DTO;
using X39.UnitedTacticalForces.Api.DTO.Payloads;
using X39.UnitedTacticalForces.Api.Services.GameServerLifecycle;

namespace X39.UnitedTacticalForces.Api.Controllers;

/// <summary>
/// Claim-controlled operational finance mutations for cloud-hosting cost owners.
/// </summary>
[ApiController]
[Authorize]
[Route(Constants.Routes.CostOwners)]
public sealed class CostOwnersController(IHostingFinanceService financeService) : ControllerBase
{
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
