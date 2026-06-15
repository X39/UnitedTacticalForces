using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace X39.UnitedTacticalForces.Api.ModelBinding;

internal sealed class NodaTimeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return modelType == typeof(Instant) || modelType == typeof(LocalDate) || modelType == typeof(LocalTime)
            ? new NodaTimeIsoModelBinder()
            : null;
    }
}
