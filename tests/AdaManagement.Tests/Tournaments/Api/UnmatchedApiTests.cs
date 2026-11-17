using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using AdaManagement.Tests.Api;
using FluentAssertions;

namespace AdaManagement.Tests.Tournaments.Api;

/// <summary>
/// The house Unmatched ladder. Anyone can read it from /api/public/unmatched without logging in; only the
/// logged-in owner can write it (PUT /api/unmatched), and the domain's validation still guards the row.
/// </summary>
[Collection(ApiCollection.Name)]
public class UnmatchedApiTests
{
    // The API's own serializer settings, so the suite reads responses exactly as the API writes them.
    private static readonly JsonSerializerOptions JsonOptions = ApiFactory.JsonOptions;

    private readonly ApiFactory _factory;

    public UnmatchedApiTests(ApiFixture fixture)
    {
        _factory = new ApiFactory(fixture);
    }

    [Fact]
    public async Task The_scoreboard_is_readable_without_logging_in()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/public/unmatched");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ScoreboardResponse>(JsonOptions)).Should().NotBeNull();
    }

    [Fact]
    public async Task Writing_the_scoreboard_requires_logging_in()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PutAsJsonAsync("/api/unmatched", new
        {
            fiukWins = 2,
            lanyokWins = 1,
            lastVictor = "Fiuk",
            picks = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_saved_scoreboard_comes_back_whole_and_replaces_what_was_there()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var first = await client.PutAsJsonAsync("/api/unmatched", new
        {
            fiukWins = 7,
            lanyokWins = 4,
            lastVictor = "Fiuk",
            picks = new[]
            {
                new { playerName = "Márk", character = "Medusa" },
                new { playerName = "Ádám", character = "Dracula" },
                new { playerName = "Berni", character = "Alice" },
                new { playerName = "Reni", character = "Titania" },
            },
        });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var board = await client.GetFromJsonAsync<ScoreboardResponse>("/api/public/unmatched", JsonOptions);
        board!.FiukWins.Should().Be(7);
        board.LanyokWins.Should().Be(4);
        board.LastVictor.Should().Be("Fiuk");
        board.Picks.Should().HaveCount(4);
        board.Picks.Single(p => p.PlayerName == "Márk").Character.Should().Be("Medusa");

        // A second write replaces rather than accumulates: no history is kept.
        await client.PutAsJsonAsync("/api/unmatched", new
        {
            fiukWins = 7,
            lanyokWins = 5,
            lastVictor = "Lanyok",
            picks = new[] { new { playerName = "Reni", character = "Sinbad" } },
        });

        var updated = await client.GetFromJsonAsync<ScoreboardResponse>("/api/public/unmatched", JsonOptions);
        updated!.LanyokWins.Should().Be(5);
        updated.LastVictor.Should().Be("Lanyok");
        updated.Picks.Should().ContainSingle(p => p.PlayerName == "Reni" && p.Character == "Sinbad");
    }

    [Fact]
    public async Task A_rejected_scoreboard_leaves_the_stored_one_untouched()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        await client.PutAsJsonAsync("/api/unmatched", new
        {
            fiukWins = 3,
            lanyokWins = 2,
            lastVictor = "Fiuk",
            picks = new[] { new { playerName = "Márk", character = "Medusa" } },
        });

        var rejected = await client.PutAsJsonAsync("/api/unmatched", new
        {
            fiukWins = 4,
            lanyokWins = 2,
            lastVictor = "Fiuk",
            // The same player twice - the domain refuses the whole write.
            picks = new[]
            {
                new { playerName = "Reni", character = "Alice" },
                new { playerName = "reni", character = "Titania" },
            },
        });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var board = await client.GetFromJsonAsync<ScoreboardResponse>("/api/public/unmatched", JsonOptions);
        board!.FiukWins.Should().Be(3);
        board.Picks.Should().ContainSingle(p => p.PlayerName == "Márk");
    }

    private record ScoreboardResponse(int FiukWins, int LanyokWins, string? LastVictor, List<PickResponse> Picks);
    private record PickResponse(string PlayerName, string Character);
}
