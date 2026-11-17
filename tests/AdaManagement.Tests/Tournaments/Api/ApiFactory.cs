using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdaManagement.Tests.Api;
using FluentAssertions;

namespace AdaManagement.Tests.Tournaments.Api;

/// <summary>
/// Az Adaminator integrációs tesztjeinek „gyára”, az AdaManagement közös SQL Server konténerére (ApiFixture) ültetve.
/// Ugyanazt a felületet adja, mint az eredeti: névtelen kliens, bejelentkezett kliens, verseny létrehozása.
/// A bejelentkezés itt a fejlesztői Google-pótló dev-login (nem admin jelszó + JWT).
/// </summary>
public sealed class ApiFactory(ApiFixture fixture)
{
    /// <summary>Az API saját szerializálása: camelCase, enum stringként.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public IServiceProvider Services => fixture.Factory.Services;

    /// <summary>Bejelentkezés nélküli kliens. A CSRF-fejlécet viszi (mint a WASM kliens), így a hiányzó belépés miatt bukik el, nem a fejléc miatt.</summary>
    public HttpClient CreateClient()
    {
        var client = fixture.Anonymous();
        client.DefaultRequestHeaders.Add("X-Ada-Client", "1");
        return client;
    }

    public Task<HttpClient> CreateAuthenticatedClientAsync() => fixture.OwnerAsync();

    /// <summary>
    /// Létrehoz egy versenyt és visszaadja az azonosítóját. Az alapértékek a legtöbb tesztnek megfelelnek (egyenes kiesés,
    /// ütközésmentes név); amit a tesztek változtatnak, az paraméter.
    /// </summary>
    public static async Task<Guid> CreateTournamentAsync(
        HttpClient client,
        string type = "SingleElimination",
        bool thirdPlaceEnabled = false,
        string defaultMatchFormat = "Bo3",
        string? groupStageMatchFormat = null,
        int groupCount = 0)
    {
        var response = await client.PostAsJsonAsync("/api/tournaments", new
        {
            name = $"Cup {Guid.NewGuid():N}",
            date = "2026-07-14",
            type,
            defaultMatchFormat,
            groupStageMatchFormat,
            thirdPlaceEnabled,
            groupCount
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedTournament>(JsonOptions))!.Id;
    }

    public record CreatedTournament(Guid Id, string PublicToken);
}
