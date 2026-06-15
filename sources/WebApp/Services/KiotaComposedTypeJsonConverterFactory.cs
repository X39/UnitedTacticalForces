using System.Reflection;
using System.Text.Json;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Kiota.Abstractions.Serialization;

namespace X39.UnitedTacticalForces.WebApp.Services;

internal sealed class KiotaComposedTypeJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => !typeToConvert.IsAbstract
           && !typeToConvert.IsInterface
           && typeof(IComposedTypeWrapper).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(KiotaComposedTypeJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter) Activator.CreateInstance(converterType)!;
    }

    private sealed class KiotaComposedTypeJsonConverter<TWrapper> : JsonConverter<TWrapper>
        where TWrapper : class, IComposedTypeWrapper, new()
    {
        private static readonly PropertyInfo[] WritableProperties = typeof(TWrapper)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where((property) => property.CanWrite)
            .ToArray();

        public override TWrapper? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.Null)
                return null;

            var wrapper = new TWrapper();
            if (TryReadPrimitive(ref reader, wrapper))
                return wrapper;

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                var property = GetPayloadProperty(reader.TokenType);
                if (property is null)
                    throw new JsonException($"No payload property found for Kiota wrapper {typeof(TWrapper).Name}.");

                var value = JsonSerializer.Deserialize(ref reader, property.PropertyType, options);
                property.SetValue(wrapper, value);
                return wrapper;
            }

            throw new JsonException($"Unsupported token {reader.TokenType} for Kiota wrapper {typeof(TWrapper).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, TWrapper value, JsonSerializerOptions options)
        {
            var property = WritableProperties
                .OrderBy(IsGeneratedPlaceholderProperty)
                .FirstOrDefault((candidate) => candidate.GetValue(value) is not null);

            if (property is null)
            {
                writer.WriteNullValue();
                return;
            }

            JsonSerializer.Serialize(writer, property.GetValue(value), property.PropertyType, options);
        }

        private static bool TryReadPrimitive(ref Utf8JsonReader reader, TWrapper wrapper)
            => reader.TokenType switch
            {
                JsonTokenType.False or JsonTokenType.True => TrySetBoolean(reader.GetBoolean(), wrapper),
                JsonTokenType.Number                      => TrySetNumber(ref reader, wrapper),
                JsonTokenType.String                      => TrySetString(ref reader, wrapper),
                _                                         => false,
            };

        private static bool TrySetBoolean(bool value, TWrapper wrapper)
            => TrySetProperty("Boolean", value, wrapper);

        private static bool TrySetNumber(ref Utf8JsonReader reader, TWrapper wrapper)
        {
            if (TryGetProperty("Integer", out _))
                return TrySetProperty("Integer", reader.GetInt32(), wrapper);
            if (TryGetProperty("Int64", out _))
                return TrySetProperty("Int64", reader.GetInt64(), wrapper);
            if (TryGetProperty("Double", out _))
                return TrySetProperty("Double", reader.GetDouble(), wrapper);

            var enumProperty = WritableProperties.FirstOrDefault((property) =>
                GetValueType(property.PropertyType).IsEnum
            );
            if (enumProperty is not null)
            {
                var enumType = GetValueType(enumProperty.PropertyType);
                enumProperty.SetValue(wrapper, Enum.ToObject(enumType, reader.GetInt32()));
                return true;
            }

            return false;
        }

        private static bool TrySetString(ref Utf8JsonReader reader, TWrapper wrapper)
        {
            var value = reader.GetString();
            if (TryGetProperty("String", out _))
                return TrySetProperty("String", value, wrapper);
            if (value is null)
                return false;
            if (TryGetProperty("Guid", out _))
                return TrySetProperty("Guid", Guid.Parse(value), wrapper);
            if (TryGetProperty("DateTimeOffset", out _))
                return TrySetProperty(
                    "DateTimeOffset",
                    DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None),
                    wrapper
                );
            if (TryGetProperty("Base64", out _))
                return TrySetProperty("Base64", Convert.FromBase64String(value), wrapper);

            var enumProperty = WritableProperties.FirstOrDefault((property) =>
                GetValueType(property.PropertyType).IsEnum
            );
            if (enumProperty is null)
                return false;

            var enumType = GetValueType(enumProperty.PropertyType);
            enumProperty.SetValue(wrapper, Enum.Parse(enumType, value, ignoreCase: true));
            return true;
        }

        private static bool TrySetProperty<TValue>(string propertyName, TValue value, TWrapper wrapper)
        {
            if (!TryGetProperty(propertyName, out var property))
                return false;

            property.SetValue(wrapper, value);
            return true;
        }

        private static bool TryGetProperty(string propertyName, out PropertyInfo property)
        {
            property = WritableProperties.FirstOrDefault((candidate) =>
                candidate.Name.Equals(propertyName, StringComparison.Ordinal)
            )!;
            return property is not null;
        }

        private static PropertyInfo? GetPayloadProperty(JsonTokenType tokenType)
        {
            var candidates = WritableProperties
                .Where((property) => tokenType is JsonTokenType.StartArray
                    ? IsArrayPayloadProperty(property)
                    : IsObjectPayloadProperty(property)
                )
                .OrderBy(IsGeneratedPlaceholderProperty)
                .ToArray();
            return candidates.FirstOrDefault();
        }

        private static bool IsObjectPayloadProperty(PropertyInfo property)
        {
            var propertyType = GetValueType(property.PropertyType);
            return propertyType != typeof(string)
                   && propertyType != typeof(byte[])
                   && propertyType != typeof(object)
                   && !propertyType.IsValueType
                   && !typeof(System.Collections.IDictionary).IsAssignableFrom(propertyType)
                   && !IsArrayPayloadProperty(property);
        }

        private static bool IsArrayPayloadProperty(PropertyInfo property)
        {
            var propertyType = GetValueType(property.PropertyType);
            return propertyType != typeof(string)
                   && propertyType != typeof(byte[])
                   && typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyType);
        }

        private static bool IsGeneratedPlaceholderProperty(PropertyInfo property)
            => property.Name.EndsWith("Member1", StringComparison.Ordinal);

        private static Type GetValueType(Type type)
            => Nullable.GetUnderlyingType(type) ?? type;
    }
}
