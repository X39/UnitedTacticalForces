using Xunit;

namespace X39.BI.Config.Parsing.Tests;

public class ConfigFieldTests
{
    [Theory]
    [InlineData("abc", 1D, "abc = 1;")]
    [InlineData("abc", 2D, "abc = 2;")]
    [InlineData("ccc", 3D, "ccc= 3;")]
    [InlineData("bbb", 3D, "bbb=3;")]
    [InlineData("cba", 3D, "cba =3;")]
    [InlineData("bac", 3D, "bac = 3 ;")]
    [InlineData("bca", 3D, "bca = 3 ; ")]
    [InlineData("123abc_", 17D, "123abc_ = 17; ")]
    [InlineData("fffff", 0.5D, "fffff = 0.5;")]
    [InlineData("___", 1.25D, "___ = 1.25;")]
    public void NumberFields(string expectedKey, double expectedNumber, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        Assert.Single(collection.OfType<ConfigPair>());
        Assert.Equal(expectedKey, collection.OfType<ConfigPair>().First().Key);
        Assert.IsType<double>(collection.OfType<ConfigPair>().First().Value);
        Assert.Equivalent(expectedNumber, (double) collection.OfType<ConfigPair>().First().Value!);
    }
    
    [Theory]
    [InlineData("abc", "1", "abc = \"1\";")]
    [InlineData("abc", "2", "abc = \"2\";")]
    [InlineData("ccc", "3", "ccc= \"3\";")]
    [InlineData("bbb", "3", "bbb=\"3\";")]
    [InlineData("cba", "3", "cba =\"3\";")]
    [InlineData("bac", "3", "bac = \"3\" ;")]
    [InlineData("bca", "3", "bca = \"3\" ; ")]
    [InlineData("123abc_", "17", "123abc_ = \"17\"; ")]
    [InlineData("fffff", "0.5", "fffff = \"0.5\";")]
    [InlineData("___", "1.25", "___ = \"1.25\";")]
    public void StringFields(string expectedKey, string expectedString, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        Assert.Single(collection.OfType<ConfigPair>());
        Assert.Equal(expectedKey, collection.OfType<ConfigPair>().First().Key);
        Assert.IsType<string>(collection.OfType<ConfigPair>().First().Value);
        Assert.Equivalent(expectedString, (string) collection.OfType<ConfigPair>().First().Value!);
    }
    
    [Theory]
    [InlineData("arr", new object?[]{}, "arr[] = {};")]
    [InlineData("abc", new object?[]{}, "abc[] = { };")]
    [InlineData("ccc", new object?[]{}, "ccc[]={ } ;")]
    [InlineData("bbb", new object?[]{}, "bbb[]={} ;")]
    [InlineData("abc", new object?[]{}, "abc [] = { };")]
    [InlineData("ccc", new object?[]{}, "ccc[] ={ } ;")]
    [InlineData("bbb", new object?[]{}, "bbb[ ]={} ;")]
    [InlineData("ddd", new object?[]{}, "ddd[ ] ={} ;")]
    [InlineData("aaa", new object?[]{}, "aaa [ ] ={} ;")]
    public void EmptyArrayFields(string expectedKey, object?[] expectedValues, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        Assert.Single(collection.OfType<ConfigPair>());
        Assert.Equal(expectedKey, collection.OfType<ConfigPair>().First().Key);
        Assert.IsType<object?[]>(collection.OfType<ConfigPair>().First().Value);
        Assert.Equivalent(expectedValues, collection.OfType<ConfigPair>().First().Value!);
    }
    [Theory]
    [InlineData("arr", new object?[]{new object?[]{},new object?[]{new object?[]{},new object?[]{new object?[]{}}}}, "arr[]={{},{{},{{}}}};")]
    [InlineData("abc", new object?[]{new object?[]{},new object?[]{new object?[]{},new object?[]{new object?[]{}}}}, "abc [ ] = { { } , { { } , { { } } } } ;")]
    public void NestedArrayFields(string expectedKey, object?[] expectedValues, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        Assert.Single(collection.OfType<ConfigPair>());
        Assert.Equal(expectedKey, collection.OfType<ConfigPair>().First().Key);
        Assert.IsType<object?[]>(collection.OfType<ConfigPair>().First().Value);
        Assert.Equivalent(expectedValues, collection.OfType<ConfigPair>().First().Value!);
    }
    [Theory]
    [InlineData("arr", new object?[]{"abc", "123", "___", "a_2", "2a_"}, @"arr[]={""abc"",""123"",""___"",""a_2"",""2a_""} ;")]
    [InlineData("arr", new object?[]{"abc", "123", "___", "a_2", "2a_"}, @"arr [ ] = { ""abc"" , ""123"" , ""___"" , ""a_2"" , ""2a_"" } ;")]
    public void StringArrayFields(string expectedKey, object?[] expectedValues, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        Assert.Single(collection.OfType<ConfigPair>());
        Assert.Equal(expectedKey, collection.OfType<ConfigPair>().First().Key);
        Assert.IsType<object?[]>(collection.OfType<ConfigPair>().First().Value);
        Assert.Equivalent(expectedValues, collection.OfType<ConfigPair>().First().Value!);
    }
    [Theory]
    [InlineData("arr", new object?[]{123D, 0.5D, 1.25D, 1D}, @"arr[]={123, 0.5, 1.25, 1};")]
    public void NumberArrayFields(string expectedKey, object?[] expectedValues, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        Assert.Single(collection.OfType<ConfigPair>());
        Assert.Equal(expectedKey, collection.OfType<ConfigPair>().First().Key);
        Assert.IsType<object?[]>(collection.OfType<ConfigPair>().First().Value);
        Assert.Equivalent(expectedValues, collection.OfType<ConfigPair>().First().Value!);
    }
    [Theory]
    [InlineData("arr", new object?[]{"123D", 0.5D, 1.25D, 1D, new object?[]{"123D", 0.5D, 1.25D, 1D}}, @"arr[]={""123D"",0.5,1.25,1,{""123D"",0.5,1.25,1}};")]
    [InlineData("arr", new object?[]{"123D", 0.5D, 1.25D, 1D, new object?[]{"123D", 0.5D, 1.25D, 1D}}, @"arr [ ] = { ""123D"" , 0.5 , 1.25 , 1 , { ""123D"" , 0.5 , 1.25 , 1 } } ;")]
    public void MixedArrayFields(string expectedKey, object?[] expectedValues, string input)
    {
        var result = ConfigParser.ParseOrThrow(input);
        Assert.IsType<ConfigCollection>(result);
        var collection = (ConfigCollection) result;
        Assert.Single(collection);
        Assert.Single(collection.OfType<ConfigPair>());
        Assert.Equal(expectedKey, collection.OfType<ConfigPair>().First().Key);
        Assert.IsType<object?[]>(collection.OfType<ConfigPair>().First().Value);
        Assert.Equivalent(expectedValues, collection.OfType<ConfigPair>().First().Value!);
    }
}