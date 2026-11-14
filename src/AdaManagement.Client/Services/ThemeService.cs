using MudBlazor;

namespace AdaManagement.Client.Services;

/// <summary>
/// A négy téma ugyanazt a (filmes) elrendezést használja, csak a színek és a mozgás térnek el:
/// a Piros és a Kék dinamikus (mozgó háttér, fénycsík), a Sötét és a Világos statikus.
/// </summary>
public enum AppTheme { Piros, Kek, Sotet, Vilagos }

public record ThemeInfo(AppTheme Id, string Name, string Description, bool IsDark, bool IsDynamic, MudTheme MudTheme)
{
    /// <summary>CSS- és JS-kulcs (pl. „kek”).</summary>
    public string Key => Id.ToString().ToLowerInvariant();
}

/// <summary>Témaválasztás. Eszközönként a localStorage-ban tárolódik (telefonon és gépen lehet más).</summary>
public class ThemeService(LocalCache cache)
{
    private const string ThemeKey = "ada.theme";

    public static readonly IReadOnlyList<ThemeInfo> All =
    [
        new(AppTheme.Piros, "Piros", "Mozgó, piros–lila fények", true, true, Dark(
                background: "#0a0a0b", surface: "rgba(22,22,25,.96)", drawer: "#111113",
                primary: "#F0464F", text: "#ffffff", secondary: "#b9b9c0", lines: "#2f2f35")),
        new(AppTheme.Kek, "Kék", "Mozgó, kék–türkiz fények", true, true, Dark(
                background: "#080b11", surface: "rgba(19,24,33,.96)", drawer: "#0e131b",
                primary: "#3D8BFF", text: "#ffffff", secondary: "#b3bccb", lines: "#2b3341")),
        new(AppTheme.Sotet, "Sötét", "Nyugodt grafit, mozgás nélkül", true, false, Dark(
                background: "#111113", surface: "rgba(28,28,32,.98)", drawer: "#161619",
                primary: "#EDEDF0", text: "#f3f3f5", secondary: "#a9a9b2", lines: "#34343b")),
        new(AppTheme.Vilagos, "Világos", "Világos háttér, mozgás nélkül", false, false, new MudTheme
            {
                PaletteLight = new PaletteLight
                {
                    Primary = "#1c1c21",
                    Secondary = "#C2185B",
                    Background = "#f2f2f5",
                    Surface = "rgba(255,255,255,.98)",
                    AppbarBackground = "rgba(0,0,0,0)",
                    AppbarText = "#1c1c21",
                    DrawerBackground = "#fafafb",
                    DrawerText = "#1c1c21",
                    DrawerIcon = "rgba(0,0,0,.6)",
                    TextPrimary = "rgba(0,0,0,.9)",
                    TextSecondary = "rgba(0,0,0,.6)",
                    ActionDefault = "rgba(0,0,0,.6)",
                    LinesDefault = "rgba(0,0,0,.12)",
                    LinesInputs = "rgba(0,0,0,.25)",
                    Divider = "rgba(0,0,0,.12)",
                    Error = "#D32F2F",
                    Success = "#1E8E5A",
                },
            }),
    ];

    public ThemeInfo Current { get; private set; } = All[0];
    public event Action? Changed;

    public async Task LoadAsync()
    {
        var stored = await cache.GetAsync<string>(ThemeKey);
        Current = stored switch
        {
            // korábbi témanevek
            "Mozi" => Get(AppTheme.Piros),
            "light" => Get(AppTheme.Vilagos),
            "Csendes" or "Bento" or "Meleg" => Get(AppTheme.Sotet),
            _ when Enum.TryParse<AppTheme>(stored, out var id) && Enum.IsDefined(id) => Get(id),
            _ => All[0],
        };
        Changed?.Invoke();
    }

    public async Task SetThemeAsync(AppTheme id)
    {
        Current = Get(id);
        await cache.SetAsync(ThemeKey, id.ToString());
        Changed?.Invoke();
    }

    /// <summary>Az app gyökerének osztályai: közös „theme-mozi” elrendezés + a téma saját színei és mozgása.</summary>
    public string CssClass =>
        $"theme-mozi theme-{Current.Key} {(Current.IsDynamic ? "is-dynamic" : "is-static")}{(Current.IsDark ? "" : " is-light")}";

    private static ThemeInfo Get(AppTheme id) => All.First(t => t.Id == id);

    private static MudTheme Dark(string background, string surface, string drawer, string primary,
        string text, string secondary, string lines) => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = primary,
            PrimaryContrastText = primary == "#EDEDF0" ? "#111113" : "#ffffff",
            Secondary = "#ED93B1",
            Background = background,
            Surface = surface,
            AppbarBackground = "rgba(0,0,0,0)",
            AppbarText = text,
            DrawerBackground = drawer,
            DrawerText = text,
            DrawerIcon = secondary,
            TextPrimary = text,
            TextSecondary = secondary,
            ActionDefault = secondary,
            LinesDefault = lines,
            LinesInputs = lines,
            Divider = lines,
            Error = "#F09595",
            Warning = "#FAC775",
            Success = "#5DCAA5",
            Info = "#85B7EB",
        },
    };
}
