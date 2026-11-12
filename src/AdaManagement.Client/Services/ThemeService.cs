using MudBlazor;

namespace AdaManagement.Client.Services;

public enum AppTheme { Csendes, Bento, Meleg, Vilagos }

public enum BackgroundAnimation { Ki, Hangulat, Idojaras }

/// <summary>Hogyan jelenjen meg a mátrix telefonon (desktopon mindig 2×2 rács).</summary>
public enum MatrixLayout { Lista, Bento, Fulek }

public record ThemeInfo(AppTheme Id, string Name, string Description, bool IsDark, MatrixLayout Layout, MudTheme MudTheme, string[] Swatch);

/// <summary>
/// Témaválasztás és háttéranimáció. Eszközönként a localStorage-ban tárolódik (telefonon és gépen lehet más).
/// </summary>
public class ThemeService(LocalCache cache)
{
    private const string ThemeKey = "ada.theme";
    private const string AnimationKey = "ada.animation";

    public static readonly IReadOnlyList<ThemeInfo> All =
    [
        new(AppTheme.Csendes, "Csendes", "Szinte fekete, letisztult lista", true, MatrixLayout.Lista, Dark(
                background: "#0c0c0f", surface: "rgba(31,31,38,.92)", bar: "rgba(12,12,15,.9)", drawer: "#15151a",
                primary: "#B8B2F2", text: "#f3f3f5", secondary: "#a8a8b2", lines: "#383841"),
            ["#0c0c0f", "#B8B2F2", "#F09595", "#FAC775"]),
        new(AppTheme.Bento, "Bento", "Színes csempék, gyors áttekintés", true, MatrixLayout.Bento, Dark(
                background: "#0f1115", surface: "rgba(36,41,52,.92)", bar: "rgba(15,17,21,.9)", drawer: "#181b22",
                primary: "#958EEA", text: "#f2f4f8", secondary: "#a7adbc", lines: "#3c4250"),
            ["#0f1115", "#958EEA", "#382024", "#382e18"]),
        new(AppTheme.Meleg, "Meleg", "Barnás tónus, fülek, alsó menü", true, MatrixLayout.Fulek, Dark(
                background: "#141110", surface: "rgba(46,40,36,.92)", bar: "rgba(20,17,16,.9)", drawer: "#1e1a18",
                primary: "#F5A285", text: "#f7f1ea", secondary: "#bcb2a8", lines: "#4a413b"),
            ["#141110", "#F5A285", "#4a2e26", "#f7f1ea"]),
        new(AppTheme.Vilagos, "Világos", "Az eredeti, világos megjelenés", false, MatrixLayout.Lista, new MudTheme
            {
                PaletteLight = new PaletteLight
                {
                    Primary = "#303f9f",
                    AppbarBackground = "#303f9f",
                    Background = "#eceef3",               // a fehér kártyák jobban elválnak a háttértől
                    Surface = "rgba(255,255,255,.96)",
                    TextPrimary = "rgba(0,0,0,.92)",
                    TextSecondary = "rgba(0,0,0,.66)",
                    LinesDefault = "rgba(0,0,0,.16)",
                    Divider = "rgba(0,0,0,.16)",
                },
            },
            ["#ffffff", "#3949ab", "#e53935", "#fb8c00"]),
    ];

    public ThemeInfo Current { get; private set; } = All[0];
    public BackgroundAnimation Animation { get; private set; } = BackgroundAnimation.Hangulat;
    public event Action? Changed;

    public async Task LoadAsync()
    {
        var stored = await cache.GetAsync<string>(ThemeKey);
        Current = stored switch
        {
            "light" => All.First(t => t.Id == AppTheme.Vilagos), // a korábbi világos/sötét kapcsoló értéke
            _ when Enum.TryParse<AppTheme>(stored, out var id) => All.First(t => t.Id == id),
            _ => All[0],
        };
        if (Enum.TryParse<BackgroundAnimation>(await cache.GetAsync<string>(AnimationKey), out var anim))
            Animation = anim;
        Changed?.Invoke();
    }

    public async Task SetThemeAsync(AppTheme id)
    {
        Current = All.First(t => t.Id == id);
        await cache.SetAsync(ThemeKey, id.ToString());
        Changed?.Invoke();
    }

    public async Task SetAnimationAsync(BackgroundAnimation animation)
    {
        Animation = animation;
        await cache.SetAsync(AnimationKey, animation.ToString());
        Changed?.Invoke();
    }

    public string CssClass => $"theme-{Current.Id.ToString().ToLowerInvariant()}";

    private static MudTheme Dark(string background, string surface, string bar, string drawer, string primary,
        string text, string secondary, string lines) => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = primary,
            Secondary = "#ED93B1",
            Background = background,
            Surface = surface,
            AppbarBackground = bar,
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
