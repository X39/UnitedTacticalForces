using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace X39.BI.Config.Parsing;

public class ConfigClass : IConfig
{
    public string Identifier { get; set; } = string.Empty;
    public IReadOnlyCollection<IConfig> Children { get; set; } = ArraySegment<IConfig>.Empty;
    public string Extends { get; set; } = string.Empty;
}