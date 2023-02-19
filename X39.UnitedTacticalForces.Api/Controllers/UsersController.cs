using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X39.UnitedTacticalForces.Api.Data;
using X39.UnitedTacticalForces.Api.Data.Authority;
using X39.UnitedTacticalForces.Api.Data.Core;
using X39.UnitedTacticalForces.Api.Data.Eventing;

namespace X39.UnitedTacticalForces.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;
    private readonly ApiDbContext _apiDbContext;

    public UsersController(ILogger<UsersController> logger, ApiDbContext apiDbContext)
    {
        _logger = logger;
        _apiDbContext = apiDbContext;
    }

    [Authorize(AuthenticationSchemes = Constants.AuthorizationSchemas.Steam)]
    [HttpPost("register", Name = nameof(RegisterUserAsync))]
    public async Task<User> RegisterUserAsync([FromBody] User user, CancellationToken cancellationToken)
    {
        HttpContext.User.HasClaim("test", "test");
        var entity = await _apiDbContext.Users.AddAsync(user, cancellationToken);
        await _apiDbContext.SaveChangesAsync(cancellationToken);
        return entity.Entity;
    }


    [HttpPost("{userId:guid}/update", Name = nameof(UpdateUserAsync))]
    public async Task UpdateUserAsync(
        [FromRoute] Guid userId,
        [FromBody] User updatedUser,
        CancellationToken cancellationToken)
    {
        var existingUser = await _apiDbContext.Users.SingleAsync((q) => q.Id == userId, cancellationToken);
        existingUser.Avatar = updatedUser.Avatar;
        existingUser.AvatarMimeType = updatedUser.AvatarMimeType;
        existingUser.Nickname = updatedUser.Nickname;
        existingUser.EMail = updatedUser.EMail;
        existingUser.SteamUuid = updatedUser.SteamUuid;
        await _apiDbContext.SaveChangesAsync(cancellationToken);
    }
}