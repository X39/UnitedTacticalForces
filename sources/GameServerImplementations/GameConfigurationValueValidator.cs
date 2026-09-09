using System.Text.Json;

namespace X39.UnitedTacticalForces.GameServerImplementations;

public static class GameConfigurationValueValidator
{
    public static bool IsCompatible(GameConfigurationDefinition definition, JsonElement value, out string? error)
    {
        error = null;
        if (!HasExpectedKind(definition.Kind, value))
        {
            error = $"Configuration value '{definition.Key}' has the wrong type.";
            return false;
        }

        if (definition.Kind is EGameConfigurationValueKind.Integer or EGameConfigurationValueKind.Number)
        {
            if (!value.TryGetDecimal(out var number))
            {
                error = $"Configuration value '{definition.Key}' is not a supported number.";
                return false;
            }
            if (definition.Minimum is { } minimum && number < minimum
                || definition.Maximum is { } maximum && number > maximum)
            {
                error = $"Configuration value '{definition.Key}' is outside its allowed range.";
                return false;
            }
            if (definition.Kind is EGameConfigurationValueKind.Integer && decimal.Truncate(number) != number)
            {
                error = $"Configuration value '{definition.Key}' must be an integer.";
                return false;
            }
        }

        if (definition.Choices is { Count: > 0 }
            && !definition.Choices.Any(q => JsonElement.DeepEquals(q.Value, value)))
        {
            error = $"Configuration value '{definition.Key}' is not an allowed choice.";
            return false;
        }

        if (definition.AllowedValues is { Count: > 0 })
        {
            var selected = value.GetString();
            if (selected is null || !definition.AllowedValues.Contains(selected, StringComparer.Ordinal))
            {
                error = $"Configuration value '{definition.Key}' is not an allowed selection.";
                return false;
            }
        }
        return true;
    }

    private static bool HasExpectedKind(EGameConfigurationValueKind kind, JsonElement value)
        => kind switch
        {
            EGameConfigurationValueKind.String or EGameConfigurationValueKind.Selection
                => value.ValueKind is JsonValueKind.String,
            EGameConfigurationValueKind.Integer or EGameConfigurationValueKind.Number
                => value.ValueKind is JsonValueKind.Number,
            EGameConfigurationValueKind.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            EGameConfigurationValueKind.StringArray => value.ValueKind is JsonValueKind.Array
                                                       && value.EnumerateArray().All(q => q.ValueKind is JsonValueKind.String),
            _ => false,
        };
}
