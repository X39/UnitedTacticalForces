namespace X39.BI.Config.Parsing;

public record ConfigParserError(int Line, int Column, int Offset, string Message)
{
    public override string ToString() => $"[L{Line:0000}|C{Column:000}|O{Offset:00000}] {Message}";
}