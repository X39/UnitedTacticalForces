using Microsoft.FluentUI.AspNetCore.Components;
using X39.UnitedTacticalForces.WebApp.Components;

namespace X39.UnitedTacticalForces.WebApp;

public static class UtfDialogServiceExtensions
{
    public static async Task<bool> ShowUtfConfirmationAsync(
        this IDialogService dialogService,
        string title,
        string message,
        string confirmLabel,
        string cancelLabel,
        bool destructive = false,
        string? kicker = null)
    {
        var content = new UtfConfirmationDialog.Data
        {
            Kicker = kicker ?? title,
            Message = message,
            ConfirmLabel = confirmLabel,
            CancelLabel = cancelLabel,
            Destructive = destructive,
        };
        var dialogReference = await dialogService
            .ShowDialogAsync<UtfConfirmationDialog, UtfConfirmationDialog.Data>(
                content,
                new DialogParameters
                {
                    Title = title,
                    Modal = true,
                    ShowDismiss = false,
                    PreventDismissOnOverlayClick = true,
                    PreventScroll = true,
                    TrapFocus = true,
                    Width = "min(92vw, 34rem)",
                    DialogBodyStyle = "overflow: hidden; background: var(--utf-bg-elevated); color: var(--utf-text); padding: 0;",
                })
            .ConfigureAwait(false);
        var result = await dialogReference.Result.ConfigureAwait(false);
        return !result.Cancelled;
    }
}
