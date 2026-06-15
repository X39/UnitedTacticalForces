using X39.Util.Collections;
using Xunit;

namespace X39.BI.Config.Parsing.Tests;

public class MixedConfigClassTests
{
    [Fact]
    public void CanParseMixed1()
    {
        const string input = "class foobar : barfoo { field1 = 1; field2[] = {1,2,3};};";
        const string expectedIdentifier = "foobar";
        const string expectedExtends = "barfoo";
        var fields = new (string key, object? value)[]
        {
            (key: "field1", value: 1D),
            ("field2", new object?[] {1D, 2D, 3D}),
        };
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        var configClass = Assert.Single(collection.OfType<ConfigClass>());
        Assert.Equivalent(expectedIdentifier, configClass.Identifier);
        Assert.Equivalent(expectedExtends, configClass.Extends);
        Assert.Equivalent(fields.Length, configClass.Children.Count);
        Assert.Equivalent(fields.Length, configClass.Children.OfType<ConfigPair>().Count());
        foreach (var (child, index) in configClass.Children.Indexed())
        {
            var (key, value) = fields[index];
            var configPair   = Assert.IsType<ConfigPair>(child);
            Assert.Equivalent(key, configPair.Key);
            Assert.Equivalent(value, configPair.Value);
        }
    }

    [Fact]
    public void CanParseMixed2()
    {
        const string input = "class foobar : barfoo { field1 = 1; class nested; field2[] = {1,2,3}; };";
        const string expectedIdentifier = "foobar";
        const string expectedExtends = "barfoo";
        const string expectedIdentifierNested = "nested";
        var fields = new (string key, object? value)[]
        {
            (key: "field1", value: 1D),
            (string.Empty, null),
            ("field2", new object?[] {1D, 2D, 3D}),
        };
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        var configClass = Assert.Single(collection.OfType<ConfigClass>());
        Assert.Equivalent(expectedIdentifier, configClass.Identifier);
        Assert.Equivalent(expectedExtends, configClass.Extends);
        Assert.Equivalent(fields.Length, configClass.Children.Count);
        Assert.Equivalent(fields.Length - 1, configClass.Children.OfType<ConfigPair>().Count());
        Assert.Equivalent(1, configClass.Children.OfType<ConfigClass>().Count());
        foreach (var (child, index) in configClass.Children.Indexed())
        {
            var (key, value) = fields[index];
            if (index is 1)
            {
                var nestedClass = Assert.IsType<ConfigClass>(child);
                Assert.Equivalent(expectedIdentifierNested, nestedClass.Identifier);
            }
            else
            {
                var configPair = Assert.IsType<ConfigPair>(child);
                Assert.Equivalent(key, configPair.Key);
                Assert.Equivalent(value, configPair.Value);
            }
        }
    }
}
