using System.Net.Http.Json;
using AdaManagement.Server.Data;
using AdaManagement.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace AdaManagement.Tests.Api;

/// <summary>Állítható óra: a tesztek előre tekerhetik az időt (pl. a törölt sablonok egy nap utáni végleges törléséhez).</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>
/// Végpont-tesztek közös környezete: egy valódi SQL Server Docker-konténerben (Testcontainers), rajta a migrációk,
/// és a szerver a memóriában (WebApplicationFactory). A belépés a fejlesztői /account/dev-login úton megy (nincs Google).
/// A konténer az összes végpont-tesztnek közös, ezért minden teszt a saját maga létrehozta adatokon ellenőriz.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public TestClock Clock { get; } = new();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        // Az adatbázist előre létrehozzuk: az app újrapróbálási stratégiája a „nincs ilyen adatbázis” hibát átmenetinek
        // veszi, így a Migrate percekig várna rá.
        await using (var master = new SqlConnection(_sql.GetConnectionString()))
        {
            await master.OpenAsync();
            await using var create = new SqlCommand("CREATE DATABASE AdaApiTests", master);
            await create.ExecuteNonQueryAsync();
        }
        var connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "AdaApiTests" }.ConnectionString;

        // Migráció még a szerver indulása előtt (induláskor már olvasná a táblákat, pl. a sütikulcsokat).
        await using (var migrator = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options))
            await migrator.Database.MigrateAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); // dev-login elérhető, Google nélkül
            // Az alkalmazás saját forrásai (appsettings.Development.json: LocalDB!) után jön, így ez nyer. Csak a lustán
            // (kérésenként) olvasott értéket lehet így felülírni: az engedélyezett címeket a Program már korábban
            // beolvassa, ezért a belépés a fejlesztői beállítás dev@localhost címével megy.
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
            }));
            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(Clock));
        });

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Biztosíték: a tesztek soha ne a fejlesztői (LocalDB) adatbázison fussanak.
        var used = db.Database.GetConnectionString() ?? "";
        if (!used.Contains("AdaApiTests") || used.Contains("localdb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A teszt nem a konténeres adatbázisra csatlakozna: " + used);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _sql.DisposeAsync();
    }

    /// <summary>Kliens süti-kezeléssel, átirányítás-követés nélkül (a Location fejlécet így ellenőrizni lehet).</summary>
    public HttpClient Anonymous() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Bejelentkezett kliens a CSRF-fejléccel, mint a WASM kliens.</summary>
    public async Task<HttpClient> OwnerAsync()
    {
        var client = Anonymous();
        var login = await client.GetAsync("/account/dev-login");
        if ((int)login.StatusCode != 302) throw new InvalidOperationException($"dev-login: {login.StatusCode}");
        client.DefaultRequestHeaders.Add("X-Ada-Client", "1");
        return client;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

/// <summary>Rövid segédek a JSON-hívásokhoz.</summary>
public static class Http
{
    public static async Task<T> Get<T>(this HttpClient c, string url)
    {
        var r = await c.GetAsync(url);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<T>())!;
    }

    public static async Task<T> Post<T>(this HttpClient c, string url, object? body = null)
    {
        var r = await c.PostAsync(url, body is null ? null : JsonContent.Create(body));
        if (!r.IsSuccessStatusCode) throw new HttpRequestException($"{url}: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        return (await r.Content.ReadFromJsonAsync<T>())!;
    }

    public static async Task<T> Put<T>(this HttpClient c, string url, object body)
    {
        var r = await c.PutAsJsonAsync(url, body);
        if (!r.IsSuccessStatusCode) throw new HttpRequestException($"{url}: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        return (await r.Content.ReadFromJsonAsync<T>())!;
    }

    public static Task<DashboardDto> Dashboard(this HttpClient c) => c.Get<DashboardDto>("api/dashboard");
}
