using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace X39.UnitedTacticalForces.WebApp.Services;

public sealed class JsonStringEnumIntegerConverter<TEnum> : JsonConverter<int>
    where TEnum : struct, Enum
{
    private static readonly IReadOnlyDictionary<string, int> EnumValues = CreateEnumValues();

    public override int Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
            return default;

        if (reader.TokenType is JsonTokenType.Number && reader.TryGetInt32(out var numberValue))
            return numberValue;

        if (reader.TokenType is JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (string.IsNullOrWhiteSpace(stringValue))
                return default;

            if (int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNumber))
                return parsedNumber;

            if (EnumValues.TryGetValue(stringValue, out var enumValue))
                return enumValue;
        }

        throw new JsonException($"Expected an integer or {typeof(TEnum).Name} enum name.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        int value,
        JsonSerializerOptions options)
    {
        var enumValue = (TEnum) Enum.ToObject(typeof(TEnum), value);
        writer.WriteStringValue(GetEnumText(enumValue));
    }

    private static IReadOnlyDictionary<string, int> CreateEnumValues()
    {
        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var enumValue in Enum.GetValues<TEnum>())
        {
            var integerValue = Convert.ToInt32(enumValue, CultureInfo.InvariantCulture);
            values[enumValue.ToString()] = integerValue;

            var enumMemberValue = GetEnumMemberValue(enumValue);
            if (!string.IsNullOrWhiteSpace(enumMemberValue))
                values[enumMemberValue] = integerValue;
        }

        return values;
    }

    private static string GetEnumText(TEnum value)
        => GetEnumMemberValue(value) ?? value.ToString();

    private static string? GetEnumMemberValue(TEnum value)
        => typeof(TEnum)
            .GetMember(value.ToString())
            .FirstOrDefault()
            ?.GetCustomAttribute<EnumMemberAttribute>()
            ?.Value;
}
