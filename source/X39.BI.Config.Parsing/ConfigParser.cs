using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Pidgin;
using X39.Util;

namespace X39.BI.Config.Parsing;

/// <summary>
/// Offers config parsing capabilities.
/// </summary>
public static class ConfigParser
{
    private static readonly Parser<char, char> CurlyBracketOpen    = Parser.Char('{').Between(Parser.SkipWhitespaces);
    private static readonly Parser<char, char> CurlyBracketClose   = Parser.Char('}').Between(Parser.SkipWhitespaces);
    private static readonly Parser<char, char> SquareBracketOpen   = Parser.Char('[').Between(Parser.SkipWhitespaces);
    private static readonly Parser<char, char> SquareBracketClose  = Parser.Char(']').Between(Parser.SkipWhitespaces);
    private static readonly Parser<char, char> DoubleQuotationMark = Parser.Char('"').Between(Parser.SkipWhitespaces);
    private static readonly Parser<char, char> Comma               = Parser.Char(',').Between(Parser.SkipWhitespaces);
    private static readonly Parser<char, char> Colon               = Parser.Char(':').Between(Parser.SkipWhitespaces);
    private static readonly Parser<char, char> SemiColon           = Parser.Char(';').Between(Parser.SkipWhitespaces);
    private static readonly Parser<char, char> Equality            = Parser.Char('=').Between(Parser.SkipWhitespaces);
    private static readonly Parser<char, char> Minus               = Parser.Char('-').Between(Parser.SkipWhitespaces);

    private static readonly Parser<char, double> NonNegativeNumber = Parser.LetterOrDigit
        .AtLeastOnce()
        .Then(Parser.Char('.').Then(Parser.LetterOrDigit.AtLeastOnce()).Optional(), AssignDouble);

    private static readonly Parser<char, double> Number = Parser.OneOf(
        Minus.Then(NonNegativeNumber, (_, d) => -d),
        NonNegativeNumber);

    private static readonly Parser<char, string> String = Parser.OneOf(
            Parser.AnyCharExcept('"'),
            Parser.Try(DoubleQuotationMark.Repeat(2).Select((_) => '"')))
        .Many()
        .Between(DoubleQuotationMark)
        .Select(AssignString);

    private static readonly Parser<char, string> ClassKeyword = Parser.String("class");

    private static readonly Parser<char, string> Identifier
        = Parser<char>.Token((c) => c.IsLetterOrDigit() || c is '_').ManyString().Labelled("identifier");

    private static readonly Parser<char, object?> Value = Parser.OneOf(
        Parser.Try(Number.Select((q) => (object?) q)),
        Parser.Try(String.Select((q) => (object?) q)),
        Parser.Rec(() => Array).Select((q) => (object?) q));

    private static readonly Parser<char, object?[]> Array = CurlyBracketOpen
        .Then(Value.Between(Parser.SkipWhitespaces).Separated(Comma), (_, objs) => objs.ToArray())
        .Before(CurlyBracketClose);

    private static readonly Parser<char, object?> PairAssignment = Parser.OneOf(
        SquareBracketOpen
            .Then(SquareBracketClose.Between(Parser.SkipWhitespaces))
            .Then(Equality)
            .Then(Array.Between(Parser.SkipWhitespaces))
            .Before(SemiColon)
            .Select((q) => (object?) q),
        Equality
            .Then(
                Parser.OneOf(
                    Parser.Try(Number.Select((q) => (object?) q)),
                    String.Select((q) => (object?) q)
                ))
            .Between(Parser.SkipWhitespaces)
            .Before(SemiColon));

    private static readonly Parser<char, ConfigPair> Pair = Identifier.Select((s) => new ConfigPair {Key = s})
        .Then(PairAssignment.Between(Parser.SkipWhitespaces), AssignClassPairValue);

    private static readonly Parser<char, IEnumerable<IConfig>> ClassBody
        = Parser.OneOf(
                Parser.Try(Parser.Rec(() => Class).Select((q) => q as IConfig)),
                Pair.Select((q) => q as IConfig)
            )
            .Many()
            .Between(CurlyBracketOpen, CurlyBracketClose);

    private static readonly Parser<char, ConfigClass> Class = ClassKeyword.Select((_) => new ConfigClass())
        .Then(Identifier.Between(Parser.SkipWhitespaces), AssignConfigClassIdentifier)
        .Then(Colon.Then(Identifier).Between(Parser.SkipWhitespaces).Optional(), AssignConfigClassExtends)
        .Then(ClassBody.Optional(), AssignConfigClassChildren)
        .Before(SemiColon)
        .Labelled("class declaration");

    public static ParseResult Parse(TextReader reader)
    {
        var result = Parser.OneOf(
                Parser.Try(Class.Select((q) => q as IConfig)),
                Pair.Select((q) => q as IConfig))
            .Many()
            .Parse(reader);
        if (!result.Success)
            return new ParseResult(
                new InvalidConfig(),
                new ConfigParserError(
                    result.Error!.ErrorPos.Line,
                    result.Error.ErrorPos.Col,
                    result.Error.ErrorOffset,
                    result.Error.Message ?? result.Error.ToString()));
        return new ParseResult(new ConfigCollection(result.Value), null);
    }

    public static ParseResult Parse(string input)
    {
        using var stringReader = new StringReader(input);
        return Parse(stringReader);
    }

    public static IConfig ParseOrThrow(TextReader input)
    {
        var (config, parserError) = Parse(input);
        if (parserError is not null)
            throw new FormatException(
                $"[L{parserError.Line}C{parserError.Column}O{parserError.Offset}] {parserError.Message}");
        return config;
    }

    public static IConfig ParseOrThrow(string input)
    {
        var (config, parserError) = Parse(input);
        if (parserError is not null)
            throw new FormatException(
                $"[L{parserError.Line}|C{parserError.Column}|O{parserError.Offset}] {parserError.Message}");
        return config;
    }

    private static ConfigClass AssignConfigClassExtends(ConfigClass configClass, Maybe<string> identifier)
    {
        if (!identifier.HasValue)
            return configClass;
        configClass.Extends = identifier.Value;
        return configClass;
    }

    private static ConfigClass AssignConfigClassIdentifier(ConfigClass configClass, string identifier)
    {
        configClass.Identifier = identifier;
        return configClass;
    }

    private static ConfigClass AssignConfigClassChildren(
        ConfigClass configClass,
        Maybe<IEnumerable<IConfig>> children)
    {
        configClass.Children =
            children.HasValue ? children.Value.ToImmutableArray() : ImmutableArray<ConfigPair>.Empty;
        return configClass;
    }

    private static ConfigPair AssignClassPairValue(ConfigPair pair, object? value)
    {
        pair.Value = value;
        return pair;
    }

    private static double AssignDouble(IEnumerable<char> arg1, Maybe<IEnumerable<char>> arg2)
    {
        var builder = new StringBuilder();
        foreach (var c in arg1)
            builder.Append(c);
        if (!arg2.HasValue)
            return double.Parse(builder.ToString(), CultureInfo.InvariantCulture);
        builder.Append('.');
        foreach (var c in arg2.Value)
            builder.Append(c);
        return double.Parse(builder.ToString(), CultureInfo.InvariantCulture);
    }

    private static string AssignString(IEnumerable<char> arg)
    {
        var builder = new StringBuilder();
        foreach (var c in arg)
            builder.Append(c);
        return builder.ToString();
    }
}