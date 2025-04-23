namespace X39.BI.Config.Parsing;

public class ConfigPair : IConfig
{
    public object? Value { get; set; }
    public string Key { get; set; } = string.Empty;
}