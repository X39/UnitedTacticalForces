using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace X39.UnitedTacticalForces.Api.Data.Authority.Extern;

[ComplexType]
[Owned]
[Index(nameof(Id64))]
public class SteamUser
{
    public ulong Id64 { get; set; }
}