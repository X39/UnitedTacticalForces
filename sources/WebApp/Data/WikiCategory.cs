using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Data;

public record WikiCategory(string Name, List<WikiPageHeader> Pages, List<WikiCategory> SubCategories);
