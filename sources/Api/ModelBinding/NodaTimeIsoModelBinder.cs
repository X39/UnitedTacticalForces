using Microsoft.AspNetCore.Mvc.ModelBinding;
using NodaTime.Text;

namespace X39.UnitedTacticalForces.Api.ModelBinding;

internal sealed class NodaTimeIsoModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);
        var value = valueProviderResult.FirstValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
                bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        var modelType = Nullable.GetUnderlyingType(bindingContext.ModelType) ?? bindingContext.ModelType;
        if (TryParse(modelType, value, out var model))
        {
            bindingContext.Result = ModelBindingResult.Success(model);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"The value '{value}' is not a valid ISO-8601 {GetTypeDescription(modelType)}.");
        return Task.CompletedTask;
    }

    private static bool TryParse(Type modelType, string value, out object? model)
    {
        if (modelType == typeof(Instant))
        {
            if (TryParseInstant(value, out var instant))
            {
                model = instant;
                return true;
            }
        }
        else if (modelType == typeof(LocalDate))
        {
            var result = LocalDatePattern.Iso.Parse(value);
            if (result.Success)
            {
                model = result.Value;
                return true;
            }
        }
        else if (modelType == typeof(LocalTime))
        {
            var result = LocalTimePattern.ExtendedIso.Parse(value);
            if (result.Success)
            {
                model = result.Value;
                return true;
            }
        }

        model = null;
        return false;
    }

    private static bool TryParseInstant(string value, out Instant instant)
    {
        var instantResult = InstantPattern.ExtendedIso.Parse(value);
        if (instantResult.Success)
        {
            instant = instantResult.Value;
            return true;
        }

        var offsetDateTimeResult = OffsetDateTimePattern.ExtendedIso.Parse(value);
        if (offsetDateTimeResult.Success)
        {
            instant = offsetDateTimeResult.Value.ToInstant();
            return true;
        }

        instant = default;
        return false;
    }

    private static string GetTypeDescription(Type modelType)
        => modelType == typeof(Instant)
            ? "instant"
            : modelType == typeof(LocalDate)
                ? "date"
                : "time";
}
