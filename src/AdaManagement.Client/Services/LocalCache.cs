using System.Text.Json;
using Microsoft.JSInterop;

namespace AdaManagement.Client.Services;

/// <summary>
/// localStorage-alapú gyorsítótár a gyors induláshoz. Minden hiba (privát mód, tele tárhely) csendben elnyelődik:
/// a cache csak kényelmi funkció, az app nélküle is működik.
/// </summary>
public class LocalCache(IJSRuntime js)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", key);
            return raw is null ? default : JsonSerializer.Deserialize<T>(raw, Json);
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(value, Json));
        }
        catch
        {
            // nem baj
        }
    }
}
