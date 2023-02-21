using X39.UnitedTacticalForces.WebApp.Services.UserRepository;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Scoped<MeService>]
public class MeService
{
    private readonly IUserRepository _userRepository;

    public MeService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    private User? _user;

    /// <summary>
    /// Returns the current <see cref="WebApp.User"/> object of the user.
    /// </summary>
    /// <remarks>
    /// If <see cref="IsAuthenticated"/> returns <see langword="false"/>, this will throw an exception.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the current user is not authenticated.
    ///     Authentication status can be checked using <see cref="IsAuthenticated"/>.
    /// </exception>
    public User User => _user ?? throw new InvalidOperationException(
        $"User is not authenticated. Check authentication status prior to using property using {nameof(IsAuthenticated)}.");

    public bool IsAuthenticated => _user is not null;

    /// <summary>
    /// Initializes the service.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="Program"/> at app-start.
    /// </remarks>
    public async Task InitializeAsync()
    {
        if (_user is not null)
            return;
        try
        {
            _user = await _userRepository.GetMeAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            // empty
        }
    }
}