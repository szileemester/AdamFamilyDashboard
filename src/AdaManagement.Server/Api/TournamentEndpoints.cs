using AdaManagement.Server.Tournaments.Application;
using AdaManagement.Server.Tournaments.Domain.Exceptions;
using AdaManagement.Shared.Tournaments;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AdaManagement.Server.Api;

/// <summary>
/// A versenymodul végpontjai (az Adaminator controllereinek megfelelői, ugyanazokkal az útvonalakkal és válaszokkal).
/// A /api csoport alatt vannak, így bejelentkezést és X-Ada-Client fejlécet kérnek; a nyilvános olvasások (nézői link,
/// Unmatched tábla) a bejelentkezés nélküli /api/public csoportban.
/// </summary>
public static class TournamentEndpoints
{
    public static void MapTournaments(this RouteGroupBuilder api)
    {
        var t = api.MapGroup("/tournaments").AddEndpointFilter(TranslateErrors);

        t.MapGet("/", (TournamentService s, CancellationToken ct) => s.GetAllAsync(ct));
        t.MapGet("/{id:guid}", async (Guid id, TournamentService s, CancellationToken ct) =>
            await s.GetByIdAsync(id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound());
        t.MapPost("/", async (CreateTournamentRequest request, TournamentService s, CancellationToken ct) =>
        {
            var created = await s.CreateAsync(request, ct);
            return Results.Created($"/api/tournaments/{created.Id}", created);
        });
        t.MapPut("/{id:guid}", (Guid id, UpdateTournamentRequest request, TournamentService s, CancellationToken ct) =>
            s.UpdateAsync(id, request, ct));
        t.MapDelete("/{id:guid}", async (Guid id, TournamentService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        // Résztvevők
        var p = t.MapGroup("/{tournamentId:guid}/participants");
        p.MapGet("/", (Guid tournamentId, ParticipantService s, CancellationToken ct) => s.ListAsync(tournamentId, ct));
        p.MapPost("/", (Guid tournamentId, AddParticipantRequest request, ParticipantService s, CancellationToken ct) =>
            s.AddAsync(tournamentId, request, ct));
        p.MapPut("/", (Guid tournamentId, ReplaceRosterRequest request, ParticipantService s, CancellationToken ct) =>
            s.ReplaceAsync(tournamentId, request, ct));
        p.MapPut("/{participantId:guid}", (Guid tournamentId, Guid participantId, UpdateParticipantRequest request, ParticipantService s, CancellationToken ct) =>
            s.UpdateAsync(tournamentId, participantId, request, ct));
        p.MapDelete("/{participantId:guid}", async (Guid tournamentId, Guid participantId, ParticipantService s, CancellationToken ct) =>
        {
            await s.RemoveAsync(tournamentId, participantId, ct);
            return Results.NoContent();
        });

        // Ág, sorsolás, életciklus
        var b = t.MapGroup("/{tournamentId:guid}");
        b.MapGet("/bracket", (Guid tournamentId, BracketService s, CancellationToken ct) => s.GetBracketAsync(tournamentId, ct));
        b.MapPost("/bracket/generate", (Guid tournamentId, BracketService s, CancellationToken ct) => s.GenerateAsync(tournamentId, ct));
        b.MapPut("/bracket", (Guid tournamentId, UpdateBracketRequest request, BracketService s, CancellationToken ct) =>
            s.UpdateAsync(tournamentId, request, ct));
        b.MapPost("/bracket/draw-groups", (Guid tournamentId, BracketService s, CancellationToken ct) => s.DrawGroupsAsync(tournamentId, ct));
        b.MapPost("/start", (Guid tournamentId, BracketService s, CancellationToken ct) => s.StartAsync(tournamentId, ct));
        b.MapPost("/start-playoffs", (Guid tournamentId, BracketService s, CancellationToken ct) => s.StartPlayoffsAsync(tournamentId, ct));
        b.MapPost("/start-swiss-round", (Guid tournamentId, BracketService s, CancellationToken ct) => s.StartNextSwissRoundAsync(tournamentId, ct));
        b.MapPost("/start-tiebreakers", (Guid tournamentId, BracketService s, CancellationToken ct) => s.StartTiebreakersAsync(tournamentId, ct));
        b.MapPost("/finish", (Guid tournamentId, BracketService s, CancellationToken ct) => s.FinishAsync(tournamentId, ct));

        // Meccsek
        var m = t.MapGroup("/{tournamentId:guid}/matches/{matchId:guid}");
        m.MapPut("/result", (Guid tournamentId, Guid matchId, MatchScoreRequest request, MatchService s, CancellationToken ct) =>
            s.SaveResultAsync(tournamentId, matchId, request, ct));
        m.MapPost("/complete", (Guid tournamentId, Guid matchId, MatchScoreRequest request, MatchService s, CancellationToken ct) =>
            s.CompleteAsync(tournamentId, matchId, request, ct));
        m.MapPost("/forfeit", (Guid tournamentId, Guid matchId, ForfeitMatchRequest request, MatchService s, CancellationToken ct) =>
            s.ForfeitAsync(tournamentId, matchId, request, ct));
        m.MapPost("/undo", (Guid tournamentId, Guid matchId, MatchService s, CancellationToken ct) =>
            s.UndoAsync(tournamentId, matchId, ct));

        // Unmatched: írni csak bejelentkezve lehet (az olvasás a nyilvános csoportban van).
        api.MapPut("/unmatched", (UpdateUnmatchedScoreboardRequest request, UnmatchedService s, CancellationToken ct) =>
            s.UpdateAsync(request, ct)).AddEndpointFilter(TranslateErrors);
    }

    /// <summary>Bejelentkezés nélkül elérhető, csak olvasó végpontok: a nézői link és az Unmatched tábla.</summary>
    public static void MapPublicTournaments(this RouteGroupBuilder pub)
    {
        pub.MapGet("/tournaments/{token}", async (string token, TournamentService s, CancellationToken ct) =>
            await s.GetByPublicTokenAsync(token, ct) is { } dto ? Results.Ok(dto) : Results.NotFound());
        pub.MapGet("/unmatched", (UnmatchedService s, CancellationToken ct) => s.GetAsync(ct));
    }

    /// <summary>
    /// A modul kivételei → ProblemDetails (mint az Adaminator GlobalExceptionHandlere): validáció és üzleti szabály 400,
    /// nem található 404, egyidejű módosítás 409. Minden más tovább megy (500).
    /// </summary>
    private static async ValueTask<object?> TranslateErrors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ValidationException e)
        {
            return Results.ValidationProblem(e.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray()));
        }
        catch (DomainException e)
        {
            return Results.Problem(title: e.Message, detail: e.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (NotFoundException e)
        {
            return Results.Problem(title: e.Message, detail: e.Message, statusCode: StatusCodes.Status404NotFound);
        }
        // Csak a két „valaki megelőzött” eset: elveszett sorverzió-ellenőrzés és egyedi kulcs ütközése (2601/2627).
        catch (DbUpdateException e) when (e is DbUpdateConcurrencyException || e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            const string message = "Közben valaki más is módosította. Töltsd újra, és próbáld meg ismét.";
            return Results.Problem(title: message, detail: message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
