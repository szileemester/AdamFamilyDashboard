using AdaManagement.Server.Tournaments.Domain.Brackets;
using AdaManagement.Server.Tournaments.Domain.Entities;
using AdaManagement.Shared.Tournaments;
using FluentValidation;

namespace AdaManagement.Server.Tournaments.Application;

/// <summary>
/// The one rule set for tournament settings, shared by create and edit - they accept exactly the same
/// shape, so a rule added for one always applies to the other.
/// </summary>
public class TournamentSettingsValidator<T> : AbstractValidator<T>
    where T : ITournamentSettings
{
    public TournamentSettingsValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A verseny neve kötelező.")
            .MaximumLength(Tournament.NameMaxLength).WithMessage($"A verseny neve legfeljebb {Tournament.NameMaxLength} karakter lehet.");

        RuleFor(x => x.Notes)
            .MaximumLength(Tournament.NotesMaxLength).WithMessage($"A megjegyzés legfeljebb {Tournament.NotesMaxLength} karakter lehet.");

        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.DefaultMatchFormat).IsInEnum();
        RuleFor(x => x.DefaultScoreType).IsInEnum();
        RuleFor(x => x.TiebreakerPolicy).IsInEnum();
        RuleFor(x => x.GroupStageMatchFormat).IsInEnum();
        RuleFor(x => x.UpperBracketFormat).IsInEnum();
        RuleFor(x => x.LowerBracketFormat).IsInEnum();
        RuleFor(x => x.GrandFinalFormat).IsInEnum();
        RuleFor(x => x.GroupStageKind).IsInEnum();
        RuleFor(x => x.PlayoffKind).IsInEnum();

        // Only the Group Stage format may allow draws (Best of 2) - every bracket match needs a winner.
        RuleFor(x => x.DefaultMatchFormat).Must(f => !f.AllowsDraw())
            .WithMessage("A Bo2 (két játszma) csak a csoportkörben választható.");
        RuleFor(x => x.UpperBracketFormat).Must(f => f is null || !f.Value.AllowsDraw())
            .WithMessage("A Bo2 (két játszma) csak a csoportkörben választható.");
        RuleFor(x => x.LowerBracketFormat).Must(f => f is null || !f.Value.AllowsDraw())
            .WithMessage("A Bo2 (két játszma) csak a csoportkörben választható.");
        RuleFor(x => x.GrandFinalFormat).Must(f => f is null || !f.Value.AllowsDraw())
            .WithMessage("A Bo2 (két játszma) csak a csoportkörben választható.");

        // A Swiss pool ranks on match wins, and a bye is a win with no games - Best of 2's games-won
        // ranking would leave it below every real win.
        RuleFor(x => x.GroupStageMatchFormat)
            .Must(f => f is null || !f.Value.AllowsDraw())
            .When(UsesSwiss)
            .WithMessage("Svájci csoportkörben nem lehet Bo2: válassz döntős formátumot.");

        RuleFor(x => x.ThirdPlaceEnabled)
            .Must((request, thirdPlace) => !thirdPlace || PlayoffIsSingleElimination(request))
            .WithMessage("Bronzmeccs csak egyenes kieséses ágban lehet.");

        RuleFor(x => x.GroupCount)
            .GreaterThanOrEqualTo(2)
            .When(x => x.Type == TournamentType.GroupStagePlayoff && x.GroupStageKind == GroupStageKind.RoundRobin)
            .WithMessage("Körmérkőzéses csoportkörhöz legalább 2 csoport kell.");

        // 0 is the "largest capacity the roster fills" default; anything else has to be a real capacity.
        RuleFor(x => x.PlayoffSize)
            .Must(size => size == 0 || GroupStagePlayoffBracket.SupportedPlayoffSizes.Contains(size))
            .When(x => x.Type == TournamentType.GroupStagePlayoff)
            .WithMessage($"A rájátszás mérete ezek egyike lehet: {string.Join(", ", GroupStagePlayoffBracket.SupportedPlayoffSizes)}.");

        // 0 is the ceil(log2 roster) default.
        RuleFor(x => x.SwissRounds)
            .InclusiveBetween(0, Tournament.MaxSwissRounds)
            .When(UsesSwiss)
            .WithMessage($"A svájci fordulók száma 1 és {Tournament.MaxSwissRounds} között lehet.");
    }

    private static bool UsesSwiss(T request) =>
        request.Type == TournamentType.GroupStagePlayoff && request.GroupStageKind == GroupStageKind.Swiss;

    private static bool PlayoffIsSingleElimination(T request) =>
        request.Type == TournamentType.SingleElimination
        || (request.Type == TournamentType.GroupStagePlayoff && request.PlayoffKind == PlayoffKind.SingleElimination);
}

public class CreateTournamentRequestValidator : TournamentSettingsValidator<CreateTournamentRequest>;

public class UpdateTournamentRequestValidator : TournamentSettingsValidator<UpdateTournamentRequest>;
