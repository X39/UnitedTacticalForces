namespace X39.UnitedTacticalForces.WebApp.Pages;

public partial class Wiki
{
    public record WikiCategory(string Name, List<WikiPageHeader> Pages, List<WikiCategory> SubCategories);
}