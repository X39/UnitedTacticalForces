using Microsoft.FluentUI.AspNetCore.Components;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.UnitedTacticalForces.WebApp.Services.UserRepository;
using X39.UnitedTacticalForces.WebApp.UtfApi.Models;
using X39.Util.DependencyInjection.Attributes;

namespace X39.UnitedTacticalForces.WebApp.Services;

[Transient<UserAutoCompleteService>]
public class UserAutoCompleteService(IUserRepository userRepository)
{
    public PlainUserDtoImpl PlainUserDto { get; } = new(userRepository);

    public sealed class PlainUserDtoImpl(IUserRepository userRepository)
    {
        public async Task OnOptionsSearch(OptionsSearchEventArgs<PlainUserDto> arg)
        {
            var results = await userRepository.GetUsersAsync(0, 20, arg.Text);
            arg.Items = results.Select(user => new PlainUserDto
                {
                    Avatar         = user.Avatar,
                    AvatarMimeType = user.AvatarMimeType,
                    Nickname       = user.Nickname,
                    PrimaryKey     = user.PrimaryKey,
                }
            );
        }

        public string? OptionText(PlainUserDto arg)
        {
            return arg.Nickname;
        }
    }

    public FullUserDtoImpl FullUserDto { get; } = new(userRepository);

    public sealed class FullUserDtoImpl(IUserRepository userRepository)
    {
        public async Task OnOptionsSearch(OptionsSearchEventArgs<FullUserDto> arg)
        {
            var results = await userRepository.GetUsersAsync(0, 20, arg.Text);
            arg.Items = results;
        }

        public string? OptionText(PlainUserDto arg)
        {
            return arg.Nickname;
        }
    }
}
