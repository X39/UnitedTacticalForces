using System.Security.Claims;

namespace X39.UnitedTacticalForces.Common;


public static class ClaimsPrincipalExtensions
{
    public static bool IsInRole(this ClaimsPrincipal self, string role, params string[] otherRoles)
        => self.IsInRole(role) || otherRoles.Any(self.IsInRole);

    public static bool IsAdmin(this ClaimsPrincipal self)
        => self.IsInRole(Roles.Admin);

    public static bool IsInRoleOrAdmin(this ClaimsPrincipal self, string role)
        => self.IsInRole(Roles.Admin) || self.IsInRole(role);

    public static bool IsInRoleOrAdmin(this ClaimsPrincipal self, string role1, string role2)
        => self.IsInRole(Roles.Admin) || self.IsInRole(role1) || self.IsInRole(role2);

    public static bool IsInRoleOrAdmin(this ClaimsPrincipal self, string role1, string role2, params string[] otherRoles)
        => self.IsInRole(Roles.Admin) || self.IsInRole(role1) || self.IsInRole(role2) || otherRoles.Any(self.IsInRole);
}