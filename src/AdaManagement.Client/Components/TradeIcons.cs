using MudBlazor;

namespace AdaManagement.Client.Components;

/// <summary>A szakik kártyáinak háttérikonja és színe a foglalkozásból (ékezet- és kisbetű-függetlenül).</summary>
public static class TradeIcons
{
    // Sorrendben keres, az első találat nyer: a szűkebb kulcsszavak a tágabbak előtt állnak
    // („állatorvos” az „orvos”, „ablakmosó” az „ablak” előtt).
    private static readonly (string[] Keys, string Icon)[] Map =
    [
        (["allatorv", "kutya"], Icons.Material.Filled.Pets),
        (["takarit", "ablakmos"], Icons.Material.Filled.CleaningServices),
        (["kemeny"], Icons.Material.Filled.LocalFireDepartment),
        (["villany", "elektrom"], Icons.Material.Filled.ElectricalServices),
        (["viz", "gaz", "futes", "kazan", "bojler"], Icons.Material.Filled.Plumbing),
        (["klima", "hut"], Icons.Material.Filled.AcUnit),
        (["fest", "mazol", "tapet"], Icons.Material.Filled.FormatPaint),
        (["asztalos", "acs", "butor"], Icons.Material.Filled.Carpenter),
        (["komuves", "burkol", "epit", "kivitelez", "felujit"], Icons.Material.Filled.Construction),
        (["teto", "badog"], Icons.Material.Filled.Roofing),
        (["ablak", "redony", "uveg"], Icons.Material.Filled.Window),
        (["lakatos", "zar", "kulcs"], Icons.Material.Filled.Key),
        (["auto", "gumi", "karossz", "szerviz"], Icons.Material.Filled.CarRepair),
        (["kert", "fuvag", "funyir", "fanyir"], Icons.Material.Filled.Grass),
        (["koltoz", "fuvar", "szallit"], Icons.Material.Filled.LocalShipping),
        (["fodrasz", "kozmetik", "manikur"], Icons.Material.Filled.ContentCut),
        (["orvos", "fogorv", "gyogytorn", "masszor"], Icons.Material.Filled.MedicalServices),
        (["ugyved", "kozjegyzo"], Icons.Material.Filled.Gavel),
        (["konyvel", "adotanacs"], Icons.Material.Filled.Calculate),
        (["informatik", "szamitogep", "rendszergazda", "it "], Icons.Material.Filled.Computer),
    ];

    public static string For(string occupation)
    {
        var o = Format.Fold(occupation) + " ";
        foreach (var (keys, icon) in Map)
            if (keys.Any(k => o.Contains(k))) return icon;
        return Icons.Material.Filled.Handyman;
    }

    /// <summary>Állandó árnyalat foglalkozásonként (a string.GetHashCode futásonként változna).</summary>
    public static int Hue(string occupation)
    {
        var h = 7;
        foreach (var c in Format.Fold(occupation)) h = (h * 31 + c) % 3600;
        return h % 360;
    }
}
