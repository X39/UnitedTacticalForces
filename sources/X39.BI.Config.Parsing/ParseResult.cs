namespace X39.BI.Config.Parsing;

public record ParseResult(IConfig Config, ConfigParserError? Error);