using X39.UnitedTacticalForces.WebApp.Api.Models;

namespace X39.UnitedTacticalForces.WebApp.Data;

public record WikiCategory(string Name, List<WikiPageHeader> Pages, List<WikiCategory> SubCategories);
