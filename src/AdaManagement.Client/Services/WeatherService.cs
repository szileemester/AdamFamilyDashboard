using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AdaManagement.Client.Services;

public record WeatherDay(DateOnly Date, int Code, double Max, double Min, double PrecipitationMm, int? PrecipitationProbability);

public record WeatherForecast(string Label, double Latitude, double Longitude, DateTime FetchedAtUtc, IReadOnlyList<WeatherDay> Days);

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

    /// <summary>Friss előrejelzés érkezett (pl. a háttéranimáció ehhez igazodik).</summary>
    public event Action<WeatherForecast>? Updated;

    public Task<WeatherForecast?> GetCachedAsync() => cache.GetAsync<WeatherForecast>(CacheKey);

    public async Task<WeatherForecast> GetAsync(string label, double lat, double lon, bool force = false)
    {
        var cached = await GetCachedAsync();
        if (!force && cached is not null && cached.Latitude == lat && cached.Longitude == lon &&
            DateTime.UtcNow - cached.FetchedAtUtc < MaxAge)
            return cached;

        var inv = CultureInfo.InvariantCulture;
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(inv)}&longitude={lon.ToString(inv)}" +
                  "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max" +
                  "&timezone=Europe%2FBudapest&forecast_days=3";
        var resp = await external.GetFromJsonAsync<ForecastResponse>(url)
                   ?? throw new InvalidOperationException("Üres válasz az Open-Meteótól.");

        var d = resp.Daily;
        var days = d.Time.Select((t, i) => new WeatherDay(
            DateOnly.Parse(t, inv), d.WeatherCode[i] ?? 0, d.Max[i] ?? 0, d.Min[i] ?? 0, d.Precipitation[i] ?? 0, d.PrecipitationProbability?[i]))
            .ToList();

        var forecast = new WeatherForecast(label, lat, lon, DateTime.UtcNow, days);
        await cache.SetAsync(CacheKey, forecast);
        Updated?.Invoke(forecast);
        return forecast;
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

    private sealed record ForecastResponse([property: JsonPropertyName("daily")] Daily Daily);

    private sealed record Daily(
        [property: JsonPropertyName("time")] string[] Time,
        [property: JsonPropertyName("weather_code")] int?[] WeatherCode,
        [property: JsonPropertyName("temperature_2m_max")] double?[] Max,
        [property: JsonPropertyName("temperature_2m_min")] double?[] Min,
        [property: JsonPropertyName("precipitation_sum")] double?[] Precipitation,
        [property: JsonPropertyName("precipitation_probability_max")] int?[]? PrecipitationProbability);

    private sealed record GeoResponse([property: JsonPropertyName("results")] GeoResult[]? Results);

    private sealed record GeoResult(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("admin1")] string? Admin1,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);
}
