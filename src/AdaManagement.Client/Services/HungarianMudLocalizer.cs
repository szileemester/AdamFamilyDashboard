using Microsoft.Extensions.Localization;
using MudBlazor;

namespace AdaManagement.Client.Services;

/// <summary>
/// A MudBlazor beépített (angol) szövegei magyarul: kötelező mező, hibás dátum/szám, naptár gombok.
/// Amit nem ismer, azt a MudBlazor angol alapszövegére hagyja.
/// </summary>
public sealed class HungarianMudLocalizer : MudLocalizer
{
    private static readonly Dictionary<string, string> Texts = new()
    {
        ["MudFormComponent_Required"] = "Kötelező mező.",
        ["Converter_InvalidDateTime"] = "Érvénytelen dátum (pl. 2026.11.13).",
        ["Converter_InvalidNumber"] = "Érvénytelen szám.",
        ["Converter_InvalidTimeSpan"] = "Érvénytelen időtartam.",
        ["Converter_InvalidBoolean"] = "Érvénytelen érték.",
        ["Converter_InvalidGUID"] = "Érvénytelen azonosító.",
        ["Converter_InvalidType"] = "Érvénytelen érték.",
        ["Converter_NotValueOf"] = "Érvénytelen érték.",
        ["Converter_ConversionError"] = "Érvénytelen érték.",
        ["Converter_ConversionFailed"] = "Érvénytelen érték.",
        ["Converter_UnableToConvert"] = "Érvénytelen érték.",
        ["Converter_ConversionNotImplemented"] = "Érvénytelen érték.",
        ["MudBaseDatePicker_Open"] = "Naptár megnyitása",
        ["MudBaseDatePicker_PrevMonth"] = "Előző hónap",
        ["MudBaseDatePicker_NextMonth"] = "Következő hónap",
        ["MudBaseDatePicker_NextYear"] = "Következő év",
    };

    public override LocalizedString this[string key] =>
        Texts.TryGetValue(key, out var text)
            ? new LocalizedString(key, text)
            : new LocalizedString(key, key, resourceNotFound: true);

    public override LocalizedString this[string key, params object[] arguments] => this[key];
}
