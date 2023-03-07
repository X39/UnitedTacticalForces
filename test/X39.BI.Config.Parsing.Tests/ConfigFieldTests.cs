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
    [InlineData("abc", -1D, "abc = -1;")]
    [InlineData("abc", -2D, "abc = -2;")]
    [InlineData("ccc", -3D, "ccc= -3;")]
    [InlineData("bbb", -3D, "bbb=-3;")]
    [InlineData("cba", -3D, "cba =-3;")]
    [InlineData("bac", -3D, "bac = -3 ;")]
    [InlineData("bca", -3D, "bca = -3 ; ")]
    [InlineData("123abc_", -17D, "123abc_ = -17; ")]
    [InlineData("fffff", -0.5D, "fffff = -0.5;")]
    [InlineData("___", -1.25D, "___ = -1.25;")]
    [InlineData("Exp1", 1.250000E-001D, "Exp1 = 1.250000E-001;")]
    [InlineData("Exp2", -1.250000E-001D, "Exp2 = -1.250000E-001;")]
    [InlineData("Exp3", 1.125000E+000D, "Exp3 = 1.125000E+000;")]
    [InlineData("Exp4", -1.125000E+000D, "Exp4 = -1.125000E+000;")]
    [InlineData("exp1", 1.250000E-001D, "exp1 = 1.250000e-001;")]
    [InlineData("exp2", -1.250000E-001D, "exp2 = -1.250000e-001;")]
    [InlineData("exp3", 1.125000E+000D, "exp3 = 1.125000e+000;")]
    [InlineData("exp4", -1.125000E+000D, "exp4 = -1.125000e+000;")]
    [InlineData("internal", -3.1170201999999999E-08D, "internal = -3.1170202e-008;")]
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
    [InlineData("multiline", "foo\"bar\nfoo\"bar", "multiline = \"foo\"\"bar\" \\n \"foo\"\"bar\";")]
    public void MultilineStringField(string expectedKey, string expectedString, string input)
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
    [InlineData("doubleString", "foo\"bar", "doubleString = \"foo\"\"bar\";")]
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
    [InlineData("singleWide", new object?[]{new object?[]{}}, "singleWide [ ] = { { } } ;")]
    [InlineData("doubleWide", new object?[]{new object?[]{new object?[]{}}}, "doubleWide [ ] = { { { } } } ;")]
    [InlineData("singleNarrow", new object?[]{new object?[]{}}, "singleNarrow[]={{}};")]
    [InlineData("doubleNarrow", new object?[]{new object?[]{new object?[]{}}}, "doubleNarrow[]={{{}}};")]
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
    [InlineData("arr", new object?[]{"abc", "123", "_\"_", "a_2", "2a_"}, @"arr [ ] = { ""abc"" , ""123"" , ""_""""_"" , ""a_2"" , ""2a_"" } ;")]
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
    [InlineData("arr", new object?[]{"123D", 0.5D, 1.25D, 1D, new object?[]{"12\"3D", 0.5D, 1.25D, 1D}}, @"arr [ ] = { ""123D"" , 0.5 , 1.25 , 1 , { ""12""""3D"" , 0.5 , 1.25 , 1 } } ;")]
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