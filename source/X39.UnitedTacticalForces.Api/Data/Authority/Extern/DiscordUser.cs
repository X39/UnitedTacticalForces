using System.ComponentModel.DataAnnotations.Schema;

namespace X39.UnitedTacticalForces.Api.Data.Authority.Extern;

[ComplexType]
[Microsoft.EntityFrameworkCore.Owned]
public class DiscordUser
{
    public ulong Id { get; set; }
    public string Username { get; set; }
}