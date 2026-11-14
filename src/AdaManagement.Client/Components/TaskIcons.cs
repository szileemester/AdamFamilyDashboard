using System.Globalization;
using System.Text;
using AdaManagement.Shared;
using MudBlazor;

namespace AdaManagement.Client.Components;

/// <summary>Egy választható feladat-ikon: kulcs (ez kerül az adatbázisba), felirat, ikon és a felismerő kulcsszavak.</summary>
public record TaskIcon(string Key, string Label, string Icon, string[] Keywords);

/// <summary>
/// A kártyák háttérikonjai. Kézi választás nélkül a feladat címéből találja ki (ékezet- és kisbetű-független
/// kulcsszó-egyezés, a lista sorrendjében – pl. az „állatorvos” előbb állat, mint orvos); ha semmi nem illik, a negyed ikonja.
/// </summary>
public static class TaskIcons
{
    public static readonly TaskIcon[] All =
    [
        new("pet", "Állat", Icons.Material.Outlined.Pets, ["allat", "kutya", "macska", "cica", "allatorvos", "oltas"]),
        new("gift", "Ajándék", Icons.Material.Outlined.CardGiftcard, ["ajandek", "szulinap", "szuletesnap", "nevnap", "evfordulo"]),
        new("car", "Autó", Icons.Material.Outlined.DirectionsCar, ["auto", "kocsi", "olaj", "gumi", "szerviz", "muszaki", "tankol", "benzin", "parkol", "matrica"]),
        new("health", "Egészség", Icons.Material.Outlined.LocalHospital, ["orvos", "fogorvos", "korhaz", "gyogyszer", "recept", "verve", "labor", "rendelo", "patika"]),
        new("bill", "Számla", Icons.Material.Outlined.ReceiptLong, ["szamla", "befizet", "utal", "csekk", "ado", "nav", "bank", "biztosit", "rezsi"]),
        new("shop", "Vásárlás", Icons.Material.Outlined.ShoppingCart, ["vasarl", "bevasarl", "bolt", "piac", "rendel", "vegyel", "venni"]),
        new("tools", "Javítás", Icons.Material.Outlined.Build, ["javit", "szerel", "kazan", "karbantart", "csere", "festes", "furo"]),
        new("clean", "Takarítás", Icons.Material.Outlined.CleaningServices, ["takarit", "mosas", "mosogat", "porszivo", "rendrak", "vasal", "ablak"]),
        new("sport", "Sport", Icons.Material.Outlined.FitnessCenter, ["edzes", "futas", "kondi", "uszas", "sport", "joga", "bicikli", "tura"]),
        new("code", "Fejlesztés", Icons.Material.Outlined.Code, ["kod", "deploy", "bug", "release", "review", "fejleszt", "teszt", "commit", "build"]),
        new("work", "Munka", Icons.Material.Outlined.Work, ["meeting", "megbeszel", "projekt", "munka", "prezentacio", "riport", "ugyfel"]),
        new("mail", "Levél", Icons.Material.Outlined.Mail, ["email", "e-mail", "level", "valaszol", "uzenet"]),
        new("phone", "Hívás", Icons.Material.Outlined.Phone, ["hivas", "hivni", "hivd", "felhiv", "telefon"]),
        new("event", "Időpont", Icons.Material.Outlined.Event, ["idopont", "foglal", "egyeztet", "naptar"]),
        new("travel", "Utazás", Icons.Material.Outlined.Flight, ["utaz", "repulo", "repjegy", "vonatjegy", "szallas", "nyaral", "utlevel"]),
        new("food", "Étel", Icons.Material.Outlined.Restaurant, ["foz", "vacsora", "ebed", "reggeli", "etterem", "suti"]),
        new("learn", "Tanulás", Icons.Material.Outlined.MenuBook, ["olvas", "konyv", "tanul", "vizsga", "kurzus", "iskola"]),
        new("docs", "Irat", Icons.Material.Outlined.Description, ["irat", "papir", "szerzodes", "nyomtat", "dokumentum", "szemelyi", "kerelem", "nyomtatvany"]),
        new("money", "Pénz", Icons.Material.Outlined.Savings, ["penz", "megtakarit", "befektet", "koltseg", "kiadas"]),
        new("plant", "Kert", Icons.Material.Outlined.LocalFlorist, ["virag", "noveny", "kert", "fu", "funyir", "locsol"]),
        new("home", "Otthon", Icons.Material.Outlined.Home, ["otthon", "lakas", "haz", "berles", "koltoz"]),
        new("party", "Ünnep", Icons.Material.Outlined.Celebration, ["buli", "parti", "unnep", "karacsony", "szilveszter"]),
        new("music", "Zene", Icons.Material.Outlined.MusicNote, ["zene", "gitar", "koncert"]),
        new("idea", "Ötlet", Icons.Material.Outlined.Lightbulb, ["otlet", "kitalal", "terv"]),
    ];

    /// <summary>Kulcs alapján (kézzel választott ikon); ismeretlen kulcsnál null.</summary>
    public static TaskIcon? ByKey(string? key) => key is null ? null : All.FirstOrDefault(i => i.Key == key);

    /// <summary>A címből kitalált ikon, vagy null, ha egy kulcsszó sem illik.</summary>
    public static TaskIcon? Guess(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var words = Normalize(title).Split([' ', '-', ',', '.', '/', ':', ';', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        // Szóeleji egyezés: „olajcsere” → olaj, de a „fu” (fű) ne illeszkedjen a „futas”-ra – ezért a rövid (≤2 betűs) kulcsszó csak teljes szóra.
        return All.FirstOrDefault(i => i.Keywords.Any(k => words.Any(w => k.Length <= 2 ? w == k : w.StartsWith(k, StringComparison.Ordinal) || (k.Length >= 4 && w.Contains(k, StringComparison.Ordinal)))));
    }

    /// <summary>Megjelenítendő ikon: kézi választás → cím alapján → a negyed ikonja.</summary>
    public static string For(TaskDto task, QuadrantInfo quadrant) =>
        ByKey(task.Icon)?.Icon ?? Guess(task.Title)?.Icon ?? quadrant.FlagIcons.LastOrDefault() ?? Icons.Material.Outlined.Bedtime;

    /// <summary>Kisbetű, ékezetek nélkül („Olajcsére” → „olajcsere”).</summary>
    private static string Normalize(string s)
    {
        var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }
}
