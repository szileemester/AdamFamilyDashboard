using MudBlazor;

namespace AdaManagement.Client.Services;

/// <summary>„Kész ✔” értesítés Visszavonás gombbal, ha véletlenül pipáltál ki valamit.</summary>
public class UndoSnackbar(ISnackbar snackbar)
{
    public void Show(string message, Func<Task> undo, Func<Task>? afterUndo = null) =>
        snackbar.Add(message, Severity.Success, o =>
        {
            o.Action = "Visszavonás";
            o.ActionColor = Color.Inherit;
            o.VisibleStateDuration = 8000;
            o.OnClick = async _ =>
            {
                try
                {
                    await undo();
                    if (afterUndo is not null) await afterUndo();
                    snackbar.Add("Visszavonva.", Severity.Info);
                }
                catch (Exception ex)
                {
                    snackbar.Add(ex.Message, Severity.Error);
                }
            };
        });
}
