using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace AdaManagement.Client.Services;

/// <param name="statusCode">A HTTP státuszkód (0, ha nem a szervertől jött válasz).</param>
public class ApiException(string message, int statusCode = 0) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>
/// Vékony réteg az /api fölött. 401 esetén a szerveroldali Google loginra navigál (BFF),
/// hibás kérésnél a ProblemDetails üzeneteit adja vissza olvasható formában.
/// </summary>
public class ApiClient(HttpClient http, NavigationManager nav)
{
    public const string CsrfHeader = "X-Ada-Client";

    public async Task<T> GetAsync<T>(string url, CancellationToken ct = default) =>
        (await ReadAsync<T>(await SendAsync(new HttpRequestMessage(HttpMethod.Get, url), ct), ct))!;

    public async Task<T> PostAsync<T>(string url, object? body = null, CancellationToken ct = default) =>
        (await ReadAsync<T>(await SendAsync(WithBody(HttpMethod.Post, url, body), ct), ct))!;

    public async Task PostAsync(string url, object? body = null, CancellationToken ct = default) =>
        await SendAsync(WithBody(HttpMethod.Post, url, body), ct);

    public async Task<T> PutAsync<T>(string url, object body, CancellationToken ct = default) =>
        (await ReadAsync<T>(await SendAsync(WithBody(HttpMethod.Put, url, body), ct), ct))!;

    public async Task DeleteAsync(string url, CancellationToken ct = default) =>
        await SendAsync(new HttpRequestMessage(HttpMethod.Delete, url), ct);

    private static HttpRequestMessage WithBody(HttpMethod method, string url, object? body) =>
        new(method, url) { Content = body is null ? null : JsonContent.Create(body) };

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Add(CsrfHeader, "1");
        var response = await http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var returnUrl = "/" + nav.ToBaseRelativePath(nav.Uri);
            nav.NavigateTo($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
            throw new ApiException("Bejelentkezés szükséges.", 401);
        }

        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ErrorMessageAsync(response, ct), (int)response.StatusCode);

        return response;
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(ct);

    private static async Task<string> ErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("errors", out var errors))
                return string.Join(" ", errors.EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString())));
            if (doc.RootElement.TryGetProperty("title", out var title))
                return title.GetString() ?? "Hiba";
        }
        catch (JsonException) { }
        return $"Szerverhiba ({(int)response.StatusCode}).";
    }
}
