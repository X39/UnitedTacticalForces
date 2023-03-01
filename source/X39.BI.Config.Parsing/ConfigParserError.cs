namespace X39.BI.Config.Parsing;

public record ConfigParserError(int Line, int Column, int Offset, string Message);