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
        Assert.Single(collection.OfType<ConfigClass>());
        Assert.Equivalent(expectedIdentifier, collection.OfType<ConfigClass>().First().Identifier);
        Assert.Equivalent(expectedExtends, collection.OfType<ConfigClass>().First().Extends);
        Assert.Equivalent(fields.Length, collection.OfType<ConfigClass>().First().Children.Count);
        Assert.Equivalent(fields.Length, collection.OfType<ConfigClass>().First().Children.OfType<ConfigPair>().Count());
        Assert.Collection(
            collection.OfType<ConfigClass>().Indexed(),
            (tuple) =>
            {
                var (configClass, index) = tuple;
                var (key, value)         = fields[index];
                Assert.IsType<ConfigPair>(configClass.Children.ElementAt(index));
                Assert.Equivalent(key, configClass.Children.OfType<ConfigPair>().ElementAt(index).Key);
                Assert.Equivalent(value, configClass.Children.OfType<ConfigPair>().ElementAt(index).Value);
            });
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
        Assert.Single(collection.OfType<ConfigClass>());
        Assert.Equivalent(expectedIdentifier, collection.OfType<ConfigClass>().First().Identifier);
        Assert.Equivalent(expectedExtends, collection.OfType<ConfigClass>().First().Extends);
        Assert.Equivalent(fields.Length, collection.OfType<ConfigClass>().First().Children.Count);
        Assert.Equivalent(fields.Length - 1, collection.OfType<ConfigClass>().First().Children.OfType<ConfigPair>().Count());
        Assert.Equivalent(1, collection.OfType<ConfigClass>().First().Children.OfType<ConfigClass>().Count());
        Assert.Collection(
            collection.OfType<ConfigClass>().Indexed(),
            (tuple) =>
            {
                var (configClass, index) = tuple;
                var (key, value)         = fields[index];
                if (index is 1)
                {
                    Assert.IsType<ConfigClass>(configClass.Children.ElementAt(index));
                    Assert.Equivalent(expectedIdentifierNested, configClass.Children.OfType<ConfigClass>().ElementAt(index).Identifier);
                }
                else
                {
                    Assert.IsType<ConfigPair>(configClass.Children.ElementAt(index));
                    Assert.Equivalent(key, configClass.Children.OfType<ConfigPair>().ElementAt(index).Key);
                    Assert.Equivalent(value, configClass.Children.OfType<ConfigPair>().ElementAt(index).Value);
                }
            });
    }
}