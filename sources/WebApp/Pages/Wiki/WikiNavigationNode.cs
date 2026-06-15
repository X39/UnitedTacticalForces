using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;

namespace X39.UnitedTacticalForces.WebApp.Pages.Wiki;

public sealed class WikiNavigationNode(string name, bool isRoot = false)
{
    public string Name { get; } = name;

    public bool IsRoot { get; } = isRoot;

    public List<WikiNavigationNode> Children { get; } = [];

    public List<WikiPageHeader> Pages { get; } = [];
}
