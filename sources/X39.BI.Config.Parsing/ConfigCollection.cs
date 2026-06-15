namespace X39.BI.Config.Parsing;

public sealed class ConfigCollection : List<IConfig>, IConfig
{
    public ConfigCollection(IEnumerable<IConfig> values) : base(values)
    {
    }
}