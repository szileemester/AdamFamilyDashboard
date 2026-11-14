using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AdaManagement.Client.Components;

namespace AdaManagement.Client.Services;

public record WeatherDay(DateOnly Date, int Code, double Max, double Min, double PrecipitationMm, int? PrecipitationProbability,
    double? WindMaxKmh = null);

/// <summary>Egy óra (helyi, budapesti idő szerint).</summary>
public record WeatherHour(DateTime Time, int Code, double Temp, int? PrecipitationProbability, double PrecipitationMm, double? WindKmh, bool IsDay);

public record WeatherForecast(string Label, double Latitude, double Longitude, DateTime FetchedAtUtc, IReadOnlyList<WeatherDay> Days,
    IReadOnlyList<WeatherHour>? Hours = null)
{
    /// <summary>Az adott nap órái (a mai napnál csak a mostani órától).</summary>
    public IEnumerable<WeatherHour> HoursOf(DateOnly date, DateTime nowLocal) =>
        (Hours ?? []).Where(h => DateOnly.FromDateTime(h.Time) == date && h.Time > nowLocal.AddHours(-1));
}

public record GeoPlace(string Name, string? Admin, string? Country, double Latitude, double Longitude)
{
    public string Display => string.Join(", ", new[] { Name, Admin, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>
/// Open-Meteo közvetlenül a böngészőből (ingyenes, kulcs nélkül). Az eredmény 30 percig a localStorage-ban marad.
/// Külön HttpClientet használ, egyedi fejlécek nélkül, hogy ne legyen CORS preflight.
/// </summary>
public class WeatherService(HttpClient external, LocalCache cache)
{
    private const string CacheKey = "ada.weather";
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    /// <summary>Ennyi napot kér le (a kártyák ebből 3-at mutatnak, a heti ablak mindet).</summary>
    public const int ForecastDays = 7;

    /// <summary>
    /// A tárolt előrejelzés azonnali megjelenítéshez – csak ha ugyanehhez a helyhez szól, és a már elmúlt napok nélkül
    /// (különben egy pár napos cache „mai” napja valójában egy korábbi nap lenne).
    /// </summary>
    public async Task<WeatherForecast?> GetCachedAsync(double lat, double lon)
    {
        var cached = await cache.GetAsync<WeatherForecast>(CacheKey);
        return cached is null || cached.Latitude != lat || cached.Longitude != lon ? null : FromToday(cached);
    }

    private static WeatherForecast? FromToday(WeatherForecast f)
    {
        var today = Format.Today;
        var days = f.Days.Where(d => d.Date >= today).ToList();
        return days.Count == 0 ? null : f with { Days = days };
    }

    public async Task<WeatherForecast> GetAsync(string label, double lat, double lon, bool force = false)
    {
        var cached = await GetCachedAsync(lat, lon);
        if (!force && cached is not null && cached.Days[0].Date == Format.Today && cached.Hours is { Count: > 0 } &&
            DateTime.UtcNow - cached.FetchedAtUtc < MaxAge)
            return cached;

        var inv = CultureInfo.InvariantCulture;
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(inv)}&longitude={lon.ToString(inv)}" +
                  "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max,wind_speed_10m_max" +
                  "&hourly=temperature_2m,weather_code,precipitation_probability,precipitation,wind_speed_10m,is_day" +
                  $"&timezone=Europe%2FBudapest&forecast_days={ForecastDays}";
        var resp = await external.GetFromJsonAsync<ForecastResponse>(url)
                   ?? throw new InvalidOperationException("Üres válasz az Open-Meteótól.");

        var d = resp.Daily;
        var days = d.Time.Select((t, i) => new WeatherDay(
            DateOnly.Parse(t, inv), d.WeatherCode[i] ?? 0, d.Max[i] ?? 0, d.Min[i] ?? 0, d.Precipitation[i] ?? 0, d.PrecipitationProbability?[i], d.WindMax?[i]))
            .ToList();

        var h = resp.Hourly;
        List<WeatherHour> hours = h is null ? [] : h.Time.Select((t, i) => new WeatherHour(
            DateTime.Parse(t, inv), h.WeatherCode[i] ?? 0, h.Temp[i] ?? 0, h.PrecipitationProbability?[i],
            h.Precipitation?[i] ?? 0, h.Wind?[i], (h.IsDay?[i] ?? 1) == 1))
            .ToList();

        var forecast = new WeatherForecast(label, lat, lon, DateTime.UtcNow, days, hours);
        await cache.SetAsync(CacheKey, forecast);
        return FromToday(forecast) ?? forecast;
    }

    public async Task<IReadOnlyList<GeoPlace>> SearchPlacesAsync(string name)
    {
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(name)}&count=6&language=hu&format=json";
        var resp = await external.GetFromJsonAsync<GeoResponse>(url);
        return resp?.Results?.Select(r => new GeoPlace(r.Name, r.Admin1, r.Country, r.Latitude, r.Longitude)).ToList() ?? [];
    }

    /// <summary>WMO időjárás-kód → (ikon, szöveg).</summary>
    public static (string Icon, string Text) Describe(int code) => code switch
    {
        0 => ("☀️", "Derült"),
        1 => ("🌤️", "Többnyire derült"),
        2 => ("⛅", "Részben felhős"),
        3 => ("☁️", "Borult"),
        45 or 48 => ("🌫️", "Köd"),
        51 or 53 or 55 => ("🌦️", "Szitálás"),
        56 or 57 => ("🌧️", "Ónos szitálás"),
        61 => ("🌦️", "Gyenge eső"),
        63 => ("🌧️", "Eső"),
        65 => ("🌧️", "Erős eső"),
        66 or 67 => ("🌧️", "Ónos eső"),
        71 => ("🌨️", "Gyenge havazás"),
        73 => ("🌨️", "Havazás"),
        75 => ("❄️", "Erős havazás"),
        77 => ("🌨️", "Hószemcse"),
        80 or 81 => ("🌦️", "Zápor"),
        82 => ("⛈️", "Heves zápor"),
        85 or 86 => ("🌨️", "Hózápor"),
        95 => ("⛈️", "Zivatar"),
        96 or 99 => ("⛈️", "Zivatar jégesővel"),
        _ => ("🌡️", "Ismeretlen"),
    };

    /// <summary>Éjjel a derült/többnyire derült ikon hold.</summary>
    public static (string Icon, string Text) Describe(int code, bool isDay) =>
        !isDay && code is 0 or 1 ? ("🌙", code == 0 ? "Derült" : "Többnyire derült") : Describe(code);

    private sealed record ForecastResponse(
        [property: JsonPropertyName("daily")] Daily Daily,
        [property: JsonPropertyName("hourly")] Hourly? Hourly);

    private sealed record Hourly(
        [property: JsonPropertyName("time")] string[] Time,
        [property: JsonPropertyName("weather_code")] int?[] WeatherCode,
        [property: JsonPropertyName("temperature_2m")] double?[] Temp,
        [property: JsonPropertyName("precipitation_probability")] int?[]? PrecipitationProbability,
        [property: JsonPropertyName("precipitation")] double?[]? Precipitation,
        [property: JsonPropertyName("wind_speed_10m")] double?[]? Wind,
        [property: JsonPropertyName("is_day")] int?[]? IsDay);

    private sealed record Daily(
        [property: JsonPropertyName("time")] string[] Time,
        [property: JsonPropertyName("weather_code")] int?[] WeatherCode,
        [property: JsonPropertyName("temperature_2m_max")] double?[] Max,
        [property: JsonPropertyName("temperature_2m_min")] double?[] Min,
        [property: JsonPropertyName("precipitation_sum")] double?[] Precipitation,
        [property: JsonPropertyName("precipitation_probability_max")] int?[]? PrecipitationProbability,
        [property: JsonPropertyName("wind_speed_10m_max")] double?[]? WindMax);

    private sealed record GeoResponse([property: JsonPropertyName("results")] GeoResult[]? Results);

    private sealed record GeoResult(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("admin1")] string? Admin1,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);
}
