using X39.Util.Blazor.WebAssembly.Services;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Singleton<UiConfiguration>]
public class UiConfiguration : UiService
{
    private readonly LocalStorage                       _localStorage;
    private          bool                               _darkMode;

    private const string DarkModeStorageKey = nameof(UiConfiguration) + "." + nameof(DarkMode);
    public bool DarkMode
    {
        get => _darkMode;
        set
        {
            _darkMode = value;
            Task.Run(async () => await _localStorage.SetAsync(DarkModeStorageKey, _darkMode));
            NotifyUi();
        }
    }

    public UiConfiguration(LocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task InitializeAsync()
    {
        _darkMode = await _localStorage.GetAsync<bool>(DarkModeStorageKey);
        NotifyUi();
    }
}