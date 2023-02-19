using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.Data.Eventing;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ModPackController : ControllerBase
{
    private readonly ILogger<ModPackController> _logger;
    private readonly ApiDbContext _apiDbContext;

    public ModPackController(ILogger<ModPackController> logger, ApiDbContext apiDbContext)
    {
        _logger = logger;
        _apiDbContext = apiDbContext;
    }

    [HttpPost("create", Name = nameof(CreateModPackAsync))]
    public async Task<ModPack> CreateModPackAsync([FromBody] ModPack modPack, CancellationToken cancellationToken)
    {
        var entity = await _apiDbContext.ModPacks.AddAsync(modPack, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return entity.Entity;
    }

    [HttpPost("{modPackId:long}/update", Name = nameof(UpdateModPackAsync))]
    public async Task UpdateModPackAsync(
        [FromRoute] long modPackId,
        [FromBody] ModPack updatedModPack,
        CancellationToken cancellationToken)
    {
        var existingModPack = await _apiDbContext.ModPacks.SingleAsync((q) => q.Id == modPackId, cancellationToken);
        existingModPack.Title = updatedModPack.Title;
        existingModPack.Xml = updatedModPack.Xml;
        existingModPack.TimeStampUpdated = DateTimeOffset.Now;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }
}