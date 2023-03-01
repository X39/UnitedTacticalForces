using X39.Util.Collections;
using Xunit;

namespace X39.BI.Config.Parsing.Tests;

public class EmptyConfigClassTests
{
    [Theory]
    [InlineData("testClass", "class testClass;")]
    [InlineData("fancy", "class fancy ; ")]
    [InlineData("123", "class 123 ;")]
    [InlineData("_abc123", "class _abc123; ")]
    public void CanParseEmpty(string expected, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        Assert.Single(collection.OfType<ConfigClass>());
        Assert.Equal(expected, collection.OfType<ConfigClass>().First().Identifier);
    }

    [Theory]
    [InlineData("testClass", "class testClass{};")]
    [InlineData("fancy", "class fancy{} ; ")]
    [InlineData("123", "class 123{} ;")]
    [InlineData("_abc123", "class _abc123{}; ")]
    [InlineData("testClass", "class testClass {};")]
    [InlineData("fancy", "class fancy {} ; ")]
    [InlineData("123", "class 123 {} ;")]
    [InlineData("_abc123", "class _abc123 {}; ")]
    [InlineData("testClass", "class testClass { };")]
    [InlineData("fancy", "class fancy { } ; ")]
    [InlineData("123", "class 123 { } ;")]
    [InlineData("_abc123", "class _abc123 { }; ")]
    public void CanParseEmptyBraced(string expected, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        Assert.Single(collection.OfType<ConfigClass>());
        Assert.Equal(expected, collection.OfType<ConfigClass>().First().Identifier);
    }

    [Theory]
    [InlineData(
        new[] {"testClass", "fancy", "123", "_abc123"},
        "class testClass{};class fancy{} ; class 123{} ;class _abc123{}; ")]
    [InlineData(new[] {"foo", "bar"}, "class foo;class bar;")]
    public void CanParseMultipleEmpty(string[] expected, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Equal(expected.Length, collection.Count);
        Assert.Equal(expected.Length, collection.OfType<ConfigClass>().Count());
        Assert.Equal(expected, collection.OfType<ConfigClass>().Select((q) => q.Identifier));
    }

    [Theory]
    [InlineData("foo", "bar", "class foo:bar;")]
    [InlineData("ccc", "bbb", "class ccc : bbb ;")]
    [InlineData("foobar", "barfoo", "class foobar : barfoo {};")]
    public void EmptyExtends(string expectedIdentifier, string expectedExtends, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        Assert.Single(collection.OfType<ConfigClass>());
        Assert.Equal(expectedIdentifier, collection.OfType<ConfigClass>().First().Identifier);
        Assert.Equal(expectedExtends, collection.OfType<ConfigClass>().First().Extends);
    }
}