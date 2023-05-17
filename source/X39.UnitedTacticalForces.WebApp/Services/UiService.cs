using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace X39.UnitedTacticalForces.WebApp.Services;

public interface IUiService
{
    /// <summary>
    /// Registers a component to be notified when the UI configuration changes
    /// via <see cref="ComponentBase.StateHasChanged"/>
    /// </summary>
    /// <param name="component">The component to register.</param>
    void RegisterComponent(ComponentBase component);
}

/// <summary>
/// Implements <see cref="IUiService"/> and provides the base implementation.
/// </summary>
/// <remarks>
/// Derive this class if you want to implement a service that notifies components.
/// Make sure to call <see cref="RegisterComponent"/> for UI-Changing methods.
/// Ensure that the interface of a derived service is registered also implementing <see cref="IUiService"/> to
/// allow components to register to the service.
/// </remarks>
public abstract class UiService : IUiService
{
    private readonly   List<WeakReference<ComponentBase>> _components = new();
    private readonly   Action<ComponentBase>              _stateHasChanged;

    protected UiService()
    {
        var method = typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method is null)
            throw new Exception("Could not find StateHasChanged method");
        var parameterExpression = Expression.Parameter(typeof(ComponentBase), "component");
        var methodCallExpression = Expression.Call(parameterExpression, method);
        var lambdaExpression = Expression.Lambda<Action<ComponentBase>>(methodCallExpression, parameterExpression);
        _stateHasChanged = lambdaExpression.Compile();
    }

    /// <inheritdoc/>
    public void RegisterComponent(ComponentBase component)
    {
        RemoveDeadReferences();
        AddReference(component);
    }

    
    /// <summary>
    /// Notifies all registered components for rendering the state change.
    /// </summary>
    protected void NotifyUi()
    {
        RemoveDeadReferences();
        lock (_components)
        {
            foreach (var weakReference in _components)
            {
                if (weakReference.TryGetTarget(out var component))
                    _stateHasChanged(component);
            }
        }
    }

    private void AddReference(ComponentBase component)
    {
        lock (_components)
        {
            _components.Add(new WeakReference<ComponentBase>(component));
        }
    }

    private void RemoveDeadReferences()
    {
        lock (_components)
        {
            var list = _components.Where(weakReference => !weakReference.TryGetTarget(out _)).ToList();
            foreach (var weakReference in list)
                _components.Remove(weakReference);
        }
    }
}