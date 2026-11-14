using MudBlazor;

namespace AdaManagement.Client.Services;

/// <summary>
/// Visszavonható műveletek (pl. „Kész”) verme. Felugró értesítés helyett egy visszavonás-ikon jelenik meg,
/// amíg van mit visszavonni (Mozi kezdőképen az „Ismétlődők” mellett, máshol a fejlécben).
/// A visszavonás után az <see cref="Undone"/> esemény szól, erre frissítik magukat az érintett oldalak.
/// </summary>
public class UndoService(ISnackbar snackbar)
{
    private const int MaxEntries = 10;
    private readonly List<(string Label, Func<Task> Undo)> _stack = [];
    private bool _busy;

    /// <summary>A verem változott (ikon megjelenik/eltűnik).</summary>
    public event Action? Changed;

    /// <summary>Egy visszavonás lefutott – az adatokat újra kell tölteni.</summary>
    public event Func<Task>? Undone;

    public bool CanUndo => _stack.Count > 0 && !_busy;

    /// <summary>A legutóbbi visszavonható művelet felirata (a gomb tooltipje).</summary>
    public string? LastLabel => _stack.Count > 0 ? _stack[^1].Label : null;

    public void Push(string label, Func<Task> undo)
    {
        _stack.Add((label, undo));
        if (_stack.Count > MaxEntries) _stack.RemoveAt(0);
        Changed?.Invoke();
    }

    public async Task UndoLastAsync()
    {
        if (!CanUndo) return;
        var (label, undo) = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        _busy = true;
        Changed?.Invoke();
        try
        {
            await undo();
            if (Undone is not null)
                foreach (var h in Undone.GetInvocationList().Cast<Func<Task>>()) await h();
        }
        catch (Exception ex)
        {
            // Hibát továbbra is jelzünk – csak a „Kész” / „Visszavonva” típusú értesítések tűntek el.
            snackbar.Add(ex.Message, Severity.Error);
            // Átmeneti hibánál (hálózat, alvó szerver) a művelet visszakerül, így újra lehet próbálni.
            // Ha a szerver elutasította (4xx: pl. már nincs meg), nincs értelme újrapróbálni.
            if (ex is not ApiException { StatusCode: >= 400 and < 500 })
                _stack.Add((label, undo));
        }
        finally
        {
            _busy = false;
            Changed?.Invoke();
        }
    }
}
